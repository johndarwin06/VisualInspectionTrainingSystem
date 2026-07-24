#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Session-level row displayed and exported by the Reports module.
    /// </summary>
    public class ReportSessionRow
    {
        #region Identity

        /// <summary>
        /// Gets or sets the training session identifier.
        /// </summary>
        public int SessionID { get; set; }

        /// <summary>
        /// Gets or sets the trainee employee number.
        /// </summary>
        public string EmployeeNo { get; set; }

        /// <summary>
        /// Gets or sets the trainee full name.
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// Gets or sets the trainee department.
        /// </summary>
        public string Department { get; set; }

        #endregion

        #region Session Values

        /// <summary>
        /// Gets or sets the local session start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the optional local session end time.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets or sets the configured question count for the session.
        /// </summary>
        public int TotalQuestions { get; set; }

        /// <summary>
        /// Gets the session completion state.
        /// </summary>
        public string Status
        {
            get
            {
                return EndTime.HasValue ? "Completed" : "Open";
            }
        }

        #endregion

        #region Review Values

        /// <summary>
        /// Gets or sets the count of reviewed answers with matching supported values.
        /// </summary>
        public int CorrectAnswers { get; set; }

        /// <summary>
        /// Gets or sets the count of reviewed answers with missing, unsupported, or different trainee values.
        /// </summary>
        public int WrongAnswers { get; set; }

        /// <summary>
        /// Gets or sets the count of answers without a supported GOOD or NG truth value.
        /// </summary>
        public int PendingAnswers { get; set; }

        /// <summary>
        /// Gets or sets the count of answers with a supported GOOD or NG truth value.
        /// </summary>
        public int ReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets reviewed-only accuracy, or null when the session has no reviewed answers.
        /// </summary>
        public decimal? ReviewedAccuracy { get; set; }

        /// <summary>
        /// Gets a display-safe reviewed-accuracy value.
        /// </summary>
        public string ReviewedAccuracyText
        {
            get
            {
                return ReviewedAccuracy.HasValue
                    ? ReviewedAccuracy.Value.ToString("0.00") + "%"
                    : "N/A";
            }
        }

        #endregion

        #region Compatibility Properties

        /// <summary>
        /// Gets or sets the legacy non-nullable accuracy value.
        /// </summary>
        /// <remarks>
        /// New Reports consumers should use <see cref="ReviewedAccuracy"/>
        /// so unavailable accuracy is not represented as zero.
        /// </remarks>
        public decimal Accuracy
        {
            get
            {
                return ReviewedAccuracy ?? 0m;
            }
            set
            {
                ReviewedAccuracy = value;
            }
        }

        #endregion
    }
}
