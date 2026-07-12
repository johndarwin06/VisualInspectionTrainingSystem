using VisualInspectionTrainingSystem.Services;

namespace VisualInspectionTrainingSystem
{
    /// <summary>
    /// Compatibility accessors for application-wide values.
    /// </summary>
    public static class AppConstants
    {
        #region Properties

        /// <summary>
        /// Configured quiz image folder.
        /// </summary>
        public static string QuizImageFolder
        {
            get
            {
                return ConfigurationService.GetPathSettings().QuizImageFolder;
            }
        }

        #endregion
    }
}
