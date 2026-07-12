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

                return IsCorrect ? "Correct" : "Wrong";
            }
        }

        #endregion
    }
}