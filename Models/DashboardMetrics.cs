#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Summary values shown on the administrator dashboard.
    /// </summary>
    public class DashboardMetrics
    {
        #region Daily Metrics

        /// <summary>
        /// Gets or sets the number of completed training sessions that started today.
        /// </summary>
        public int TodaysTraining { get; set; }

        /// <summary>
        /// Gets or sets reviewed-answer accuracy for today, or null when no answers are reviewed.
        /// </summary>
        public decimal? AverageReviewedAccuracy { get; set; }

        /// <summary>
        /// Gets or sets valid completed-session time for today in whole seconds.
        /// </summary>
        public long TimeSpentSeconds { get; set; }

        /// <summary>
        /// Gets or sets the number of trainee GOOD selections made today.
        /// </summary>
        public int GoodCount { get; set; }

        /// <summary>
        /// Gets or sets the number of trainee NG selections made today.
        /// </summary>
        public int NgCount { get; set; }

        /// <summary>
        /// Gets or sets the number of answers reviewed today.
        /// </summary>
        public int ReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets the number of reviewed answers that match trainee selections.
        /// </summary>
        public int CorrectReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets the number of reviewed answers that do not match trainee selections.
        /// </summary>
        public int WrongReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets the number of answers still pending administrator review today.
        /// </summary>
        public int PendingAnswers { get; set; }

        #endregion

        #region Compatibility Properties

        /// <summary>
        /// Gets or sets the session total retained for existing dashboard consumers.
        /// </summary>
        public int TotalSessions { get; set; }

        /// <summary>
        /// Gets or sets the answer total retained for existing dashboard consumers.
        /// </summary>
        public int TotalAnswers { get; set; }

        /// <summary>
        /// Gets or sets the active-trainee count retained for existing dashboard consumers.
        /// </summary>
        public int ActiveTrainees { get; set; }

        /// <summary>
        /// Gets or sets reviewed accuracy retained for existing dashboard consumers.
        /// </summary>
        public decimal AverageAccuracy { get; set; }

        /// <summary>
        /// Gets or sets the latest completed-session start time in the selected day.
        /// </summary>
        public DateTime? LatestSessionTime { get; set; }

        #endregion
    }
}
