#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Aggregate values for one reports date period.
    /// </summary>
    public class ReportSummary
    {
        #region Session Metrics

        /// <summary>
        /// Gets or sets the total number of matching sessions.
        /// </summary>
        public int SessionCount { get; set; }

        /// <summary>
        /// Gets or sets the number of matching completed sessions.
        /// </summary>
        public int CompletedSessionCount { get; set; }

        /// <summary>
        /// Gets or sets the number of matching open sessions.
        /// </summary>
        public int OpenSessionCount { get; set; }

        /// <summary>
        /// Gets or sets the number of distinct matching trainees.
        /// </summary>
        public int TraineeCount { get; set; }

        /// <summary>
        /// Gets or sets the first matching session start time.
        /// </summary>
        public DateTime? FirstSessionTime { get; set; }

        /// <summary>
        /// Gets or sets the last matching session start time.
        /// </summary>
        public DateTime? LastSessionTime { get; set; }

        #endregion

        #region Answer Metrics

        /// <summary>
        /// Gets or sets the total configured question count across matching sessions.
        /// </summary>
        public int TotalQuestions { get; set; }

        /// <summary>
        /// Gets or sets the number of reviewed answers that match supported truth values.
        /// </summary>
        public int CorrectAnswers { get; set; }

        /// <summary>
        /// Gets or sets the number of reviewed answers that do not match supported truth values.
        /// </summary>
        public int WrongAnswers { get; set; }

        /// <summary>
        /// Gets or sets the number of answers whose truth is not a supported GOOD or NG value.
        /// </summary>
        public int PendingAnswers { get; set; }

        /// <summary>
        /// Gets or sets the number of answers with a supported GOOD or NG truth value.
        /// </summary>
        public int ReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets reviewed-only accuracy, or null when no answer is reviewed.
        /// </summary>
        public decimal? AverageReviewedAccuracy { get; set; }

        #endregion

        #region Compatibility Properties

        /// <summary>
        /// Gets or sets the legacy non-nullable average accuracy value.
        /// </summary>
        /// <remarks>
        /// New Reports consumers should use <see cref="AverageReviewedAccuracy"/>
        /// so a zero reviewed-answer denominator can be represented as unavailable.
        /// </remarks>
        public decimal AverageAccuracy
        {
            get
            {
                return AverageReviewedAccuracy ?? 0m;
            }
            set
            {
                AverageReviewedAccuracy = value;
            }
        }

        #endregion
    }
}
