#region Namespaces

using System;
using System.Globalization;
using System.IO;
using System.Text;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Writes non-sensitive application error diagnostics without allowing logging failures to affect the application.
    /// </summary>
    internal static class ApplicationErrorLogger
    {
        #region Constants

        private const string ApplicationFolderName =
            "VisualInspectionTrainingSystem";

        private const string LogFolderName = "Logs";

        private const string LogFilePrefix = "application-errors-";

        #endregion

        #region Fields

        private static readonly object SyncRoot = new object();

        #endregion

        #region Public Methods

        /// <summary>
        /// Records an unhandled error using a non-sensitive diagnostic entry.
        /// </summary>
        /// <param name="source">The application boundary where the error was observed.</param>
        /// <param name="exception">The exception to classify.</param>
        public static void LogUnhandledException(
            string source,
            Exception exception)
        {
            try
            {
                string logDirectory = GetLogDirectory();

                Directory.CreateDirectory(logDirectory);

                string logFile = Path.Combine(
                    logDirectory,
                    LogFilePrefix +
                    DateTime.UtcNow.ToString(
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture) +
                    ".log");

                lock (SyncRoot)
                {
                    File.AppendAllText(
                        logFile,
                        BuildEntry(source, exception),
                        Encoding.UTF8);
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
        /// Builds a stable diagnostic entry without persisting exception messages or connection details.
        /// </summary>
        private static string BuildEntry(
            string source,
            Exception exception)
        {
            StringBuilder entry = new StringBuilder();

            entry.Append(DateTime.UtcNow.ToString(
                "o",
                CultureInfo.InvariantCulture));
            entry.Append(" | Source: ");
            entry.Append(SanitizeSource(source));
            entry.Append(" | ExceptionType: ");
            entry.Append(GetExceptionType(exception));
            entry.AppendLine();

            return entry.ToString();
        }

        /// <summary>
        /// Returns a safe exception classification without including exception text.
        /// </summary>
        private static string GetExceptionType(Exception exception)
        {
            return exception == null
                ? "Unknown"
                : exception.GetType().FullName;
        }

        /// <summary>
        /// Restricts a source label to a concise single-line diagnostic value.
        /// </summary>
        private static string SanitizeSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "Unknown";
            }

            return source.Replace("\r", string.Empty)
                         .Replace("\n", string.Empty)
                         .Trim();
        }

        #endregion

        #region Paths

        /// <summary>
        /// Gets the configuration-independent directory for global error diagnostics.
        /// </summary>
        private static string GetLogDirectory()
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
