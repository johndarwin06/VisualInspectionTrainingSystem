#region Namespaces

using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Performs all required and optional application startup checks.
    /// </summary>
    public class SystemInitializerService
    {
        #region Constants

        private static readonly TimeSpan DefaultStartupTimeout =
            TimeSpan.FromSeconds(120);

        private static readonly TimeSpan MinimumStartupTimeout =
            TimeSpan.FromSeconds(10);

        #endregion

        #region Fields

        private readonly object _syncRoot;
        private Task<InitializationResult> _initializationTask;
        private bool _completionRaised;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the startup service.
        /// </summary>
        public SystemInitializerService()
        {
            _syncRoot = new object();
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised whenever startup progress changes.
        /// </summary>
        public event EventHandler<InitializationProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Raised once when initialization has completed successfully.
        /// </summary>
        public event EventHandler InitializationCompleted;

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the initialization process and preserves the existing event-based API.
        /// </summary>
        public async Task InitializeAsync()
        {
            await InitializeAsync(CancellationToken.None);
        }

        /// <summary>
        /// Starts the initialization process with cancellation support.
        /// Duplicate calls share the same in-flight startup task.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token for the startup operation.</param>
        /// <returns>The non-sensitive startup result.</returns>
        public Task<InitializationResult> InitializeAsync(CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                if (_initializationTask == null)
                {
                    _initializationTask = InitializeCoreAsync(cancellationToken);
                }

                return _initializationTask;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Runs startup checks asynchronously.
        /// </summary>
        private async Task<InitializationResult> InitializeCoreAsync(CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                using (CancellationTokenSource startupTimeout =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    startupTimeout.CancelAfter(DefaultStartupTimeout);

                    CancellationToken token = startupTimeout.Token;

                    ReportProgress(5, "Starting system checks...");

                    ApplicationSettings settings =
                        await LoadConfigurationAsync(token);

                    startupTimeout.CancelAfter(
                        GetStartupTimeout(settings));

                    ReportProgress(35, "Checking required MySQL service...");

                    InitializationResult databaseResult =
                        await CheckDatabaseAsync(
                            settings,
                            token,
                            cancellationToken);

                    if (!databaseResult.Succeeded)
                    {
                        ReportProgress(
                            35,
                            databaseResult.StatusMessage);

                        return databaseResult;
                    }

                    ReportProgress(70, "Checking optional image inventory...");

                    OptionalStartupResult imageInventory =
                        await CheckImageInventoryAsync(
                            settings.Paths.QuizImageFolder,
                            token);

                    ReportProgress(
                        85,
                        imageInventory.StatusMessage);

                    ReportProgress(100, "System Ready");

                    InitializationResult result =
                        InitializationResult.Success(
                            "System Ready",
                            BuildSuccessDiagnostic(
                                stopwatch.Elapsed,
                                imageInventory));

                    RaiseInitializationCompletedOnce();

                    return result;
                }
            }
            catch (ConfigurationErrorsException ex)
            {
                string message = ex.Message;

                SafeReportProgress(10, message);

                return InitializationResult.Failed(
                    message,
                    "Required application configuration is missing or invalid.");
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    SafeReportProgress(0, "Startup cancelled.");

                    return InitializationResult.CreateCancelled(
                        "Startup cancelled.",
                        "Initialization was cancelled before it completed.");
                }

                SafeReportProgress(35, "Startup timed out.");

                return InitializationResult.CreateTimedOut(
                    "Startup timed out.",
                    "Initialization exceeded the bounded startup timeout.");
            }
            catch
            {
                const string message =
                    "Startup failed because an unexpected initialization error occurred.";

                SafeReportProgress(0, message);

                return InitializationResult.Failed(
                    message,
                    "Unexpected startup exception. Review configuration and service availability.");
            }
        }

        #endregion

        #region Required Checks

        /// <summary>
        /// Loads and validates application configuration on a background thread.
        /// </summary>
        private static Task<ApplicationSettings> LoadConfigurationAsync(CancellationToken cancellationToken)
        {
            return Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return ConfigurationService.GetApplicationSettings();
                },
                cancellationToken);
        }

        /// <summary>
        /// Checks the required MySQL connection using the existing resiliency service.
        /// </summary>
        private static async Task<InitializationResult> CheckDatabaseAsync(
            ApplicationSettings settings,
            CancellationToken startupToken,
            CancellationToken callerToken)
        {
            TimeSpan timeout = GetDatabaseStartupTimeout(settings.Database);

            CancellationTokenSource databaseTimeout =
                CancellationTokenSource.CreateLinkedTokenSource(startupToken);

            MySqlService database = null;

            try
            {
                databaseTimeout.CancelAfter(timeout);

                database = new MySqlService();

                Task<bool> connectionTask =
                    database.TestConnectionAsync(databaseTimeout.Token);

                Task delayTask =
                    Task.Delay(
                        timeout,
                        startupToken);

                Task completedTask =
                    await Task.WhenAny(
                        connectionTask,
                        delayTask);

                if (completedTask != connectionTask)
                {
                    databaseTimeout.Cancel();
                    database.Dispose();
                    database = null;

                    _ = ObserveDatabaseTaskAsync(connectionTask);

                    if (callerToken.IsCancellationRequested)
                    {
                        return InitializationResult.CreateCancelled(
                            "Startup cancelled.",
                            "Database check was cancelled before completion.");
                    }

                    return InitializationResult.CreateTimedOut(
                        "Database connection timed out.",
                        "Required MySQL service did not respond before the startup timeout.");
                }

                bool connected =
                    await connectionTask;

                if (connected)
                {
                    if (callerToken.IsCancellationRequested)
                    {
                        return InitializationResult.CreateCancelled(
                            "Startup cancelled.",
                            "Database check was cancelled before completion.");
                    }

                    if (startupToken.IsCancellationRequested)
                    {
                        return InitializationResult.CreateTimedOut(
                            "Database connection timed out.",
                            "Required MySQL service did not respond before the startup timeout.");
                    }

                    return InitializationResult.Success(
                        "MySQL connection ready.",
                        "Required database service is available.");
                }

                if (callerToken.IsCancellationRequested)
                {
                    return InitializationResult.CreateCancelled(
                        "Startup cancelled.",
                        "Database check was cancelled before completion.");
                }

                if (databaseTimeout.IsCancellationRequested ||
                    startupToken.IsCancellationRequested)
                {
                    return InitializationResult.CreateTimedOut(
                        "Database connection timed out.",
                        "Required MySQL service did not respond before the startup timeout.");
                }

                string message = string.IsNullOrWhiteSpace(database.LastConnectionError)
                    ? "Unable to connect to MySQL. Check that the database is running and reachable."
                    : database.LastConnectionError;

                return InitializationResult.Failed(
                    message,
                    "Required MySQL service is unavailable.");
            }
            finally
            {
                databaseTimeout.Dispose();

                if (database != null)
                {
                    database.Dispose();
                }
            }
        }

        /// <summary>
        /// Observes a timed-out database task so any later exception does not surface unhandled.
        /// </summary>
        private static async Task ObserveDatabaseTaskAsync(Task<bool> connectionTask)
        {
            try
            {
                await connectionTask;
            }
            catch
            {
            }
        }

        #endregion

        #region Optional Checks

        /// <summary>
        /// Counts image files when available without blocking startup if the optional inventory check fails.
        /// </summary>
        private static async Task<OptionalStartupResult> CheckImageInventoryAsync(
            string folder,
            CancellationToken cancellationToken)
        {
            try
            {
                int imageCount =
                    await Task.Run(
                        () => CountImageFiles(
                            folder,
                            cancellationToken),
                        cancellationToken);

                return OptionalStartupResult.CreateAvailable(
                    "Found " + imageCount + " image(s).",
                    "Optional image inventory completed.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return OptionalStartupResult.CreateSkipped(
                    "Image inventory unavailable; continuing startup.",
                    "Optional image inventory check was skipped.");
            }
        }

        /// <summary>
        /// Counts BMP images in the configured folder.
        /// </summary>
        private static int CountImageFiles(
            string folder,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(folder) ||
                !Directory.Exists(folder))
            {
                return 0;
            }

            string[] files =
                Directory.GetFiles(
                    folder,
                    "*.bmp",
                    SearchOption.TopDirectoryOnly);

            return files.Length;
        }

        #endregion

        #region Progress

        /// <summary>
        /// Reports startup progress.
        /// </summary>
        private void ReportProgress(
            int progress,
            string message)
        {
            ProgressChanged?.Invoke(
                this,
                new InitializationProgressEventArgs(
                    progress,
                    message));
        }

        /// <summary>
        /// Reports failure progress without allowing notification errors to replace the startup result.
        /// </summary>
        private void SafeReportProgress(
            int progress,
            string message)
        {
            try
            {
                ReportProgress(
                    progress,
                    message);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Raises successful completion only once.
        /// </summary>
        private void RaiseInitializationCompletedOnce()
        {
            if (_completionRaised)
            {
                return;
            }

            _completionRaised = true;

            InitializationCompleted?.Invoke(
                this,
                EventArgs.Empty);
        }

        #endregion

        #region Timeout Helpers

        /// <summary>
        /// Gets the bounded startup wait time.
        /// </summary>
        private static TimeSpan GetStartupTimeout(ApplicationSettings settings)
        {
            TimeSpan databaseTimeout =
                GetDatabaseStartupTimeout(settings.Database);

            double totalMilliseconds =
                databaseTimeout.TotalMilliseconds +
                10000.0;

            totalMilliseconds = Math.Max(
                totalMilliseconds,
                MinimumStartupTimeout.TotalMilliseconds);

            totalMilliseconds = Math.Min(
                totalMilliseconds,
                DefaultStartupTimeout.TotalMilliseconds);

            return TimeSpan.FromMilliseconds(totalMilliseconds);
        }

        /// <summary>
        /// Gets the maximum startup wait time for database connectivity.
        /// </summary>
        private static TimeSpan GetDatabaseStartupTimeout(DatabaseSettings settings)
        {
            int attemptCount = Math.Max(
                1,
                settings.RetryCount + 1);

            int retryDelayCount = Math.Max(
                0,
                attemptCount - 1);

            double totalMilliseconds =
                (settings.ConnectionTimeoutSeconds * 1000.0 * attemptCount) +
                (settings.RetryDelayMilliseconds * retryDelayCount) +
                1000.0;

            return TimeSpan.FromMilliseconds(
                Math.Min(
                    totalMilliseconds,
                    DefaultStartupTimeout.TotalMilliseconds));
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Builds the success diagnostic message.
        /// </summary>
        private static string BuildSuccessDiagnostic(
            TimeSpan elapsed,
            OptionalStartupResult imageInventory)
        {
            string optionalStatus = imageInventory == null
                ? "Optional checks completed."
                : imageInventory.DiagnosticMessage;

            return "Startup completed in " +
                   Math.Round(elapsed.TotalSeconds, 1).ToString("0.0") +
                   " second(s). " +
                   optionalStatus;
        }

        #endregion
    }

    /// <summary>
    /// Progress information.
    /// </summary>
    public class InitializationProgressEventArgs : EventArgs
    {
        #region Constructor

        /// <summary>
        /// Creates startup progress information.
        /// </summary>
        public InitializationProgressEventArgs(
            int progress,
            string message)
        {
            Progress = progress;
            Message = message;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Startup progress from 0 through 100.
        /// </summary>
        public int Progress
        {
            get;
        }

        /// <summary>
        /// Non-sensitive status message.
        /// </summary>
        public string Message
        {
            get;
        }

        #endregion
    }

    /// <summary>
    /// Non-sensitive startup result.
    /// </summary>
    public class InitializationResult
    {
        #region Constructor

        private InitializationResult(
            bool succeeded,
            bool cancelled,
            bool timedOut,
            string statusMessage,
            string diagnosticMessage)
        {
            Succeeded = succeeded;
            Cancelled = cancelled;
            TimedOut = timedOut;
            StatusMessage = statusMessage;
            DiagnosticMessage = diagnosticMessage;
        }

        #endregion

        #region Properties

        /// <summary>
        /// True when all required startup checks succeeded.
        /// </summary>
        public bool Succeeded
        {
            get;
        }

        /// <summary>
        /// True when startup was cancelled by the caller.
        /// </summary>
        public bool Cancelled
        {
            get;
        }

        /// <summary>
        /// True when startup exceeded the bounded timeout.
        /// </summary>
        public bool TimedOut
        {
            get;
        }

        /// <summary>
        /// Primary non-sensitive status message.
        /// </summary>
        public string StatusMessage
        {
            get;
        }

        /// <summary>
        /// Supporting non-sensitive diagnostic message.
        /// </summary>
        public string DiagnosticMessage
        {
            get;
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Creates a successful startup result.
        /// </summary>
        public static InitializationResult Success(
            string statusMessage,
            string diagnosticMessage)
        {
            return new InitializationResult(
                true,
                false,
                false,
                statusMessage,
                diagnosticMessage);
        }

        /// <summary>
        /// Creates a failed startup result.
        /// </summary>
        public static InitializationResult Failed(
            string statusMessage,
            string diagnosticMessage)
        {
            return new InitializationResult(
                false,
                false,
                false,
                statusMessage,
                diagnosticMessage);
        }

        /// <summary>
        /// Creates a cancelled startup result.
        /// </summary>
        public static InitializationResult CreateCancelled(
            string statusMessage,
            string diagnosticMessage)
        {
            return new InitializationResult(
                false,
                true,
                false,
                statusMessage,
                diagnosticMessage);
        }

        /// <summary>
        /// Creates a timed-out startup result.
        /// </summary>
        public static InitializationResult CreateTimedOut(
            string statusMessage,
            string diagnosticMessage)
        {
            return new InitializationResult(
                false,
                false,
                true,
                statusMessage,
                diagnosticMessage);
        }

        #endregion
    }

    /// <summary>
    /// Result for optional startup checks.
    /// </summary>
    internal sealed class OptionalStartupResult
    {
        #region Constructor

        private OptionalStartupResult(
            bool available,
            string statusMessage,
            string diagnosticMessage)
        {
            Available = available;
            StatusMessage = statusMessage;
            DiagnosticMessage = diagnosticMessage;
        }

        #endregion

        #region Properties

        /// <summary>
        /// True when the optional check completed.
        /// </summary>
        public bool Available
        {
            get;
        }

        /// <summary>
        /// Non-sensitive optional-check status.
        /// </summary>
        public string StatusMessage
        {
            get;
        }

        /// <summary>
        /// Non-sensitive optional-check diagnostic.
        /// </summary>
        public string DiagnosticMessage
        {
            get;
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Creates an available optional-check result.
        /// </summary>
        public static OptionalStartupResult AvailableResult(
            string statusMessage,
            string diagnosticMessage)
        {
            return new OptionalStartupResult(
                true,
                statusMessage,
                diagnosticMessage);
        }

        /// <summary>
        /// Creates an available optional-check result.
        /// </summary>
        public static OptionalStartupResult CreateAvailable(
            string statusMessage,
            string diagnosticMessage)
        {
            return AvailableResult(
                statusMessage,
                diagnosticMessage);
        }

        /// <summary>
        /// Creates a skipped optional-check result.
        /// </summary>
        public static OptionalStartupResult CreateSkipped(
            string statusMessage,
            string diagnosticMessage)
        {
            return new OptionalStartupResult(
                false,
                statusMessage,
                diagnosticMessage);
        }

        #endregion
    }
}
