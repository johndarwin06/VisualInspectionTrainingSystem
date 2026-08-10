#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Performance
{
    /// <summary>
    /// Measures configuration, quiz interaction, export, and logging workloads with
    /// deterministic local data and hard functional/resource cleanup assertions.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Performance)]
    [NonParallelizable]
    public sealed class CoreOperationPerformanceTests
    {
        #region Fields

        private string _temporaryDirectory;

        #endregion

        #region Lifecycle

        /// <summary>Creates an isolated output root.</summary>
        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "VITS-I18-Core-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        /// <summary>Deletes configuration, logs, and exports after every workload.</summary>
        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_temporaryDirectory) &&
                Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }

            Assert.That(Directory.Exists(_temporaryDirectory), Is.False);
        }

        #endregion

        #region Startup Components

        /// <summary>
        /// Measures repeated validated configuration loading without production paths.
        /// </summary>
        [Test]
        public void ConfigurationLoading_IsValidatedAndDoesNotRetainFileHandles()
        {
            string settingsFile = CreateIsolatedSettings();
            ApplicationSettings latest = null;

            using (ConfigurationService.UseSettingsFileForTesting(settingsFile))
            {
                PerformanceMeasurement.Measure(
                    "Startup.ConfigurationLoad",
                    2,
                    15,
                    delegate
                    {
                        latest = ConfigurationService.GetApplicationSettings();
                    });
            }

            File.Delete(settingsFile);

            Assert.Multiple(delegate
            {
                Assert.That(latest, Is.Not.Null);
                Assert.That(latest.Database.ConnectionTimeoutSeconds, Is.EqualTo(1));
                Assert.That(File.Exists(settingsFile), Is.False);
            });
        }

        #endregion

        #region Quiz Interaction

        /// <summary>
        /// Measures complete in-memory GOOD/NG interaction for both supported quiz sizes.
        /// </summary>
        [TestCase(ImageService.DefaultQuizSize)]
        [TestCase(ImageService.ExtendedQuizSize)]
        public void QuizInteraction_RecordsEveryAnswerAndCompletesOnce(int questionCount)
        {
            QuizEngine latest = null;

            PerformanceMeasurement.Measure(
                "Quiz.Interaction." + questionCount,
                3,
                21,
                delegate
                {
                    latest = CreateCompletedQuiz(questionCount);
                });

            Assert.Multiple(delegate
            {
                Assert.That(latest, Is.Not.Null);
                Assert.That(latest.IsCompleted(), Is.True);
                Assert.That(latest.Session.Answers, Has.Count.EqualTo(questionCount));
                Assert.That(latest.TrySubmitAnswer(QuizAnswerType.Good), Is.False);
            });
        }

        #endregion

        #region Export Throughput

        /// <summary>
        /// Measures real CSV, XLSX, and PDF generation for representative row counts.
        /// </summary>
        [TestCase("csv", 100)]
        [TestCase("csv", 1000)]
        [TestCase("csv", 5000)]
        [TestCase("xlsx", 100)]
        [TestCase("xlsx", 1000)]
        [TestCase("xlsx", 5000)]
        [TestCase("pdf", 100)]
        [TestCase("pdf", 1000)]
        [TestCase("pdf", 5000)]
        public void Export_DeterministicSnapshotProducesReadableOutput(
            string extension,
            int rowCount)
        {
            string path = Path.Combine(
                _temporaryDirectory,
                "performance." + extension);
            ReportSnapshot snapshot = CreateReportSnapshot(rowCount);
            ReportExportService service = new ReportExportService();

            PerformanceMeasurement.Measure(
                "Export." + extension.ToUpperInvariant() + "." + rowCount,
                1,
                3,
                delegate
                {
                    Export(service, extension, snapshot, path);
                });

            AssertExportReadable(extension, path, rowCount);
        }

        #endregion

        #region Logging Overhead

        /// <summary>
        /// Measures concurrent queue pressure, rollover, flush, and shutdown cleanup.
        /// </summary>
        [Test]
        public void Logging_ConcurrentThroughputFlushesWithoutSensitiveOutput()
        {
            string primary = Path.Combine(_temporaryDirectory, "Logs");
            string fallback = Path.Combine(_temporaryDirectory, "Fallback");

            using (ApplicationErrorLogger.UseConfigurationForTesting(
                       primary,
                       fallback,
                       ApplicationLogLevel.Debug,
                       32L * 1024L,
                       3))
            {
                PerformanceMeasurement.Measure(
                    "Logging.Concurrent.1000",
                    1,
                    5,
                    delegate
                    {
                        Parallel.For(
                            0,
                            1000,
                            index => ApplicationErrorLogger.LogInformation(
                                "Performance",
                                "Bounded performance event " + index + "."));

                        Assert.That(
                            ApplicationErrorLogger.Flush(TimeSpan.FromSeconds(5)),
                            Is.True);
                    });
            }

            string content = ReadAllLogs(primary) + ReadAllLogs(fallback);

            Assert.Multiple(delegate
            {
                Assert.That(content, Does.Contain("Source: Performance"));
                Assert.That(content, Does.Not.Contain("Password="));
                Assert.That(content, Does.Not.Contain("Connection String="));
                Assert.That(CountLogFiles(primary) + CountLogFiles(fallback), Is.LessThanOrEqualTo(4));
            });
        }

        #endregion

        #region Fixtures

        private string CreateIsolatedSettings()
        {
            string images = Path.Combine(_temporaryDirectory, "Images");
            string logs = Path.Combine(_temporaryDirectory, "Logs");
            string exports = Path.Combine(_temporaryDirectory, "Exports");
            string reports = Path.Combine(_temporaryDirectory, "Reports");

            Directory.CreateDirectory(images);
            Directory.CreateDirectory(logs);
            Directory.CreateDirectory(exports);
            Directory.CreateDirectory(reports);

            string settingsFile = Path.Combine(
                _temporaryDirectory,
                "DatabaseSettings.performance.config");

            XDocument document = new XDocument(
                new XElement(
                    "applicationSettings",
                    new XElement(
                        "mysql",
                        new XAttribute("server", "127.0.0.1"),
                        new XAttribute("port", "1"),
                        new XAttribute("database", "vits_performance_test"),
                        new XAttribute("username", "performance_runner"),
                        new XAttribute("password", "test-placeholder"),
                        new XAttribute("sslMode", "Disabled"),
                        new XAttribute("connectionTimeoutSeconds", "1"),
                        new XAttribute("retryCount", "0"),
                        new XAttribute("retryDelayMilliseconds", "0")),
                    new XElement(
                        "paths",
                        new XAttribute("quizImageFolder", images),
                        new XAttribute("logFolder", logs),
                        new XAttribute("exportFolder", exports),
                        new XAttribute("reportFolder", reports))));

            document.Save(settingsFile);
            return settingsFile;
        }

        private static QuizEngine CreateCompletedQuiz(int questionCount)
        {
            User user = new User
            {
                EmployeeNo = "PERF-TRAINEE",
                FullName = "Performance Trainee",
                Role = UserRoles.User,
                IsActive = true
            };
            List<QuizImage> images = new List<QuizImage>(questionCount);

            for (int index = 0; index < questionCount; index++)
            {
                images.Add(new QuizImage
                {
                    ImageID = index + 1,
                    FileName = "image-" + index + ".bmp",
                    FilePath = "temporary-image-" + index + ".bmp",
                    ImageHash = index.ToString("x64")
                });
            }

            QuizEngine engine = new QuizEngine(user, images);

            for (int index = 0; index < questionCount; index++)
            {
                bool submitted = engine.TrySubmitAnswer(
                    index % 2 == 0
                        ? QuizAnswerType.Good
                        : QuizAnswerType.Ng);

                if (!submitted)
                    throw new InvalidOperationException("The quiz rejected a valid performance answer.");
            }

            return engine;
        }

        private static ReportSnapshot CreateReportSnapshot(int rowCount)
        {
            DateTime start = new DateTime(2026, 8, 1, 8, 0, 0);
            List<ReportSessionRow> rows = new List<ReportSessionRow>(rowCount);

            for (int index = 0; index < rowCount; index++)
            {
                rows.Add(new ReportSessionRow
                {
                    SessionID = index + 1,
                    EmployeeNo = "PERF-" + (index % 100).ToString("D3"),
                    FullName = "Synthetic Trainee",
                    Department = "Performance",
                    StartTime = start.AddMinutes(index),
                    EndTime = start.AddMinutes(index + 10),
                    TotalQuestions = 20,
                    ReviewedAnswers = 16,
                    PendingAnswers = 4,
                    CorrectAnswers = 12,
                    WrongAnswers = 4,
                    ReviewedAccuracy = 75m
                });
            }

            return new ReportSnapshot
            {
                Period = ReportPeriod.CreateCustomInclusive(start.Date, start.Date.AddDays(30)),
                GeneratedAtLocal = start,
                Sessions = rows,
                Summary = new ReportSummary
                {
                    SessionCount = rowCount,
                    CompletedSessionCount = rowCount,
                    TraineeCount = Math.Min(rowCount, 100),
                    TotalQuestions = rowCount * 20,
                    ReviewedAnswers = rowCount * 16,
                    PendingAnswers = rowCount * 4,
                    CorrectAnswers = rowCount * 12,
                    WrongAnswers = rowCount * 4,
                    AverageReviewedAccuracy = 75m
                }
            };
        }

        private static void Export(
            ReportExportService service,
            string extension,
            ReportSnapshot snapshot,
            string path)
        {
            if (string.Equals(extension, "csv", StringComparison.Ordinal))
                service.ExportCsv(snapshot, path, CancellationToken.None);
            else if (string.Equals(extension, "xlsx", StringComparison.Ordinal))
                service.ExportExcel(snapshot, path, CancellationToken.None);
            else
                service.ExportPdf(snapshot, path, CancellationToken.None);
        }

        private static void AssertExportReadable(
            string extension,
            string path,
            int rowCount)
        {
            Assert.That(File.Exists(path), Is.True);
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(100));

            if (string.Equals(extension, "csv", StringComparison.Ordinal))
            {
                string content = File.ReadAllText(path, Encoding.UTF8);
                Assert.That(content, Does.Contain("Session ID,Employee Number"));
                Assert.That(content, Does.Contain(rowCount.ToString()));
            }
            else if (string.Equals(extension, "xlsx", StringComparison.Ordinal))
            {
                using (ZipArchive archive = ZipFile.OpenRead(path))
                    Assert.That(archive.GetEntry("xl/workbook.xml"), Is.Not.Null);
            }
            else
            {
                byte[] bytes = File.ReadAllBytes(path);
                Assert.That(Encoding.ASCII.GetString(bytes, 0, 5), Is.EqualTo("%PDF-"));
            }
        }

        private static string ReadAllLogs(string directory)
        {
            if (!Directory.Exists(directory))
                return string.Empty;

            StringBuilder content = new StringBuilder();

            foreach (string file in Directory.GetFiles(directory, "application.log*"))
                content.Append(File.ReadAllText(file));

            return content.ToString();
        }

        private static int CountLogFiles(string directory)
        {
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, "application.log*").Length
                : 0;
        }

        #endregion
    }
}
