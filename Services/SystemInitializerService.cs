#region Namespaces

using System;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Performs all application startup checks.
    /// </summary>
    public class SystemInitializerService
    {
        #region Fields

        private string _startupErrorMessage;

        #endregion

        #region Events

        /// <summary>
        /// Raised whenever startup progress changes.
        /// </summary>
        public event EventHandler<InitializationProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Raised when initialization has completed.
        /// </summary>
        public event EventHandler InitializationCompleted;

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the initialization process.
        /// </summary>
        public async Task InitializeAsync()
        {
            ApplicationSettings settings;

            ReportProgress(10, "Loading configuration...");
            await Task.Delay(400);

            if (!TryLoadConfiguration(out settings))
            {
                ReportProgress(10, _startupErrorMessage);

                return;
            }

            ReportProgress(30, "Checking MySQL connection...");
            await Task.Delay(300);

            bool databaseConnected = await CheckDatabaseAsync(settings);

            if (!databaseConnected)
            {
                ReportProgress(
                    30,
                    string.IsNullOrWhiteSpace(_startupErrorMessage)
                        ? "Unable to connect to MySQL."
                        : _startupErrorMessage);

                return;
            }

            ReportProgress(60, "Checking quiz image folder...");
            await Task.Delay(300);

            int imageCount = CheckImageFolder(
                settings.Paths.QuizImageFolder);

            ReportProgress(
                80,
                $"Found {imageCount} image(s).");

            await Task.Delay(300);

            ReportProgress(100, "System Ready");

            await Task.Delay(500);

            InitializationCompleted?.Invoke(
                this,
                EventArgs.Empty);
        }

        #endregion

        #region Private Methods

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
        /// Loads and validates application configuration.
        /// </summary>
        private bool TryLoadConfiguration(out ApplicationSettings settings)
        {
            settings = null;

            try
            {
                settings = ConfigurationService.GetApplicationSettings();

                return true;
            }
            catch (ConfigurationErrorsException ex)
            {
                _startupErrorMessage = ex.Message;

                return false;
            }
            catch
            {
                _startupErrorMessage =
                    "Application configuration could not be loaded.";

                return false;
            }
        }

        /// <summary>
        /// Checks the MySQL connection.
        /// </summary>
        private async Task<bool> CheckDatabaseAsync(ApplicationSettings settings)
        {
            try
            {
                using (CancellationTokenSource timeout =
                    new CancellationTokenSource(
                        GetDatabaseStartupTimeout(settings.Database)))
                {
                    using (MySqlService database = new MySqlService())
                    {
                        bool connected =
                            await database.TestConnectionAsync(timeout.Token);

                        if (!connected)
                        {
                            _startupErrorMessage =
                                string.IsNullOrWhiteSpace(database.LastConnectionError)
                                    ? "Unable to connect to MySQL. Check the local database configuration."
                                    : database.LastConnectionError;
                        }

                        return connected;
                    }
                }
            }
            catch (ConfigurationErrorsException ex)
            {
                _startupErrorMessage = ex.Message;

                return false;
            }
            catch (OperationCanceledException)
            {
                _startupErrorMessage =
                    "Database connection timed out. Check that MySQL is running and reachable.";

                return false;
            }
            catch
            {
                _startupErrorMessage =
                    "Unable to connect to MySQL. Check the local database configuration.";

                return false;
            }
        }

        /// <summary>
        /// Gets the maximum startup wait time for database connectivity.
        /// </summary>
        private TimeSpan GetDatabaseStartupTimeout(DatabaseSettings settings)
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
                    120000.0));
        }

        /// <summary>
        /// Counts BMP images.
        /// </summary>
        private int CheckImageFolder(string folder)
        {
            if (!Directory.Exists(folder))
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
    }

    /// <summary>
    /// Progress information.
    /// </summary>
    public class InitializationProgressEventArgs : EventArgs
    {
        #region Constructor

        public InitializationProgressEventArgs(
            int progress,
            string message)
        {
            Progress = progress;
            Message = message;
        }

        #endregion

        #region Properties

        public int Progress
        {
            get;
        }

        public string Message
        {
            get;
        }

        #endregion
    }
}
