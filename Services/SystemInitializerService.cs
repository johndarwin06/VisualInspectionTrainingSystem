#region Namespaces

using System;
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
            // -----------------------------
            // Load Configuration
            // -----------------------------

            ReportProgress(10, "Loading configuration...");
            await Task.Delay(400);

            // -----------------------------
            // Database
            // -----------------------------

            ReportProgress(30, "Checking MySQL connection...");
            await Task.Delay(300);

            bool databaseConnected = CheckDatabase();

            if (!databaseConnected)
            {
                ReportProgress(30, "Unable to connect to MySQL.");
                return;
            }

            // -----------------------------
            // Image Folder
            // -----------------------------

            ReportProgress(60, "Checking quiz image folder...");
            await Task.Delay(300);

            int imageCount = CheckImageFolder();

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
        /// Checks the MySQL connection.
        /// </summary>
        private bool CheckDatabase()
        {
            try
            {
                MySqlService database = new MySqlService();

                return database.TestConnection();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Counts BMP images.
        /// </summary>
        private int CheckImageFolder()
        {
            const string folder =
                @"D:\QuizImages";

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