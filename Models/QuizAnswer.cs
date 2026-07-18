#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents one answer given by the trainee
    /// during a training session.
    /// </summary>
    public class QuizAnswer
    {
        #region Identity

        /// <summary>
        /// Database identity.
        /// </summary>
        public int AnswerID { get; set; }

        /// <summary>
        /// Parent training session ID.
        /// </summary>
        public int SessionID { get; set; }

        /// <summary>
        /// Question number within the session.
        /// </summary>
        public int Sequence { get; set; }

        #endregion

        #region Image Information

        /// <summary>
        /// Image database ID.
        /// </summary>
        public int ImageID { get; set; }

        /// <summary>
        /// Image file name.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Full image path.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Employee number from the parent session.
        /// Used by the admin review screen.
        /// </summary>
        public string EmployeeNo { get; set; }

        #endregion

        #region User Answer

        /// <summary>
        /// User selected answer.
        /// Expected values:
        /// GOOD
        /// NG
        /// </summary>
        public QuizAnswerType UserAnswer { get; set; }

        #endregion

        #region Admin Answer

        /// <summary>
        /// Correct answer assigned by the administrator.
        /// Initially null until reviewed.
        /// </summary>
        public QuizAnswerType? CorrectAnswer { get; set; }

        #endregion

        #region Result

        /// <summary>
        /// Indicates whether the user's answer is correct.
        /// </summary>
        public bool IsCorrect { get; set; }

        #endregion

        #region Timing

        /// <summary>
        /// Time when the user submitted the answer.
        /// </summary>
        public DateTime AnswerTime { get; set; }

        /// <summary>
        /// Seconds taken to answer this question.
        /// </summary>
        public double ElapsedSeconds { get; set; }

        #endregion

        #region Helper Properties

        /// <summary>
        /// Returns true when the administrator
        /// has already assigned the correct answer.
        /// </summary>
        public bool IsReviewed
        {
            get
            {
                return CorrectAnswer.HasValue;
            }
        }

        /// <summary>
        /// Returns a readable status.
        /// </summary>
        public string ResultText
        {
            get
            {
                if (!IsReviewed)
                    return "Pending Review";

                return UserAnswer == CorrectAnswer.Value
                    ? "Correct"
                    : "Wrong";
            }
        }

        /// <summary>
        /// Returns the trainee selection in consistent result-display form.
        /// </summary>
        public string UserAnswerText
        {
            get
            {
                if (UserAnswer == QuizAnswerType.Good)
                    return "GOOD";

                if (UserAnswer == QuizAnswerType.Ng)
                    return "NG";

                return "Unknown";
            }
        }

        /// <summary>
        /// Returns reviewed truth without treating an unreviewed answer as GOOD or NG.
        /// </summary>
        public string CorrectAnswerText
        {
            get
            {
                if (!CorrectAnswer.HasValue)
                    return "Pending";

                if (CorrectAnswer.Value == QuizAnswerType.Good)
                    return "GOOD";

                if (CorrectAnswer.Value == QuizAnswerType.Ng)
                    return "NG";

                return "Unknown";
            }
        }

        /// <summary>
        /// Returns a safe elapsed-time label for result review.
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
    }
}
