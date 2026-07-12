#region Namespaces

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents one complete training session.
    /// This class owns the quiz lifecycle and statistics.
    /// </summary>
    public class TrainingSession
    {
        #region Constructor

        public TrainingSession()
        {
            Images = new List<QuizImage>();
            Answers = new List<QuizAnswer>();

            Started = DateTime.Now;
            CurrentIndex = 0;
        }

        #endregion

        #region Database

        /// <summary>
        /// Database Session ID.
        /// </summary>
        public int SessionID { get; set; }

        #endregion

        #region User

        /// <summary>
        /// Logged in user.
        /// </summary>
        public User User { get; set; }

        #endregion

        #region Images

        /// <summary>
        /// Images included in this session.
        /// </summary>
        public List<QuizImage> Images { get; }

        /// <summary>
        /// Current image index (0-based).
        /// </summary>
        public int CurrentIndex { get; private set; }

        /// <summary>
        /// Current image.
        /// </summary>
        public QuizImage CurrentImage
        {
            get
            {
                if (Images.Count == 0)
                    return null;

                if (CurrentIndex >= Images.Count)
                    return null;

                return Images[CurrentIndex];
            }
        }

        /// <summary>
        /// Total number of questions.
        /// </summary>
        public int TotalQuestions
        {
            get
            {
                return Images.Count;
            }
        }

        /// <summary>
        /// Current question number (1-based).
        /// </summary>
        public int CurrentQuestion
        {
            get
            {
                if (Images.Count == 0)
                    return 0;

                return CurrentIndex + 1;
            }
        }

        /// <summary>
        /// Progress text.
        /// </summary>
        public string Progress
        {
            get
            {
                if (Images.Count == 0)
                    return "0 / 0";

                return $"{CurrentQuestion} / {TotalQuestions}";
            }
        }

        #endregion

        #region Answers

        /// <summary>
        /// User answers.
        /// </summary>
        public List<QuizAnswer> Answers { get; }

        #endregion

        #region Statistics

        /// <summary>
        /// Number of answered questions.
        /// </summary>
        public int AnsweredQuestions
        {
            get
            {
                return Answers.Count;
            }
        }

        /// <summary>
        /// Number of correct answers.
        /// </summary>
        public int CorrectAnswers
        {
            get
            {
                return Answers.Count(a => a.IsCorrect);
            }
        }

        /// <summary>
        /// Number of wrong answers.
        /// </summary>
        public int WrongAnswers
        {
            get
            {
                return Answers.Count(a => !a.IsCorrect && a.IsReviewed);
            }
        }

        /// <summary>
        /// Accuracy percentage.
        /// </summary>
        public double Accuracy
        {
            get
            {
                if (AnsweredQuestions == 0)
                    return 0;

                return Math.Round(
                    (double)CorrectAnswers /
                    AnsweredQuestions * 100,
                    2);
            }
        }

        #endregion

        #region Time

        /// <summary>
        /// Training start time.
        /// </summary>
        public DateTime Started { get; }

        /// <summary>
        /// Training end time.
        /// </summary>
        public DateTime? Finished { get; private set; }

        /// <summary>
        /// Total duration.
        /// </summary>
        public TimeSpan Duration
        {
            get
            {
                if (Finished == null)
                    return DateTime.Now - Started;

                return Finished.Value - Started;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds an answer to the session.
        /// </summary>
        public void AddAnswer(QuizAnswer answer)
        {
            if (answer == null)
                throw new ArgumentNullException(nameof(answer));

            Answers.Add(answer);
        }

        /// <summary>
        /// Moves to the next image.
        /// </summary>
        public bool MoveNext()
        {
            if (CurrentIndex >= Images.Count)
                return false;

            CurrentIndex++;

            return CurrentIndex < Images.Count;
        }

        /// <summary>
        /// Determines whether the session is complete.
        /// </summary>
        public bool IsCompleted()
        {
            return CurrentIndex >= Images.Count;
        }

        /// <summary>
        /// Marks the session as completed.
        /// </summary>
        public void Finish()
        {
            Finished = DateTime.Now;
        }

        #endregion
    }
}