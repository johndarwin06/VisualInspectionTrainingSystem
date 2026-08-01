#region Namespaces

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Integration
{
    /// <summary>
    /// Verifies centralized logging with isolated file-system state and no production configuration.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    [Category(TestCategories.Integration)]
    public sealed class ApplicationErrorLoggerTests
    {
        #region Fields

        private string _rootFolder;
        private string _configuredFolder;
        private string _fallbackFolder;
        private IDisposable _loggingScope;

        #endregion

        #region Setup And Teardown

        /// <summary>
        /// Creates a unique temporary logger configuration for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _rootFolder = Path.Combine(
                Path.GetTempPath(),
                "VitsLoggingTests-" + Guid.NewGuid().ToString("N"));
            _configuredFolder = Path.Combine(_rootFolder, "configured");
            _fallbackFolder = Path.Combine(_rootFolder, "fallback");

            ReplaceConfiguration(
                _configuredFolder,
                _fallbackFolder,
                ApplicationLogLevel.Debug,
                64L * 1024L,
                3);
        }

        /// <summary>
        /// Stops the isolated writer and removes every generated log file.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            try
            {
                ApplicationErrorLogger.Flush(TimeSpan.FromSeconds(2));
            }
            finally
            {
                if (_loggingScope != null)
                {
                    _loggingScope.Dispose();
                    _loggingScope = null;
                }

                DeleteTemporaryDirectory(_rootFolder);
            }
        }

        #endregion

        #region Initialization And Levels

        /// <summary>
        /// Confirms logger initialization remains safe and configured folders are created lazily.
        /// </summary>
        [Test]
        public void InitializeAndWrite_CreatesConfiguredDirectoryWithoutUsingFallback()
        {
            Assert.That(Directory.Exists(_configuredFolder), Is.False);

            Assert.DoesNotThrow(ApplicationErrorLogger.Initialize);

            ApplicationErrorLogger.LogInformation(
                "Logger Test",
                "Configured directory selection.");

            Assert.That(
                ApplicationErrorLogger.Flush(TimeSpan.FromSeconds(2)),
                Is.True);
            Assert.That(Directory.Exists(_configuredFolder), Is.True);
            Assert.That(File.Exists(GetCurrentLogFile(_configuredFolder)), Is.True);
            Assert.That(Directory.Exists(_fallbackFolder), Is.False);
        }

        /// <summary>
        /// Confirms all supported production levels are represented in deterministic entries.
        /// </summary>
        [Test]
        public void SupportedLevels_WriteExpectedSeverityValues()
        {
            ApplicationErrorLogger.LogDebug("Levels", "debug marker");
            ApplicationErrorLogger.LogInformation("Levels", "information marker");
            ApplicationErrorLogger.LogWarning("Levels", "warning marker");
            ApplicationErrorLogger.LogError(
                "Levels",
                "error marker",
                new InvalidOperationException("safe error"));
            ApplicationErrorLogger.LogFatal(
                "Levels",
                "fatal marker",
                new ApplicationException("safe fatal"));

            string content = ReadConfiguredLog();

            Assert.Multiple(() =>
            {
                Assert.That(content, Does.Contain("Severity: Debug"));
                Assert.That(content, Does.Contain("Severity: Information"));
                Assert.That(content, Does.Contain("Severity: Warning"));
                Assert.That(content, Does.Contain("Severity: Error"));
                Assert.That(content, Does.Contain("Severity: Fatal"));
                Assert.That(content, Does.Contain("TimestampUtc:"));
                Assert.That(content, Does.Contain("EventId:"));
                Assert.That(content, Does.Contain("ThreadId:"));
            });
        }

        /// <summary>
        /// Confirms entries below the configured minimum level are discarded.
        /// </summary>
        [Test]
        public void LevelFiltering_DiscardsEntriesBelowMinimum()
        {
            ReplaceConfiguration(
                _configuredFolder,
                _fallbackFolder,
                ApplicationLogLevel.Warning,
                64L * 1024L,
                3);

            ApplicationErrorLogger.LogDebug("Filtering", "discard debug");
            ApplicationErrorLogger.LogInformation("Filtering", "discard info");
            ApplicationErrorLogger.LogWarning("Filtering", "keep warning");

            string content = ReadConfiguredLog();

            Assert.That(content, Does.Not.Contain("discard debug"));
            Assert.That(content, Does.Not.Contain("discard info"));
            Assert.That(content, Does.Contain("keep warning"));
        }

        /// <summary>
        /// Confirms files are UTF-8 without a byte-order mark.
        /// </summary>
        [Test]
        public void Output_UsesUtf8WithoutByteOrderMark()
        {
            ApplicationErrorLogger.LogInformation(
                "Encoding",
                "Unicode marker: café.");

            FlushSuccessfully();

            byte[] bytes = File.ReadAllBytes(
                GetCurrentLogFile(_configuredFolder));

            Assert.That(bytes.Length, Is.GreaterThan(3));
            Assert.That(
                bytes.Take(3).ToArray(),
                Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.That(
                Encoding.UTF8.GetString(bytes),
                Does.Contain("Unicode marker: café."));
        }

        #endregion

        #region Diagnostics And Security

        /// <summary>
        /// Confirms exception, stack, inner, and aggregate classifications remain useful but bounded.
        /// </summary>
        [Test]
        public void ExceptionOutput_PreservesSanitizedInnerAndAggregateDetails()
        {
            Exception first = CaptureException(
                "Password=TopSecret; Server=db; Database=prod; Uid=operator; Pwd=Hidden");
            Exception second = new ArgumentException(
                "token=abc123 username=private-user");
            AggregateException aggregate = CaptureAggregateException(
                "Bearer abc.def.ghi",
                first,
                second);

            ApplicationErrorLogger.LogError(
                "Exception Test",
                "An isolated failure was captured.",
                aggregate);

            string content = ReadConfiguredLog();

            Assert.Multiple(() =>
            {
                Assert.That(content, Does.Contain(typeof(AggregateException).FullName));
                Assert.That(content, Does.Contain(typeof(InvalidOperationException).FullName));
                Assert.That(content, Does.Contain(typeof(ArgumentException).FullName));
                Assert.That(content, Does.Contain("StackTrace:"));
                Assert.That(content, Does.Contain("[connection string redacted]"));
                Assert.That(content, Does.Contain("Bearer <redacted>"));
                Assert.That(content, Does.Not.Contain("TopSecret"));
                Assert.That(content, Does.Not.Contain("abc123"));
                Assert.That(content, Does.Not.Contain("private-user"));
                Assert.That(content, Does.Not.Contain("operator"));
                Assert.That(content, Does.Not.Contain(_rootFolder));
            });
        }

        /// <summary>
        /// Confirms credentials, tokens, hashes, and connection strings are removed from ordinary messages.
        /// </summary>
        [Test]
        public void Redaction_RemovesCredentialAndConfigurationSecrets()
        {
            const string bcryptHash =
                "$2b$12$abcdefghijklmnopqrstuu12345678901234567890123456789";

            ApplicationErrorLogger.LogWarning(
                "Security",
                "Password=secret Pwd:hidden token=token-value " +
                "Authorization=Bearer-value");
            ApplicationErrorLogger.LogWarning(
                "Security",
                "Server=localhost;Database=prod;Uid=user1;Pwd=db-secret; " +
                bcryptHash);

            string content = ReadConfiguredLog();

            Assert.Multiple(() =>
            {
                Assert.That(content, Does.Contain("<redacted>"));
                Assert.That(content, Does.Contain("[connection string redacted]"));
                Assert.That(content, Does.Not.Contain("secret"));
                Assert.That(content, Does.Not.Contain("hidden"));
                Assert.That(content, Does.Not.Contain("token-value"));
                Assert.That(content, Does.Not.Contain("Bearer-value"));
                Assert.That(content, Does.Not.Contain("localhost"));
                Assert.That(content, Does.Not.Contain("user1"));
                Assert.That(content, Does.Not.Contain(bcryptHash));
            });
        }

        /// <summary>
        /// Confirms the same exception instance is recorded once across overlapping handlers.
        /// </summary>
        [Test]
        public void DuplicateUnhandledException_IsWrittenExactlyOnce()
        {
            InvalidOperationException exception =
                new InvalidOperationException("deduplication marker");

            ApplicationErrorLogger.LogUnhandledException(
                "First Handler",
                exception,
                false);
            ApplicationErrorLogger.LogUnhandledException(
                "Second Handler",
                exception,
                true);

            string content = ReadConfiguredLog();

            Assert.That(
                CountOccurrences(content, "deduplication marker"),
                Is.EqualTo(1));
        }

        #endregion

        #region Concurrency And Rolling

        /// <summary>
        /// Confirms concurrent callers produce complete, non-interleaved entries.
        /// </summary>
        [Test]
        public void ConcurrentLogging_WritesEveryCompleteEntryOnce()
        {
            const int entryCount = 120;

            Parallel.For(
                0,
                entryCount,
                index => ApplicationErrorLogger.LogInformation(
                    "Concurrent Test",
                    "Concurrent marker " + index + "."));

            string content = ReadConfiguredLog();

            Assert.That(
                CountOccurrences(content, "--- Application Log Entry ---"),
                Is.EqualTo(entryCount));
            Assert.That(
                CountOccurrences(content, "--- End Application Log Entry ---"),
                Is.EqualTo(entryCount));

            for (int index = 0; index < entryCount; index++)
            {
                Assert.That(
                    CountOccurrences(
                        content,
                        "Concurrent marker " + index + "."),
                    Is.EqualTo(1));
            }
        }

        /// <summary>
        /// Confirms size rollover retains only the configured current file and backups.
        /// </summary>
        [Test]
        public void RollingFiles_EnforceSizeAndRetentionBounds()
        {
            ReplaceConfiguration(
                _configuredFolder,
                _fallbackFolder,
                ApplicationLogLevel.Debug,
                2048,
                2);

            for (int index = 0; index < 80; index++)
            {
                ApplicationErrorLogger.LogInformation(
                    "Rollover",
                    "Rollover marker " + index + " " +
                    new string('x', 180));
            }

            FlushSuccessfully();

            string[] files = Directory.GetFiles(
                _configuredFolder,
                ApplicationErrorLogger.CurrentLogFileName + "*");

            Assert.That(files.Length, Is.EqualTo(3));
            Assert.That(
                files.Select(Path.GetFileName),
                Is.EquivalentTo(new[]
                {
                    ApplicationErrorLogger.CurrentLogFileName,
                    ApplicationErrorLogger.CurrentLogFileName + ".1",
                    ApplicationErrorLogger.CurrentLogFileName + ".2"
                }));
            Assert.That(
                File.ReadAllText(GetCurrentLogFile(_configuredFolder)),
                Does.Contain("Rollover marker 79"));
        }

        #endregion

        #region Fallback And Failure Isolation

        /// <summary>
        /// Confirms an invalid configured directory falls back without escaping an exception.
        /// </summary>
        [Test]
        public void InvalidConfiguredPath_WritesToFallback()
        {
            string invalidDirectory = Path.Combine(_rootFolder, "not-a-directory");
            Directory.CreateDirectory(_rootFolder);
            File.WriteAllText(invalidDirectory, "file blocks directory creation");

            ReplaceConfiguration(
                invalidDirectory,
                _fallbackFolder,
                ApplicationLogLevel.Debug,
                64L * 1024L,
                3);

            Assert.DoesNotThrow(() =>
                ApplicationErrorLogger.LogWarning(
                    "Fallback",
                    "invalid path fallback marker"));

            Assert.That(ReadFallbackLog(), Does.Contain("invalid path fallback marker"));
        }

        /// <summary>
        /// Confirms a read-only configured file redirects later entries to the fallback.
        /// </summary>
        [Test]
        public void ReadOnlyConfiguredLog_WritesToFallback()
        {
            ApplicationErrorLogger.LogInformation(
                "Fallback",
                "create primary log");
            FlushSuccessfully();

            string primaryLog = GetCurrentLogFile(_configuredFolder);
            File.SetAttributes(primaryLog, FileAttributes.ReadOnly);

            try
            {
                ApplicationErrorLogger.LogWarning(
                    "Fallback",
                    "read-only fallback marker");

                Assert.That(
                    ReadFallbackLog(),
                    Does.Contain("read-only fallback marker"));
            }
            finally
            {
                File.SetAttributes(primaryLog, FileAttributes.Normal);
            }
        }

        /// <summary>
        /// Confirms a temporary exclusive lock redirects the affected entry to fallback.
        /// </summary>
        [Test]
        public void LockedConfiguredLog_WritesToFallback()
        {
            ApplicationErrorLogger.LogInformation(
                "Fallback",
                "create lock target");
            FlushSuccessfully();

            using (FileStream locked = new FileStream(
                GetCurrentLogFile(_configuredFolder),
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                ApplicationErrorLogger.LogWarning(
                    "Fallback",
                    "locked fallback marker");

                Assert.That(
                    ReadFallbackLog(),
                    Does.Contain("locked fallback marker"));
            }
        }

        /// <summary>
        /// Confirms complete primary and fallback failure cannot recurse or escape.
        /// </summary>
        [Test]
        public void TotalWriterFailure_DoesNotThrowOrRecurse()
        {
            string blockedPath = Path.Combine(_rootFolder, "blocked");
            Directory.CreateDirectory(_rootFolder);
            File.WriteAllText(blockedPath, "not a directory");

            ReplaceConfiguration(
                blockedPath,
                blockedPath,
                ApplicationLogLevel.Debug,
                64L * 1024L,
                3);

            Assert.DoesNotThrow(() =>
            {
                for (int index = 0; index < 20; index++)
                {
                    ApplicationErrorLogger.LogError(
                        "Failure Isolation",
                        "writer failure marker",
                        new IOException("simulated sink failure"));
                }

                ApplicationErrorLogger.Flush(TimeSpan.FromSeconds(2));
            });

            Assert.That(File.ReadAllText(blockedPath), Is.EqualTo("not a directory"));
        }

        #endregion

        #region Flush And Global Integration

        /// <summary>
        /// Confirms bounded shutdown writes entries queued before shutdown and returns promptly.
        /// </summary>
        [Test]
        public void Shutdown_FlushesQueuedEntryWithinBound()
        {
            ApplicationErrorLogger.LogInformation(
                "Shutdown",
                "bounded shutdown marker");

            DateTime started = DateTime.UtcNow;
            bool stopped = ApplicationErrorLogger.Shutdown(
                TimeSpan.FromSeconds(2));
            TimeSpan elapsed = DateTime.UtcNow - started;

            Assert.That(stopped, Is.True);
            Assert.That(elapsed, Is.LessThan(TimeSpan.FromSeconds(2.5)));
            Assert.That(
                File.ReadAllText(GetCurrentLogFile(_configuredFolder)),
                Does.Contain("bounded shutdown marker"));
        }

        /// <summary>
        /// Invokes the real TaskScheduler global-handler method and verifies observation plus logging.
        /// </summary>
        [Test]
        public void TaskSchedulerHandler_LogsSanitizedExceptionAndMarksObserved()
        {
            object application = FormatterServices.GetUninitializedObject(
                typeof(App));
            MethodInfo handler = typeof(App).GetMethod(
                "TaskScheduler_UnobservedTaskException",
                BindingFlags.Instance | BindingFlags.NonPublic);
            UnobservedTaskExceptionEventArgs arguments =
                new UnobservedTaskExceptionEventArgs(
                    new AggregateException(
                        new InvalidOperationException(
                            "Password=unobserved-secret")));

            Assert.That(handler, Is.Not.Null);

            handler.Invoke(
                application,
                new object[] { null, arguments });

            string content = ReadConfiguredLog();

            Assert.That(arguments.Observed, Is.True);
            Assert.That(content, Does.Contain("Source: Task Scheduler"));
            Assert.That(content, Does.Contain("Severity: Error"));
            Assert.That(content, Does.Not.Contain("unobserved-secret"));
        }

        /// <summary>
        /// Confirms production global handlers retain logging, bounded flush, observation, and safe dialog contracts.
        /// </summary>
        [Test]
        public void GlobalHandlerSource_PreservesSafeIntegrationContracts()
        {
            string source = File.ReadAllText(
                FindRepositoryFile("App.xaml.cs"));

            Assert.Multiple(() =>
            {
                Assert.That(source, Does.Contain("ApplicationErrorLogger.Initialize()"));
                Assert.That(source, Does.Contain("ApplicationErrorLogger.LogUnhandledException("));
                Assert.That(source, Does.Contain("ApplicationErrorLogger.Flush(FatalLogFlushTimeout)"));
                Assert.That(source, Does.Contain("e.SetObserved()"));
                Assert.That(source, Does.Contain("ShowFatalErrorMessage()"));
                Assert.That(source, Does.Contain("MarkDispatcherExceptionHandled(e)"));
            });
        }

        /// <summary>
        /// Guards the required production logging boundaries without invoking production services or data.
        /// </summary>
        [Test]
        public void ProductionSources_RetainCentralizedLoggingIntegration()
        {
            string logger = ReadRepositorySource(
                "Services",
                "ApplicationErrorLogger.cs");
            string startup = ReadRepositorySource(
                "Services",
                "SystemInitializerService.cs");
            string configuration = ReadRepositorySource(
                "Services",
                "ConfigurationService.cs");
            string authentication = ReadRepositorySource(
                "Services",
                "AuthenticationService.cs");
            string registration = ReadRepositorySource(
                "Services",
                "RegistrationService.cs");
            string userManagement = ReadRepositorySource(
                "Services",
                "UserManagementService.cs");
            string dashboard = ReadRepositorySource(
                "ViewModels",
                "DashboardViewModel.cs");
            string reports = ReadRepositorySource(
                "ViewModels",
                "ReportsViewModel.cs");
            string review = ReadRepositorySource(
                "ViewModels",
                "AdminViewModel.cs");
            string quiz = ReadRepositorySource(
                "ViewModels",
                "QuizViewModel.cs");
            string result = ReadRepositorySource(
                "ViewModels",
                "ResultViewModel.cs");
            string history = ReadRepositorySource(
                "ViewModels",
                "TrainingHistoryViewModel.cs");

            Assert.Multiple(() =>
            {
                Assert.That(logger, Does.Contain("SafeRollingFileAppender"));
                Assert.That(logger, Does.Contain("ApplicationLogLevel.Fatal"));
                Assert.That(startup, Does.Contain("ConfigureLogFolder("));
                Assert.That(startup, Does.Contain("Application initialization completed successfully"));
                Assert.That(configuration, Does.Contain("TryPrepareOptionalLogDirectory"));
                Assert.That(authentication, Does.Contain("An authentication attempt was rejected."));
                Assert.That(registration, Does.Contain("inactive trainee registration"));
                Assert.That(userManagement, Does.Contain("authorization boundary"));
                Assert.That(dashboard, Does.Contain("ApplicationErrorLogger.LogUnhandledException("));
                Assert.That(reports, Does.Contain("report export completed"));
                Assert.That(review, Does.Contain("ApplicationErrorLogger.LogUnhandledException("));
                Assert.That(quiz, Does.Contain("completed training session was saved"));
                Assert.That(result, Does.Contain("Result Image Preview"));
                Assert.That(result, Does.Contain("ApplicationErrorLogger.LogWarning("));
                Assert.That(history, Does.Contain("ApplicationErrorLogger.LogUnhandledException("));
            });
        }

        #endregion

        #region Helpers

        private void ReplaceConfiguration(
            string configuredFolder,
            string fallbackFolder,
            ApplicationLogLevel minimumLevel,
            long maximumFileSizeBytes,
            int retainedFileCount)
        {
            if (_loggingScope != null)
            {
                _loggingScope.Dispose();
            }

            _loggingScope = ApplicationErrorLogger.UseConfigurationForTesting(
                configuredFolder,
                fallbackFolder,
                minimumLevel,
                maximumFileSizeBytes,
                retainedFileCount);
        }

        private string ReadConfiguredLog()
        {
            FlushSuccessfully();
            return ReadAllLogFiles(_configuredFolder);
        }

        private string ReadFallbackLog()
        {
            FlushSuccessfully();
            return ReadAllLogFiles(_fallbackFolder);
        }

        private static string ReadAllLogFiles(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return string.Empty;
            }

            StringBuilder content = new StringBuilder();

            foreach (string file in Directory
                .GetFiles(
                    directory,
                    ApplicationErrorLogger.CurrentLogFileName + "*")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                content.Append(File.ReadAllText(file));
            }

            return content.ToString();
        }

        private static string GetCurrentLogFile(string directory)
        {
            return Path.Combine(
                directory,
                ApplicationErrorLogger.CurrentLogFileName);
        }

        private static Exception CaptureException(string message)
        {
            try
            {
                throw new InvalidOperationException(
                    message,
                    new IOException("username=inner-user"));
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static AggregateException CaptureAggregateException(
            string message,
            params Exception[] exceptions)
        {
            try
            {
                throw new AggregateException(message, exceptions);
            }
            catch (AggregateException exception)
            {
                return exception;
            }
        }

        private static int CountOccurrences(
            string value,
            string marker)
        {
            int count = 0;
            int index = 0;

            while ((index = value.IndexOf(
                marker,
                index,
                StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += marker.Length;
            }

            return count;
        }

        private static void FlushSuccessfully()
        {
            Assert.That(
                ApplicationErrorLogger.Flush(TimeSpan.FromSeconds(2)),
                Is.True,
                "The isolated logger did not process its bounded flush marker.");
        }

        private static string FindRepositoryFile(string relativePath)
        {
            DirectoryInfo directory = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(
                    directory.FullName,
                    relativePath);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException(
                "Repository file was not found for the source-contract test.",
                relativePath);
        }

        private static string ReadRepositorySource(
            string directory,
            string fileName)
        {
            return File.ReadAllText(
                FindRepositoryFile(
                    Path.Combine(directory, fileName)));
        }

        private static void DeleteTemporaryDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) ||
                !Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(
                directory,
                "*",
                SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(directory, true);
        }

        #endregion
    }
}
