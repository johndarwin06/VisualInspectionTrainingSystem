#region Namespaces

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents one trainee answer, its durable image identity, and administrator review state.
    /// </summary>
    public class QuizAnswer : INotifyPropertyChanged
    {
        #region Constants

        /// <summary>
        /// Identifies an answer reviewed directly by an administrator.
        /// </summary>
        public const string ManualReviewSource = "MANUAL";

        /// <summary>
        /// Identifies an answer graded from reusable administrator image truth.
        /// </summary>
        public const string AutomaticReviewSource = "AUTO";

        #endregion

        #region Fields

        private bool _isSelected;

        #endregion

        #region Identity

        /// <summary>
        /// Gets or sets the database answer identity.
        /// </summary>
        public int AnswerID { get; set; }

        /// <summary>
        /// Gets or sets the parent training session identity.
        /// </summary>
        public int SessionID { get; set; }

        /// <summary>
        /// Gets or sets the one-based question sequence within the session.
        /// </summary>
        public int Sequence { get; set; }

        #endregion

        #region Image Information

        /// <summary>
        /// Gets or sets the transient image catalog identity retained for compatibility.
        /// </summary>
        public int ImageID { get; set; }

        /// <summary>
        /// Gets or sets the normalized lowercase SHA-256 identity of the exact image bytes.
        /// </summary>
        public string ImageHash { get; set; }

        /// <summary>
        /// Gets whether a stable image identity is available.
        /// </summary>
        public bool HasStableIdentity
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ImageHash) &&
                       ImageHash.Length == 64;
            }
        }

        /// <summary>
        /// Gets a shortened stable identity for safe display.
        /// </summary>
        public string ShortImageHash
        {
            get
            {
                if (!HasStableIdentity)
                    return "Unavailable";

                return ImageHash.Substring(0, 12);
            }
        }

        /// <summary>
        /// Gets or sets the safe image filename stored with the answer.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the current local preview path. This path is never persisted.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets the employee number from the parent session.
        /// </summary>
        public string EmployeeNo { get; set; }

        #endregion

        #region User Answer

        /// <summary>
        /// Gets or sets the trainee's GOOD or NG selection.
        /// </summary>
        public QuizAnswerType UserAnswer { get; set; }

        #endregion

        #region Administrator Review

        /// <summary>
        /// Gets or sets the supported administrator truth, or null while pending.
        /// </summary>
        public QuizAnswerType? CorrectAnswer { get; set; }

        /// <summary>
        /// Gets or sets MANUAL or AUTO review provenance.
        /// </summary>
        public string ReviewSource { get; set; }

        /// <summary>
        /// Gets or sets the review timestamp when available.
        /// </summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>
        /// Gets or sets the non-secret reviewer employee number when available.
        /// </summary>
        public string ReviewedBy { get; set; }

        /// <summary>
        /// Gets whether this answer was graded automatically from reusable image truth.
        /// </summary>
        public bool IsAutoReviewed
        {
            get
            {
                return IsReviewed &&
                       string.Equals(
                           ReviewSource,
                           AutomaticReviewSource,
                           StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Gets whether this answer was reviewed directly by an administrator.
        /// </summary>
        public bool IsManuallyReviewed
        {
            get
            {
                return IsReviewed &&
                       !IsAutoReviewed;
            }
        }

        /// <summary>
        /// Gets whether a reusable review can be applied to this image.
        /// </summary>
        public bool HasReusableTruth
        {
            get
            {
                return HasStableIdentity && IsReviewed;
            }
        }

        #endregion

        #region Result

        /// <summary>
        /// Gets or sets whether the trainee answer matches supported administrator truth.
        /// </summary>
        public bool IsCorrect { get; set; }

        /// <summary>
        /// Gets whether this row contains supported GOOD or NG administrator truth.
        /// </summary>
        public bool IsReviewed
        {
            get
            {
                return CorrectAnswer.HasValue &&
                       IsSupportedAnswer(CorrectAnswer.Value);
            }
        }

        #endregion

        #region Timing

        /// <summary>
        /// Gets or sets when the trainee submitted the answer.
        /// </summary>
        public DateTime AnswerTime { get; set; }

        /// <summary>
        /// Gets or sets seconds taken to answer the question.
        /// </summary>
        public double ElapsedSeconds { get; set; }

        #endregion

        #region Selection

        /// <summary>
        /// Gets or sets whether the administrator selected this row for a bulk operation.
        /// </summary>
        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Display

        /// <summary>
        /// Gets a readable reviewed result.
        /// </summary>
        public string ResultText
        {
            get
            {
                if (!IsReviewed)
                    return "Pending Review";

                return IsCorrect ? "Correct" : "Wrong";
            }
        }

        /// <summary>
        /// Gets the trainee selection in consistent text form.
        /// </summary>
        public string UserAnswerText
        {
            get
            {
                return FormatAnswer(UserAnswer, "Unknown");
            }
        }

        /// <summary>
        /// Gets supported administrator truth without treating pending as GOOD or NG.
        /// </summary>
        public string CorrectAnswerText
        {
            get
            {
                if (!IsReviewed)
                    return "Pending";

                return FormatAnswer(CorrectAnswer.Value, "Unknown");
            }
        }

        /// <summary>
        /// Gets a clear review provenance message for the administrator queue.
        /// </summary>
        public string ReviewStatusText
        {
            get
            {
                if (!HasStableIdentity)
                    return "Stable image identity unavailable";

                if (!IsReviewed)
                    return "Administrator review required";

                if (IsAutoReviewed)
                    return "Auto reviewed from existing administrator image truth";

                if (IsManuallyReviewed)
                    return "Manually reviewed by administrator";

                return "Administrator reviewed";
            }
        }

        /// <summary>
        /// Gets a safe elapsed-time label.
        /// </summary>
        public string ElapsedTimeText
        {
            get
            {
                if (ElapsedSeconds < 0 ||
                    double.IsNaN(ElapsedSeconds) ||
                    double.IsInfinity(ElapsedSeconds))
                {
                    return "N/A";
                }

                return ElapsedSeconds.ToString("0.00") + " s";
            }
        }

        #endregion

        #region INotifyPropertyChanged

        /// <summary>
        /// Occurs when a selectable row property changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises a property-change notification.
        /// </summary>
        private void OnPropertyChanged(
            [CallerMemberName] string propertyName = "")
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                handler(
                    this,
                    new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns whether an answer enum is supported by the application.
        /// </summary>
        private static bool IsSupportedAnswer(QuizAnswerType answer)
        {
            return answer == QuizAnswerType.Good ||
                   answer == QuizAnswerType.Ng;
        }

        /// <summary>
        /// Formats one supported answer in database-compatible form.
        /// </summary>
        private static string FormatAnswer(
            QuizAnswerType answer,
            string fallback)
        {
            if (!IsSupportedAnswer(answer))
                return fallback;

            return answer.ToString().ToUpperInvariant();
        }

        #endregion
    }
}
