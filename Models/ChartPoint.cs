#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents one chart-neutral daily analytics point.
    /// </summary>
    public class ChartPoint
    {
        #region Period

        /// <summary>
        /// Gets or sets the inclusive local start of the represented day.
        /// </summary>
        public DateTime PeriodStartLocal { get; set; }

        /// <summary>
        /// Gets or sets the short display label for the represented day.
        /// </summary>
        public string Label { get; set; }

        #endregion

        #region Activity Values

        /// <summary>
        /// Gets or sets the number of completed sessions that started on the day.
        /// </summary>
        public int CompletedSessions { get; set; }

        /// <summary>
        /// Gets or sets valid completed-session duration in whole seconds.
        /// </summary>
        public long DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the number of normalized GOOD trainee selections.
        /// </summary>
        public int GoodSelections { get; set; }

        /// <summary>
        /// Gets or sets the number of normalized NG trainee selections.
        /// </summary>
        public int NgSelections { get; set; }

        #endregion

        #region Review Values

        /// <summary>
        /// Gets or sets the number of answers with supported GOOD or NG truth.
        /// </summary>
        public int ReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets the number of supported trainee selections that match truth.
        /// </summary>
        public int CorrectReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets the number of answers without supported GOOD or NG truth.
        /// </summary>
        public int PendingAnswers { get; set; }

        /// <summary>
        /// Gets or sets reviewed-only accuracy, or null when there is no denominator.
        /// </summary>
        public decimal? ReviewedAccuracyPercent { get; set; }

        #endregion
    }
}
