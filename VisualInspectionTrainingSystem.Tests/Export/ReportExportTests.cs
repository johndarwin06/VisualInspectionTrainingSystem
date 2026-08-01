#region Namespaces

using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Export
{
    /// <summary>
    /// Validates real CSV, Open XML, and PDF output plus cancellation cleanup.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Export)]
    [NonParallelizable]
    public sealed class ReportExportTests
    {
        #region Fields

        private string _temporaryDirectory;

        #endregion

        #region Lifecycle

        /// <summary>Creates an isolated export directory for one test.</summary>
        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "VITS-I17-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        /// <summary>Removes every generated export even when an assertion fails.</summary>
        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_temporaryDirectory) &&
                Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        #endregion

        #region CSV Tests

        /// <summary>Confirms CSV contains metadata, safe quoting, nullable accuracy, and every row.</summary>
        [Test]
        public void CsvExport_WritesStructuredValidatedContent()
        {
            // Arrange
            string path = Path.Combine(_temporaryDirectory, "report.csv");
            ReportSnapshot snapshot = CreateSnapshot();

            // Act
            new ReportExportService().ExportCsv(
                snapshot,
                path,
                CancellationToken.None);
            string content = File.ReadAllText(path, Encoding.UTF8);

            // Assert
            Assert.That(File.Exists(path), Is.True);
            Assert.That(content, Does.Contain("Visual Inspection Training Report"));
            Assert.That(content, Does.Contain("Session ID,Employee Number"));
            Assert.That(content, Does.Contain("\"Trainee, One\""));
            Assert.That(content, Does.Contain("N/A"));
            Assert.That(content, Does.Contain("50.00%"));
            Assert.That(content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length,
                Is.GreaterThan(10));
        }

        #endregion

        #region Excel Tests

        /// <summary>Confirms XLSX is a valid Open Packaging Convention archive with three sheets.</summary>
        [Test]
        public void ExcelExport_WritesValidOpenXmlPackage()
        {
            // Arrange
            string path = Path.Combine(_temporaryDirectory, "report.xlsx");

            // Act
            new ReportExportService().ExportExcel(
                CreateSnapshot(),
                path,
                CancellationToken.None);

            // Assert
            Assert.That(File.Exists(path), Is.True);
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(1000));

            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                Assert.That(archive.GetEntry("[Content_Types].xml"), Is.Not.Null);
                Assert.That(archive.GetEntry("xl/workbook.xml"), Is.Not.Null);
                Assert.That(
                    archive.Entries.Count(entry =>
                        entry.FullName.StartsWith(
                            "xl/worksheets/sheet",
                            StringComparison.OrdinalIgnoreCase)),
                    Is.EqualTo(3));
            }
        }

        #endregion

        #region PDF Tests

        /// <summary>Confirms PDF output has a real header, trailer, and non-empty layout.</summary>
        [Test]
        public void PdfExport_WritesValidPdfEnvelope()
        {
            // Arrange
            string path = Path.Combine(_temporaryDirectory, "report.pdf");

            // Act
            new ReportExportService().ExportPdf(
                CreateSnapshot(),
                path,
                CancellationToken.None);
            byte[] bytes = File.ReadAllBytes(path);
            string prefix = Encoding.ASCII.GetString(bytes, 0, 5);
            string suffix = Encoding.ASCII.GetString(
                bytes,
                Math.Max(0, bytes.Length - 32),
                Math.Min(32, bytes.Length));

            // Assert
            Assert.That(prefix, Is.EqualTo("%PDF-"));
            Assert.That(suffix, Does.Contain("%%EOF"));
            Assert.That(bytes.Length, Is.GreaterThan(1000));
        }

        #endregion

        #region Safeguard Tests

        /// <summary>Confirms a pre-cancelled export leaves no destination or temporary file.</summary>
        [TestCase("csv")]
        [TestCase("xlsx")]
        [TestCase("pdf")]
        public void Export_PreCancelledOperationLeavesNoArtifacts(string extension)
        {
            // Arrange
            string path = Path.Combine(
                _temporaryDirectory,
                "cancelled." + extension);
            CancellationTokenSource source = new CancellationTokenSource();
            source.Cancel();

            try
            {
                // Act and Assert
                Assert.That(
                    () => ExportByExtension(
                        extension,
                        CreateSnapshot(),
                        path,
                        source.Token),
                    Throws.TypeOf<OperationCanceledException>());
                Assert.That(File.Exists(path), Is.False);
                Assert.That(Directory.GetFiles(_temporaryDirectory), Is.Empty);
            }
            finally
            {
                source.Dispose();
            }
        }

        /// <summary>Confirms the repository's export safeguard cannot be bypassed by a writer.</summary>
        [Test]
        public void Export_LimitExceededSnapshotIsRejectedWithoutOutput()
        {
            // Arrange
            string path = Path.Combine(_temporaryDirectory, "blocked.csv");
            ReportSnapshot snapshot = CreateSnapshot();
            snapshot.IsExportLimitExceeded = true;

            // Act and Assert
            Assert.That(
                () => new ReportExportService().ExportCsv(
                    snapshot,
                    path,
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(File.Exists(path), Is.False);
        }

        #endregion

        #region Fixtures

        private static void ExportByExtension(
            string extension,
            ReportSnapshot snapshot,
            string path,
            CancellationToken cancellationToken)
        {
            ReportExportService service = new ReportExportService();

            if (string.Equals(extension, "csv", StringComparison.Ordinal))
            {
                service.ExportCsv(snapshot, path, cancellationToken);
            }
            else if (string.Equals(extension, "xlsx", StringComparison.Ordinal))
            {
                service.ExportExcel(snapshot, path, cancellationToken);
            }
            else
            {
                service.ExportPdf(snapshot, path, cancellationToken);
            }
        }

        private static ReportSnapshot CreateSnapshot()
        {
            return new ReportSnapshot
            {
                Period = ReportPeriod.CreateDaily(new DateTime(2026, 8, 1)),
                GeneratedAtLocal = new DateTime(2026, 8, 1, 12, 0, 0),
                Summary = new ReportSummary
                {
                    SessionCount = 2,
                    CompletedSessionCount = 1,
                    OpenSessionCount = 1,
                    TraineeCount = 1,
                    TotalQuestions = 6,
                    ReviewedAnswers = 4,
                    PendingAnswers = 2,
                    CorrectAnswers = 2,
                    WrongAnswers = 2,
                    AverageReviewedAccuracy = 50m
                },
                Sessions =
                {
                    new ReportSessionRow
                    {
                        SessionID = 101,
                        EmployeeNo = "I17-1",
                        FullName = "Trainee, One",
                        Department = "Quality",
                        StartTime = new DateTime(2026, 8, 1, 9, 0, 0),
                        EndTime = new DateTime(2026, 8, 1, 9, 10, 0),
                        TotalQuestions = 6,
                        ReviewedAnswers = 4,
                        PendingAnswers = 2,
                        CorrectAnswers = 2,
                        WrongAnswers = 2,
                        ReviewedAccuracy = 50m
                    },
                    new ReportSessionRow
                    {
                        SessionID = 100,
                        EmployeeNo = "I17-1",
                        FullName = "Trainee, One",
                        Department = "Quality",
                        StartTime = new DateTime(2026, 8, 1, 8, 0, 0),
                        EndTime = null,
                        TotalQuestions = 0,
                        ReviewedAccuracy = null
                    }
                }
            };
        }

        #endregion
    }
}
