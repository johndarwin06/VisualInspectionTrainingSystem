#region Namespaces

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Writes bounded, sanitized diagnostics without allowing logging failures to affect the application.
    /// </summary>
    internal static class ApplicationErrorLogger
    {
        #region Constants

        private const string ApplicationFolderName =
            "VisualInspectionTrainingSystem";

        private const string LogFolderName = "Logs";

        private const string LogFilePrefix = "application-errors-";

        private const int MaximumMessageLength = 1024;

        private const int MaximumStackTraceLength = 4096;

        private const int MaximumSourceLength = 128;

        private const int MaximumInnerExceptions = 5;

        #endregion

        #region Fields

        private static readonly object SyncRoot = new object();

        private static readonly Regex ConnectionStringPattern =
            new Regex(
                "(?i)(?:server|host|data\\s+source|database|initial\\s+catalog|port|uid|user\\s*id|username|password|pwd)\\s*=\\s*[^;\\r\\n]*(?:\\s*;\\s*(?:server|host|data\\s+source|database|initial\\s+catalog|port|uid|user\\s*id|username|password|pwd)\\s*=\\s*[^;\\r\\n]*)+",
                RegexOptions.Compiled);

        private static readonly Regex SensitiveValuePattern =
            new Regex(
                "(?i)\\b(password|pwd|user\\s*id|uid|username|user|token|access[_ -]?token|refresh[_ -]?token|secret|api[_ -]?key)\\s*([=:])\\s*(?:\\\"[^\\\"]*\\\"|'[^']*'|[^;,\\s\\r\\n]+)",
                RegexOptions.Compiled);

        private static string _configuredLogFolder;

        #endregion

        #region Public Methods

        /// <summary>
        /// Caches the already-validated configured log folder for later non-discovery error logging.
        /// </summary>
        /// <param name="logFolder">The validated configured log folder.</param>
        public static void ConfigureLogFolder(string logFolder)
        {
            if (string.IsNullOrWhiteSpace(logFolder))
            {
                return;
            }

            lock (SyncRoot)
            {
                _configuredLogFolder = logFolder.Trim();
            }
        }

        /// <summary>
        /// Records an unhandled error that is not expected to terminate the process.
        /// </summary>
        /// <param name="source">The application boundary where the error was observed.</param>
        /// <param name="exception">The exception to classify.</param>
        public static void LogUnhandledException(
            string source,
            Exception exception)
        {
            LogUnhandledException(source, exception, false);
        }

        /// <summary>
        /// Records an unhandled error using a bounded, non-sensitive diagnostic entry.
        /// </summary>
        /// <param name="source">The application boundary where the error was observed.</param>
        /// <param name="exception">The exception to classify.</param>
        /// <param name="terminationExpected">Whether the handler will terminate the application or process.</param>
        public static void LogUnhandledException(
            string source,
            Exception exception,
            bool terminationExpected)
        {
            try
            {
                string entry = BuildEntry(
                    source,
                    exception,
                    terminationExpected);

                lock (SyncRoot)
                {
                    string configuredLogFolder = _configuredLogFolder;

                    if (TryWriteEntry(
                            configuredLogFolder,
                            entry,
                            false))
                    {
                        return;
                    }

                    TryWriteEntry(
                        GetFallbackLogDirectory(),
                        entry,
                        true);
                }
            }
            catch
            {
                // Global exception handlers must never throw while reporting a failure.
            }
        }

        #endregion

        #region Entry Building

        /// <summary>
        /// Builds a stable diagnostic entry while bounding and sanitizing exception content.
        /// </summary>
        /// <param name="source">The application boundary where the error was observed.</param>
        /// <param name="exception">The exception to record.</param>
        /// <param name="terminationExpected">Whether termination is expected.</param>
        /// <returns>A complete log entry.</returns>
        private static string BuildEntry(
            string source,
            Exception exception,
            bool terminationExpected)
        {
            StringBuilder entry = new StringBuilder();

            entry.AppendLine("--- Application Error ---");
            AppendField(
                entry,
                "TimestampUtc",
                DateTime.UtcNow.ToString(
                    "o",
                    CultureInfo.InvariantCulture));
            AppendField(entry, "ErrorId", Guid.NewGuid().ToString("N"));
            AppendField(entry, "Source", SanitizeSource(source));
            AppendField(
                entry,
                "TerminationExpected",
                terminationExpected
                    ? "True"
                    : "False");
            AppendField(entry, "ExceptionType", GetExceptionType(exception));
            AppendField(
                entry,
                "ExceptionMessage",
                SanitizeAndLimit(
                    exception == null
                        ? "No exception details were supplied."
                        : exception.Message,
                    MaximumMessageLength));

            AppendStackTrace(entry, exception);
            AppendInnerExceptions(entry, exception);
            AppendAggregateExceptionTypes(entry, exception);
            entry.AppendLine("--- End Application Error ---");

            return entry.ToString();
        }

        /// <summary>
        /// Adds a labeled value to the current entry.
        /// </summary>
        /// <param name="entry">The entry builder.</param>
        /// <param name="name">The field name.</param>
        /// <param name="value">The field value.</param>
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
        /// Appends a bounded stack trace when the exception supplied one.
        /// </summary>
        /// <param name="entry">The entry builder.</param>
        /// <param name="exception">The exception to inspect.</param>
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
        /// Appends a bounded inner-exception chain without serializing the full exception object.
        /// </summary>
        /// <param name="entry">The entry builder.</param>
        /// <param name="exception">The outer exception.</param>
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
        /// Appends flattened aggregate exception type names without relying on recursive formatting.
        /// </summary>
        /// <param name="entry">The entry builder.</param>
        /// <param name="exception">The exception to inspect.</param>
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
                flattenedExceptions = aggregateException.Flatten().InnerExceptions;
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

        /// <summary>
        /// Returns a safe exception classification without formatting the exception instance.
        /// </summary>
        /// <param name="exception">The exception to classify.</param>
        /// <returns>The exception type name.</returns>
        private static string GetExceptionType(Exception exception)
        {
            return exception == null
                ? "Unknown"
                : exception.GetType().FullName;
        }

        /// <summary>
        /// Restricts a source label to a concise single-line diagnostic value.
        /// </summary>
        /// <param name="source">The source label.</param>
        /// <returns>A bounded source label.</returns>
        private static string SanitizeSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "Unknown";
            }

            return SanitizeAndLimit(source, MaximumSourceLength);
        }

        /// <summary>
        /// Removes credential-like values and bounds potentially hostile diagnostic text.
        /// </summary>
        /// <param name="value">The text to sanitize.</param>
        /// <param name="maximumLength">The maximum returned length.</param>
        /// <returns>Safe bounded text.</returns>
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
            }
            catch
            {
                sanitized = "Diagnostic text could not be sanitized.";
            }

            sanitized = sanitized.Replace("\0", string.Empty).Trim();

            if (sanitized.Length <= maximumLength)
            {
                return sanitized;
            }

            return sanitized.Substring(0, maximumLength) + " [truncated]";
        }

        #endregion

        #region Paths And Writes

        /// <summary>
        /// Attempts one fully serialized log write without allowing file-system failures to escape.
        /// </summary>
        /// <param name="logDirectory">The target directory.</param>
        /// <param name="entry">The already-built entry.</param>
        /// <param name="createDirectory">Whether the target directory may be created before writing.</param>
        /// <returns><c>true</c> when the entry was written.</returns>
        private static bool TryWriteEntry(
            string logDirectory,
            string entry,
            bool createDirectory)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                return false;
            }

            try
            {
                if (createDirectory)
                {
                    Directory.CreateDirectory(logDirectory);
                }
                else if (!Directory.Exists(logDirectory))
                {
                    return false;
                }

                string logFile = Path.Combine(
                    logDirectory,
                    LogFilePrefix +
                    DateTime.UtcNow.ToString(
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture) +
                    ".log");

                File.AppendAllText(logFile, entry, Encoding.UTF8);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the configuration-independent fallback directory for global error diagnostics.
        /// </summary>
        /// <returns>The local application-data fallback directory.</returns>
        private static string GetFallbackLogDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                ApplicationFolderName,
                LogFolderName);
        }

        #endregion
    }
}
