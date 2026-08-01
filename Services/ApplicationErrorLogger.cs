#region Namespaces

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Defines the application logging levels supported by the central logger.
    /// </summary>
    internal enum ApplicationLogLevel
    {
        Debug = 0,
        Information = 1,
        Warning = 2,
        Error = 3,
        Fatal = 4
    }

    /// <summary>
    /// Provides centralized, bounded, sanitized logging without allowing logger failures to affect the application.
    /// </summary>
    internal static class ApplicationErrorLogger
    {
        #region Constants

        private const string ApplicationFolderName =
            "VisualInspectionTrainingSystem";

        private const string LogFolderName = "Logs";

        internal const string CurrentLogFileName = "application.log";

        private const int DefaultQueueCapacity = 2048;

        private const int DefaultRetainedFileCount = 5;

        private const long DefaultMaximumFileSizeBytes = 5L * 1024L * 1024L;

        private const int MaximumMessageLength = 1024;

        private const int MaximumStackTraceLength = 8192;

        private const int MaximumSourceLength = 128;

        private const int MaximumThreadNameLength = 64;

        private const int MaximumInnerExceptions = 5;

        private static readonly TimeSpan DefaultFlushTimeout =
            TimeSpan.FromSeconds(1);

        #endregion

        #region Fields

        private static readonly object SyncRoot = new object();

        private static readonly ILog Logger =
            LogManager.GetLogger(typeof(ApplicationErrorLogger));

        private static readonly Hierarchy LoggingHierarchy =
            (Hierarchy)LogManager.GetRepository(
                Assembly.GetExecutingAssembly());

        private static readonly Regex ConnectionStringPattern =
            new Regex(
                "(?i)(?:server|host|data\\s+source|database|initial\\s+catalog|port|uid|user\\s*id|username|password|pwd)\\s*=\\s*[^;\\r\\n]*(?:\\s*;\\s*(?:server|host|data\\s+source|database|initial\\s+catalog|port|uid|user\\s*id|username|password|pwd)\\s*=\\s*[^;\\r\\n]*)+",
                RegexOptions.Compiled);

        private static readonly Regex SensitiveValuePattern =
            new Regex(
                "(?i)\\b(password|password\\s+hash|pwd|user\\s*id|uid|username|user|token|access[_ -]?token|refresh[_ -]?token|secret|api[_ -]?key|authorization)\\s*([=:])\\s*(?:\\\"[^\\\"]*\\\"|'[^']*'|[^;,\\s\\r\\n]+)",
                RegexOptions.Compiled);

        private static readonly Regex BearerTokenPattern =
            new Regex(
                "(?i)\\bBearer\\s+[A-Za-z0-9._~+/-]+=*",
                RegexOptions.Compiled);

        private static readonly Regex PasswordHashPattern =
            new Regex(
                "\\$2[aby]\\$\\d{2}\\$[./A-Za-z0-9]{40,80}",
                RegexOptions.Compiled);

        private static readonly Regex SqlParameterValuePattern =
            new Regex(
                "(?i)(@[A-Z_][A-Z0-9_]*)\\s*([=:])\\s*(?:\\\"[^\\\"]*\\\"|'[^']*'|[^;,\\s\\r\\n]+)",
                RegexOptions.Compiled);

        private static readonly Regex WindowsPathPattern =
            new Regex(
                "(?i)(?:[A-Z]:\\\\|\\\\\\\\)[^\\r\\n\\t]*",
                RegexOptions.Compiled);

        private static ConditionalWeakTable<Exception, object>
            _handledExceptions =
                new ConditionalWeakTable<Exception, object>();

        private static SafeRollingFileAppender _appender;

        private static ApplicationLogLevel _minimumLevel;

        private static string _configuredLogFolder;

        private static string _fallbackLogFolder;

        private static long _maximumFileSizeBytes;

        private static int _retainedFileCount;

        private static bool _shutdown;

        #endregion

        #region Constructor

        /// <summary>
        /// Leaves file output disabled until the real application lifecycle or an isolated test initializes it.
        /// </summary>
        static ApplicationErrorLogger()
        {
            _shutdown = true;
        }

        #endregion

        #region Initialization And Lifecycle

        /// <summary>
        /// Ensures the central logger is ready before application configuration has loaded.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                lock (SyncRoot)
                {
                    if (_appender == null || _shutdown)
                    {
                        ConfigureCoreLocked(
                            null,
                            GetFallbackLogDirectory(),
                            ApplicationLogLevel.Information,
                            DefaultMaximumFileSizeBytes,
                            DefaultRetainedFileCount);
                    }
                }
            }
            catch
            {
                // Logger initialization must never prevent application startup.
            }
        }

        /// <summary>
        /// Uses the configured log folder after configuration has loaded successfully.
        /// </summary>
        /// <param name="logFolder">The configured application log folder.</param>
        public static void ConfigureLogFolder(string logFolder)
        {
            try
            {
                ConfigureCore(
                    NormalizeDirectory(logFolder),
                    GetFallbackLogDirectory(),
                    ApplicationLogLevel.Information,
                    DefaultMaximumFileSizeBytes,
                    DefaultRetainedFileCount);
            }
            catch
            {
                // Invalid or unavailable configured paths remain a fallback-only condition.
            }
        }

        /// <summary>
        /// Flushes queued entries within a bounded wait.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns><c>true</c> when all entries queued before the call were processed.</returns>
        public static bool Flush(TimeSpan timeout)
        {
            try
            {
                SafeRollingFileAppender appender;

                lock (SyncRoot)
                {
                    appender = _appender;
                }

                return appender == null || appender.Flush(timeout);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Flushes and stops the logger without exceeding the supplied shutdown wait.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns><c>true</c> when the writer stopped normally.</returns>
        public static bool Shutdown(TimeSpan timeout)
        {
            SafeRollingFileAppender appender = null;

            try
            {
                lock (SyncRoot)
                {
                    if (_shutdown)
                    {
                        return true;
                    }

                    _shutdown = true;
                    appender = _appender;
                    _appender = null;

                    if (appender != null)
                    {
                        LoggingHierarchy.Root.RemoveAppender(appender);
                    }
                }

                return appender == null || appender.Stop(timeout);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Level Methods

        /// <summary>
        /// Records a low-volume diagnostic message when Debug logging is enabled.
        /// </summary>
        public static void LogDebug(string source, string message)
        {
            Log(ApplicationLogLevel.Debug, source, message, null, false);
        }

        /// <summary>
        /// Records a normal application lifecycle or operation event.
        /// </summary>
        public static void LogInformation(string source, string message)
        {
            Log(ApplicationLogLevel.Information, source, message, null, false);
        }

        /// <summary>
        /// Records a recoverable or security-relevant warning.
        /// </summary>
        public static void LogWarning(string source, string message)
        {
            Log(ApplicationLogLevel.Warning, source, message, null, false);
        }

        /// <summary>
        /// Records a recoverable warning with sanitized exception details.
        /// </summary>
        public static void LogWarning(
            string source,
            string message,
            Exception exception)
        {
            Log(
                ApplicationLogLevel.Warning,
                source,
                message,
                exception,
                false);
        }

        /// <summary>
        /// Records a recoverable error using sanitized exception details.
        /// </summary>
        public static void LogError(
            string source,
            string message,
            Exception exception)
        {
            Log(
                ApplicationLogLevel.Error,
                source,
                message,
                exception,
                false);
        }

        /// <summary>
        /// Records a fatal error using sanitized exception details.
        /// </summary>
        public static void LogFatal(
            string source,
            string message,
            Exception exception)
        {
            Log(
                ApplicationLogLevel.Fatal,
                source,
                message,
                exception,
                true);
        }

        /// <summary>
        /// Records an unhandled error that is not expected to terminate the process.
        /// </summary>
        public static void LogUnhandledException(
            string source,
            Exception exception)
        {
            LogUnhandledException(source, exception, false);
        }

        /// <summary>
        /// Records an unhandled error once, preserving the existing public logging API.
        /// </summary>
        public static void LogUnhandledException(
            string source,
            Exception exception,
            bool terminationExpected)
        {
            try
            {
                if (!TryMarkUnhandledException(exception))
                {
                    return;
                }

                Log(
                    terminationExpected
                        ? ApplicationLogLevel.Fatal
                        : ApplicationLogLevel.Error,
                    source,
                    "An unexpected application error was observed.",
                    exception,
                    terminationExpected);
            }
            catch
            {
                // Global exception handlers must never throw while reporting a failure.
            }
        }

        #endregion

        #region Entry Building

        /// <summary>
        /// Writes one sanitized entry through the configured provider.
        /// </summary>
        private static void Log(
            ApplicationLogLevel level,
            string source,
            string message,
            Exception exception,
            bool terminationExpected)
        {
            try
            {
                lock (SyncRoot)
                {
                    if (_shutdown ||
                        _appender == null ||
                        level < _minimumLevel)
                    {
                        return;
                    }
                }

                string entry = BuildEntry(
                    level,
                    source,
                    message,
                    exception,
                    terminationExpected);

                Logger.Logger.Log(
                    typeof(ApplicationErrorLogger),
                    GetProviderLevel(level),
                    entry,
                    null);
            }
            catch
            {
                // Logging failures are deliberately isolated from application behavior.
            }
        }

        /// <summary>
        /// Builds a deterministic entry with bounded diagnostic detail.
        /// </summary>
        private static string BuildEntry(
            ApplicationLogLevel level,
            string source,
            string message,
            Exception exception,
            bool terminationExpected)
        {
            StringBuilder entry = new StringBuilder();

            entry.AppendLine("--- Application Log Entry ---");
            AppendField(
                entry,
                "TimestampUtc",
                DateTime.UtcNow.ToString(
                    "o",
                    CultureInfo.InvariantCulture));
            AppendField(entry, "EventId", Guid.NewGuid().ToString("N"));
            AppendField(entry, "Severity", level.ToString());
            AppendField(entry, "Source", SanitizeSource(source));
            AppendField(
                entry,
                "ThreadId",
                Thread.CurrentThread.ManagedThreadId.ToString(
                    CultureInfo.InvariantCulture));
            AppendField(
                entry,
                "ThreadName",
                SanitizeAndLimit(
                    string.IsNullOrWhiteSpace(Thread.CurrentThread.Name)
                        ? "Unnamed"
                        : Thread.CurrentThread.Name,
                    MaximumThreadNameLength));
            AppendField(
                entry,
                "Message",
                SanitizeAndLimit(message, MaximumMessageLength));
            AppendField(
                entry,
                "TerminationExpected",
                terminationExpected ? "True" : "False");

            if (exception != null)
            {
                AppendField(
                    entry,
                    "ExceptionType",
                    GetExceptionType(exception));
                AppendField(
                    entry,
                    "ExceptionMessage",
                    SanitizeAndLimit(
                        exception.Message,
                        MaximumMessageLength));
                AppendStackTrace(entry, exception);
                AppendInnerExceptions(entry, exception);
                AppendAggregateExceptionTypes(entry, exception);
            }

            entry.AppendLine("--- End Application Log Entry ---");

            return entry.ToString();
        }

        /// <summary>
        /// Adds a labeled value to the current entry.
        /// </summary>
        private static void AppendField(
            StringBuilder entry,
            string name,
            string value)
        {
            entry.Append(name);
            entry.Append(": ");
            entry.AppendLine(value ?? string.Empty);
        }

        /// <summary>
        /// Appends a bounded, sanitized stack trace when available.
        /// </summary>
        private static void AppendStackTrace(
            StringBuilder entry,
            Exception exception)
        {
            if (exception == null ||
                string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                return;
            }

            entry.AppendLine("StackTrace:");
            entry.AppendLine(
                SanitizeAndLimit(
                    exception.StackTrace,
                    MaximumStackTraceLength));
        }

        /// <summary>
        /// Appends a bounded inner-exception chain.
        /// </summary>
        private static void AppendInnerExceptions(
            StringBuilder entry,
            Exception exception)
        {
            if (exception == null || exception.InnerException == null)
            {
                return;
            }

            entry.AppendLine("InnerExceptions:");

            Exception current = exception.InnerException;
            int index = 0;

            while (current != null && index < MaximumInnerExceptions)
            {
                index++;
                entry.Append("  ");
                entry.Append(index.ToString(CultureInfo.InvariantCulture));
                entry.Append(". ");
                entry.Append(GetExceptionType(current));
                entry.Append(": ");
                entry.AppendLine(
                    SanitizeAndLimit(
                        current.Message,
                        MaximumMessageLength));

                current = current.InnerException;
            }

            if (current != null)
            {
                entry.AppendLine("  Additional inner exceptions omitted.");
            }
        }

        /// <summary>
        /// Appends flattened AggregateException type names without serializing raw exception objects.
        /// </summary>
        private static void AppendAggregateExceptionTypes(
            StringBuilder entry,
            Exception exception)
        {
            AggregateException aggregateException =
                exception as AggregateException;

            if (aggregateException == null)
            {
                return;
            }

            IList<Exception> flattenedExceptions;

            try
            {
                flattenedExceptions = aggregateException
                    .Flatten()
                    .InnerExceptions;
            }
            catch
            {
                return;
            }

            entry.AppendLine("AggregateInnerExceptionTypes:");

            int count = Math.Min(
                flattenedExceptions.Count,
                MaximumInnerExceptions);

            for (int index = 0; index < count; index++)
            {
                entry.Append("  ");
                entry.Append((index + 1).ToString(CultureInfo.InvariantCulture));
                entry.Append(". ");
                entry.AppendLine(GetExceptionType(flattenedExceptions[index]));
            }

            if (flattenedExceptions.Count > count)
            {
                entry.AppendLine("  Additional aggregate exceptions omitted.");
            }
        }

        #endregion

        #region Sanitization

        /// <summary>
        /// Marks an unhandled exception instance so overlapping global handlers do not duplicate it.
        /// </summary>
        private static bool TryMarkUnhandledException(Exception exception)
        {
            if (exception == null)
            {
                return true;
            }

            lock (SyncRoot)
            {
                object ignored;

                if (_handledExceptions.TryGetValue(exception, out ignored))
                {
                    return false;
                }

                _handledExceptions.Add(exception, new object());

                return true;
            }
        }

        /// <summary>
        /// Returns a safe exception type name.
        /// </summary>
        private static string GetExceptionType(Exception exception)
        {
            return exception == null
                ? "Unknown"
                : exception.GetType().FullName;
        }

        /// <summary>
        /// Restricts a source label to a concise single-line value.
        /// </summary>
        private static string SanitizeSource(string source)
        {
            return string.IsNullOrWhiteSpace(source)
                ? "Unknown"
                : SanitizeAndLimit(source, MaximumSourceLength);
        }

        /// <summary>
        /// Removes credential-like values, password hashes, and full local paths from diagnostic text.
        /// </summary>
        private static string SanitizeAndLimit(
            string value,
            int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Not available.";
            }

            string sanitized = value;

            try
            {
                sanitized = ConnectionStringPattern.Replace(
                    sanitized,
                    "[connection string redacted]");
                sanitized = SensitiveValuePattern.Replace(
                    sanitized,
                    "$1$2<redacted>");
                sanitized = BearerTokenPattern.Replace(
                    sanitized,
                    "Bearer <redacted>");
                sanitized = PasswordHashPattern.Replace(
                    sanitized,
                    "<password hash redacted>");
                sanitized = SqlParameterValuePattern.Replace(
                    sanitized,
                    "$1$2<redacted>");
                sanitized = WindowsPathPattern.Replace(
                    sanitized,
                    "[path redacted]");
            }
            catch
            {
                sanitized = "Diagnostic text could not be sanitized.";
            }

            sanitized = sanitized
                .Replace("\0", string.Empty)
                .Trim();

            if (sanitized.Length <= maximumLength)
            {
                return sanitized;
            }

            return sanitized.Substring(0, maximumLength) +
                   " [truncated]";
        }

        #endregion

        #region Provider Configuration

        /// <summary>
        /// Configures the single provider and safely retires the previous writer.
        /// </summary>
        private static void ConfigureCore(
            string configuredLogFolder,
            string fallbackLogFolder,
            ApplicationLogLevel minimumLevel,
            long maximumFileSizeBytes,
            int retainedFileCount)
        {
            SafeRollingFileAppender previousAppender;

            lock (SyncRoot)
            {
                previousAppender = _appender;

                ConfigureCoreLocked(
                    configuredLogFolder,
                    fallbackLogFolder,
                    minimumLevel,
                    maximumFileSizeBytes,
                    retainedFileCount);
            }

            if (previousAppender != null)
            {
                previousAppender.Stop(DefaultFlushTimeout);
            }
        }

        /// <summary>
        /// Applies provider configuration while the central lock is held.
        /// </summary>
        private static void ConfigureCoreLocked(
            string configuredLogFolder,
            string fallbackLogFolder,
            ApplicationLogLevel minimumLevel,
            long maximumFileSizeBytes,
            int retainedFileCount)
        {
            if (_appender != null)
            {
                LoggingHierarchy.Root.RemoveAppender(_appender);
            }

            _configuredLogFolder = NormalizeDirectory(configuredLogFolder);
            _fallbackLogFolder = NormalizeDirectory(fallbackLogFolder);
            _minimumLevel = minimumLevel;
            _maximumFileSizeBytes = Math.Max(1024L, maximumFileSizeBytes);
            _retainedFileCount = Math.Max(1, retainedFileCount);
            _shutdown = false;

            PatternLayout layout = new PatternLayout("%message%newline");
            layout.ActivateOptions();

            SafeRollingFileAppender appender =
                new SafeRollingFileAppender(
                    _configuredLogFolder,
                    _fallbackLogFolder,
                    _maximumFileSizeBytes,
                    _retainedFileCount,
                    DefaultQueueCapacity)
                {
                    Name = "VisualInspectionTrainingSystemSafeRollingFile",
                    Layout = layout,
                    Threshold = Level.Debug
                };

            appender.ActivateOptions();

            LoggingHierarchy.Root.Level = Level.Debug;
            LoggingHierarchy.Root.AddAppender(appender);
            LoggingHierarchy.Configured = true;

            _appender = appender;
        }

        /// <summary>
        /// Maps an application level to the provider level.
        /// </summary>
        private static Level GetProviderLevel(ApplicationLogLevel level)
        {
            switch (level)
            {
                case ApplicationLogLevel.Debug:
                    return Level.Debug;

                case ApplicationLogLevel.Information:
                    return Level.Info;

                case ApplicationLogLevel.Warning:
                    return Level.Warn;

                case ApplicationLogLevel.Fatal:
                    return Level.Fatal;

                default:
                    return Level.Error;
            }
        }

        /// <summary>
        /// Returns a trimmed directory without touching the file system.
        /// </summary>
        private static string NormalizeDirectory(string directory)
        {
            return string.IsNullOrWhiteSpace(directory)
                ? null
                : directory.Trim();
        }

        /// <summary>
        /// Gets the configuration-independent fallback directory.
        /// </summary>
        internal static string GetFallbackLogDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                ApplicationFolderName,
                LogFolderName);
        }

        #endregion

        #region Test Support

        /// <summary>
        /// Installs an isolated logger configuration for permanent regression tests.
        /// </summary>
        internal static IDisposable UseConfigurationForTesting(
            string configuredLogFolder,
            string fallbackLogFolder,
            ApplicationLogLevel minimumLevel,
            long maximumFileSizeBytes,
            int retainedFileCount)
        {
            ConfigureCore(
                configuredLogFolder,
                fallbackLogFolder,
                minimumLevel,
                maximumFileSizeBytes,
                retainedFileCount);

            lock (SyncRoot)
            {
                _handledExceptions =
                    new ConditionalWeakTable<Exception, object>();
            }

            return new LoggerTestScope();
        }

        /// <summary>
        /// Restores a fallback-only configuration after an isolated test.
        /// </summary>
        private sealed class LoggerTestScope : IDisposable
        {
            private int _disposed;

            /// <summary>
            /// Restores production-safe fallback defaults once.
            /// </summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                DisableForTesting();

                lock (SyncRoot)
                {
                    _handledExceptions =
                        new ConditionalWeakTable<Exception, object>();
                }
            }
        }

        /// <summary>
        /// Stops an isolated test writer and leaves logging disabled until explicitly initialized again.
        /// </summary>
        private static void DisableForTesting()
        {
            SafeRollingFileAppender appender;

            lock (SyncRoot)
            {
                appender = _appender;
                _appender = null;
                _shutdown = true;

                if (appender != null)
                {
                    LoggingHierarchy.Root.RemoveAppender(appender);
                }
            }

            if (appender != null)
            {
                appender.Stop(DefaultFlushTimeout);
            }
        }

        #endregion
    }

    /// <summary>
    /// Queues provider entries and writes bounded rolling UTF-8 files away from application threads.
    /// </summary>
    internal sealed class SafeRollingFileAppender : AppenderSkeleton
    {
        #region Fields

        private static readonly Encoding Utf8WithoutBom =
            new UTF8Encoding(false);

        private readonly string _configuredLogFolder;
        private readonly string _fallbackLogFolder;
        private readonly long _maximumFileSizeBytes;
        private readonly int _retainedFileCount;
        private readonly BlockingCollection<LogWriteRequest> _queue;
        private readonly Thread _writerThread;
        private int _started;
        private int _stopping;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a bounded asynchronous rolling-file appender.
        /// </summary>
        internal SafeRollingFileAppender(
            string configuredLogFolder,
            string fallbackLogFolder,
            long maximumFileSizeBytes,
            int retainedFileCount,
            int queueCapacity)
        {
            _configuredLogFolder = configuredLogFolder;
            _fallbackLogFolder = fallbackLogFolder;
            _maximumFileSizeBytes = maximumFileSizeBytes;
            _retainedFileCount = retainedFileCount;
            _queue = new BlockingCollection<LogWriteRequest>(
                new ConcurrentQueue<LogWriteRequest>(),
                Math.Max(32, queueCapacity));
            _writerThread = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = "ApplicationLogWriter"
            };
        }

        #endregion

        #region Appender Lifecycle

        /// <summary>
        /// Starts the background writer once provider configuration is active.
        /// </summary>
        public override void ActivateOptions()
        {
            base.ActivateOptions();

            if (Interlocked.Exchange(ref _started, 1) == 0)
            {
                _writerThread.Start();
            }
        }

        /// <summary>
        /// Stops the writer with the default bounded wait when log4net closes the appender.
        /// </summary>
        protected override void OnClose()
        {
            Stop(TimeSpan.FromSeconds(1));
            base.OnClose();
        }

        /// <summary>
        /// Flushes entries queued before this call.
        /// </summary>
        internal bool Flush(TimeSpan timeout)
        {
            if (Volatile.Read(ref _stopping) != 0)
            {
                return true;
            }

            TaskCompletionSource<bool> completion =
                new TaskCompletionSource<bool>();

            try
            {
                if (!_queue.TryAdd(LogWriteRequest.CreateFlush(completion)))
                {
                    return false;
                }

                return completion.Task.Wait(NormalizeTimeout(timeout));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Completes the queue and waits only for the configured bound.
        /// </summary>
        internal bool Stop(TimeSpan timeout)
        {
            if (Interlocked.Exchange(ref _stopping, 1) != 0)
            {
                return !_writerThread.IsAlive;
            }

            try
            {
                _queue.CompleteAdding();

                if (Thread.CurrentThread == _writerThread)
                {
                    return true;
                }

                return _writerThread.Join(NormalizeTimeout(timeout));
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Appending

        /// <summary>
        /// Enqueues a rendered event without waiting for file-system I/O.
        /// </summary>
        protected override void Append(LoggingEvent loggingEvent)
        {
            if (loggingEvent == null ||
                Volatile.Read(ref _stopping) != 0)
            {
                return;
            }

            try
            {
                string renderedEntry = RenderLoggingEvent(loggingEvent);

                _queue.TryAdd(
                    LogWriteRequest.CreateEntry(renderedEntry));
            }
            catch
            {
                // Provider rendering and queue pressure are isolated from callers.
            }
        }

        /// <summary>
        /// Processes queued entries serially so entries cannot interleave.
        /// </summary>
        private void WriteLoop()
        {
            try
            {
                foreach (LogWriteRequest request in _queue.GetConsumingEnumerable())
                {
                    try
                    {
                        if (request.Entry != null)
                        {
                            WriteWithFallback(request.Entry);
                        }
                    }
                    catch
                    {
                        // File-system errors are terminal only for the affected entry.
                    }
                    finally
                    {
                        if (request.Completion != null)
                        {
                            request.Completion.TrySetResult(true);
                        }
                    }
                }
            }
            catch
            {
                // A writer failure must not promote itself into a global exception.
            }
        }

        /// <summary>
        /// Tries the configured directory first and then the local fallback.
        /// </summary>
        private void WriteWithFallback(string entry)
        {
            if (TryWriteEntry(_configuredLogFolder, entry))
            {
                return;
            }

            if (!AreSameDirectory(
                    _configuredLogFolder,
                    _fallbackLogFolder))
            {
                TryWriteEntry(_fallbackLogFolder, entry);
            }
        }

        /// <summary>
        /// Writes one complete entry and performs size-based rollover first when required.
        /// </summary>
        private bool TryWriteEntry(string directory, string entry)
        {
            if (string.IsNullOrWhiteSpace(directory) || entry == null)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(directory);

                string logFile = Path.Combine(
                    directory,
                    ApplicationErrorLogger.CurrentLogFileName);

                byte[] bytes = Utf8WithoutBom.GetBytes(entry);

                RollIfRequired(logFile, bytes.LongLength);

                using (FileStream stream = new FileStream(
                    logFile,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Retains only the configured number of size-based backup files.
        /// </summary>
        private void RollIfRequired(
            string logFile,
            long pendingByteCount)
        {
            if (!File.Exists(logFile))
            {
                return;
            }

            FileInfo file = new FileInfo(logFile);

            if (file.Length == 0 ||
                file.Length + pendingByteCount <= _maximumFileSizeBytes)
            {
                return;
            }

            string oldestBackup = GetBackupFile(logFile, _retainedFileCount);

            if (File.Exists(oldestBackup))
            {
                File.Delete(oldestBackup);
            }

            for (int index = _retainedFileCount - 1;
                index >= 1;
                index--)
            {
                string source = GetBackupFile(logFile, index);

                if (File.Exists(source))
                {
                    File.Move(
                        source,
                        GetBackupFile(logFile, index + 1));
                }
            }

            File.Move(logFile, GetBackupFile(logFile, 1));
        }

        /// <summary>
        /// Gets one deterministic backup file name.
        /// </summary>
        private static string GetBackupFile(
            string logFile,
            int index)
        {
            return logFile + "." +
                   index.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Compares directories without allowing invalid path text to escape.
        /// </summary>
        private static bool AreSameDirectory(
            string first,
            string second)
        {
            if (string.IsNullOrWhiteSpace(first) ||
                string.IsNullOrWhiteSpace(second))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(first).TrimEnd('\\'),
                    Path.GetFullPath(second).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(
                    first,
                    second,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Converts a timeout to a safe wait value.
        /// </summary>
        private static TimeSpan NormalizeTimeout(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            return timeout > TimeSpan.FromSeconds(5)
                ? TimeSpan.FromSeconds(5)
                : timeout;
        }

        #endregion

        #region Queue Item

        /// <summary>
        /// Represents either one entry or a flush boundary.
        /// </summary>
        private sealed class LogWriteRequest
        {
            private LogWriteRequest(
                string entry,
                TaskCompletionSource<bool> completion)
            {
                Entry = entry;
                Completion = completion;
            }

            internal string Entry
            {
                get;
            }

            internal TaskCompletionSource<bool> Completion
            {
                get;
            }

            internal static LogWriteRequest CreateEntry(string entry)
            {
                return new LogWriteRequest(entry, null);
            }

            internal static LogWriteRequest CreateFlush(
                TaskCompletionSource<bool> completion)
            {
                return new LogWriteRequest(null, completion);
            }
        }

        #endregion
    }
}
