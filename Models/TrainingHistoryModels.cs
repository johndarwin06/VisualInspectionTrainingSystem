#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Identifies a supported trainee history review-status filter.
    /// </summary>
    public enum TrainingHistoryReviewFilter
    {
        /// <summary>
        /// Includes every completed session owned by the current user.
        /// </summary>
        All,

        /// <summary>
        /// Includes sessions with no reviewed answers.
        /// </summary>
        PendingReview,

        /// <summary>
        /// Includes sessions containing both reviewed and pending answers.
        /// </summary>
        PartiallyReviewed,

        /// <summary>
        /// Includes sessions whose persisted answers are all reviewed.
        /// </summary>
        Reviewed
    }

    /// <summary>
    /// Defines one bounded trainee history request without accepting a user identity.
    /// </summary>
    public sealed class TrainingHistoryQuery
    {
        #region Properties

        /// <summary>
        /// Gets or sets optional session or image-name search text.
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        /// Gets or sets the optional inclusive completion-time boundary.
        /// </summary>
        public DateTime? StartInclusive { get; set; }

        /// <summary>
        /// Gets or sets the optional exclusive completion-time boundary.
        /// </summary>
        public DateTime? EndExclusive { get; set; }

        /// <summary>
        /// Gets or sets the supported review-status filter.
        /// </summary>
        public TrainingHistoryReviewFilter ReviewFilter { get; set; }

        /// <summary>
        /// Gets or sets the zero-based result offset.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of session rows to return.
        /// </summary>
        public int Limit { get; set; }

        #endregion
    }

    /// <summary>
    /// Represents one completed session in the current trainee's bounded history.
    /// </summary>
    public sealed class TrainingHistorySessionSummary
    {
        #region Stored Values

        /// <summary>
        /// Gets or sets the session identity used for authorized detail lookup.
        /// </summary>
        public int SessionID { get; set; }

        /// <summary>
        /// Gets or sets the local session start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the required completion time.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the persisted question count.
        /// </summary>
        public int TotalQuestions { get; set; }

        /// <summary>
        /// Gets or sets the number of persisted answer rows.
        /// </summary>
        public int AnswerCount { get; set; }

        /// <summary>
        /// Gets or sets normalized trainee GOOD selections.
        /// </summary>
        public int UserGoodAnswers { get; set; }

        /// <summary>
        /// Gets or sets normalized trainee NG selections.
        /// </summary>
        public int UserNgAnswers { get; set; }

        /// <summary>
        /// Gets or sets answers with supported GOOD or NG truth.
        /// </summary>
        public int ReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets answers without supported GOOD or NG truth.
        /// </summary>
        public int PendingAnswers { get; set; }

        /// <summary>
        /// Gets or sets reviewed answers matching a supported trainee answer.
        /// </summary>
        public int CorrectReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets reviewed answers that do not match the trainee answer.
        /// </summary>
        public int WrongReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets reviewed-only accuracy, or null when no reviewed denominator exists.
        /// </summary>
        public decimal? ReviewedAccuracy { get; set; }

        #endregion

        #region Display Values

        /// <summary>
        /// Gets the local completion date and time.
        /// </summary>
        public string CompletionTimeText
        {
            get { return EndTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); }
        }

        /// <summary>
        /// Gets a safe non-negative duration label.
        /// </summary>
        public string DurationText
        {
            get
            {
                if (EndTime < StartTime)
                    return "N/A";

                TimeSpan duration = EndTime - StartTime;

                if (duration.TotalHours >= 1)
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}h {1:00}m {2:00}s",
                        (int)duration.TotalHours,
                        duration.Minutes,
                        duration.Seconds);
                }

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}m {1:00}s",
                    (int)duration.TotalMinutes,
                    duration.Seconds);
            }
        }

        /// <summary>
        /// Gets reviewed-only accuracy or N/A for an empty denominator.
        /// </summary>
        public string ReviewedAccuracyText
        {
            get
            {
                return ReviewedAccuracy.HasValue
                    ? ReviewedAccuracy.Value.ToString("0.00", CultureInfo.InvariantCulture) + "%"
                    : "N/A";
            }
        }

        /// <summary>
        /// Gets the clear review state using both text and aggregate values.
        /// </summary>
        public string ReviewStatusText
        {
            get
            {
                if (ReviewedAnswers <= 0)
                    return "Pending Review";

                if (AnswerCount > 0 && PendingAnswers <= 0)
                    return "Reviewed";

                return "Partially Reviewed";
            }
        }

        /// <summary>
        /// Gets a concise accessible answer summary.
        /// </summary>
        public string AnswerSummaryText
        {
            get
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "GOOD {0}, NG {1}, reviewed {2}, pending {3}, correct {4}, wrong {5}",
                    UserGoodAnswers,
                    UserNgAnswers,
                    ReviewedAnswers,
                    PendingAnswers,
                    CorrectReviewedAnswers,
                    WrongReviewedAnswers);
            }
        }

        #endregion
    }

    /// <summary>
    /// Carries one deterministic bounded page of current-user session summaries.
    /// </summary>
    public sealed class TrainingHistoryPage
    {
        #region Constructors

        /// <summary>
        /// Initializes a bounded history page.
        /// </summary>
        /// <param name="sessions">Newest-first page rows.</param>
        /// <param name="hasMore">Whether another page is available.</param>
        public TrainingHistoryPage(
            IEnumerable<TrainingHistorySessionSummary> sessions,
            bool hasMore)
        {
            Sessions = new ReadOnlyCollection<TrainingHistorySessionSummary>(
                (sessions ?? Enumerable.Empty<TrainingHistorySessionSummary>())
                    .Where(session => session != null)
                    .ToList());
            HasMore = hasMore;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the deterministic newest-first page rows.
        /// </summary>
        public ReadOnlyCollection<TrainingHistorySessionSummary> Sessions
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets whether a subsequent bounded page exists.
        /// </summary>
        public bool HasMore
        {
            get;
            private set;
        }

        #endregion
    }

    /// <summary>
    /// Represents one read-only answer in an authorized trainee session detail.
    /// </summary>
    public sealed class TrainingHistoryAnswerDetail
    {
        #region Stored Values

        /// <summary>
        /// Gets or sets the answer identity.
        /// </summary>
        public int AnswerID { get; set; }

        /// <summary>
        /// Gets or sets the authorized parent session identity.
        /// </summary>
        public int SessionID { get; set; }

        /// <summary>
        /// Gets or sets the deterministic one-based question number.
        /// </summary>
        public int QuestionNumber { get; set; }

        /// <summary>
        /// Gets or sets the safe display filename.
        /// </summary>
        public string ImageFileName { get; set; }

        /// <summary>
        /// Gets or sets the shortened non-sensitive image identity.
        /// </summary>
        public string ShortImageIdentifier { get; set; }

        /// <summary>
        /// Gets or sets normalized trainee answer text.
        /// </summary>
        public string UserAnswerText { get; set; }

        /// <summary>
        /// Gets or sets normalized reviewed truth text, or Pending.
        /// </summary>
        public string CorrectAnswerText { get; set; }

        /// <summary>
        /// Gets or sets Pending, Correct, or Wrong.
        /// </summary>
        public string OutcomeText { get; set; }

        /// <summary>
        /// Gets or sets Pending, Automatic Review, or Administrator Review provenance.
        /// </summary>
        public string ReviewSourceText { get; set; }

        /// <summary>
        /// Gets or sets whether this answer uses supported reviewed truth.
        /// </summary>
        public bool IsReviewed { get; set; }

        /// <summary>
        /// Gets or sets whether the supported trainee answer matches truth.
        /// </summary>
        public bool IsCorrect { get; set; }

        /// <summary>
        /// Gets or sets the optional valid elapsed seconds.
        /// </summary>
        public double? ElapsedSeconds { get; set; }

        #endregion

        #region Display Values

        /// <summary>
        /// Gets a filename or non-sensitive identifier without exposing a path.
        /// </summary>
        public string ImageDisplayText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ImageFileName))
                    return ImageFileName;

                return string.IsNullOrWhiteSpace(ShortImageIdentifier)
                    ? "Image unavailable"
                    : "Image " + ShortImageIdentifier;
            }
        }

        /// <summary>
        /// Gets a safe elapsed-time label.
        /// </summary>
        public string ElapsedTimeText
        {
            get
            {
                return ElapsedSeconds.HasValue
                    ? ElapsedSeconds.Value.ToString("0.00", CultureInfo.InvariantCulture) + " s"
                    : "N/A";
            }
        }

        #endregion
    }

    /// <summary>
    /// Carries one authorized session summary and its read-only answer rows.
    /// </summary>
    public sealed class TrainingHistorySessionDetail
    {
        #region Constructors

        /// <summary>
        /// Initializes one immutable session-detail snapshot.
        /// </summary>
        /// <param name="summary">Authorized session summary.</param>
        /// <param name="answers">Deterministically ordered answer rows.</param>
        public TrainingHistorySessionDetail(
            TrainingHistorySessionSummary summary,
            IEnumerable<TrainingHistoryAnswerDetail> answers)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            Summary = summary;
            Answers = new ReadOnlyCollection<TrainingHistoryAnswerDetail>(
                (answers ?? Enumerable.Empty<TrainingHistoryAnswerDetail>())
                    .Where(answer => answer != null)
                    .ToList());
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the authorized session summary.
        /// </summary>
        public TrainingHistorySessionSummary Summary
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the read-only ordered answer rows.
        /// </summary>
        public ReadOnlyCollection<TrainingHistoryAnswerDetail> Answers
        {
            get;
            private set;
        }

        #endregion
    }
}
