#region Namespaces

using System;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using VisualInspectionTrainingSystem.Services;

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

            // -----------------------------
            // Load Configuration
            // -----------------------------

            ReportProgress(10, "Loading configuration...");
            await Task.Delay(400);

            if (!TryLoadConfiguration(out settings))
            {
                ReportProgress(10, _startupErrorMessage);

                return;
            }

            // -----------------------------
            // Database
            // -----------------------------

            ReportProgress(30, "Checking MySQL connection...");
            await Task.Delay(300);

            bool databaseConnected = CheckDatabase();

            if (!databaseConnected)
            {
                ReportProgress(
                    30,
                    string.IsNullOrWhiteSpace(_startupErrorMessage)
                        ? "Unable to connect to MySQL."
                        : _startupErrorMessage);

                return;
            }

            // -----------------------------
            // Image Folder
            // -----------------------------

            ReportProgress(60, "Checking quiz image folder...");
            await Task.Delay(300);

            int imageCount = CheckImageFolder(
                settings.Paths.QuizImageFolder);

            ReportProgress(
                80,
                $"Found {imageCount} image(s).");

            await Task.Delay(300);

            // -----------------------------
            // Ready
            // -----------------------------

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
        private bool CheckDatabase()
        {
            try
            {
                using (MySqlService database = new MySqlService())
                {
                    bool connected = database.TestConnection();

                    if (!connected)
                    {
                        _startupErrorMessage =
                            "Unable to connect to MySQL. Check the local database configuration.";
                    }

                    return connected;
                }
            }
            catch (ConfigurationErrorsException ex)
            {
                _startupErrorMessage = ex.Message;

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
        /// Counts BMP images.
        /// </summary>
        private int CheckImageFolder(string folder)
        {
            if (!Directory.Exists(folder))
                return 0;

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
