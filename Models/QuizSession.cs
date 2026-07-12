#region Namespaces

using System;
using System.Collections.Generic;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents one running quiz session.
    /// </summary>
    public class QuizSession
    {
        #region Constructor

        public QuizSession()
        {
            Images = new List<QuizImage>();
            Answers = new List<QuizAnswer>();
            Started = DateTime.Now;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Logged-in user.
        /// </summary>
        public User User { get; set; }

        /// <summary>
        /// Images participating in the quiz.
        /// </summary>
        public List<QuizImage> Images { get; }

        /// <summary>
        /// User answers.
        /// </summary>
        public List<QuizAnswer> Answers { get; }

        /// <summary>
        /// Current image index.
        /// </summary>
        public int CurrentIndex { get; set; }

        /// <summary>
        /// Quiz start time.
        /// </summary>
        public DateTime Started { get; }

        /// <summary>
        /// Quiz finish time.
        /// </summary>
        public DateTime Finished { get; set; }

        /// <summary>
        /// Returns current image.
        /// </summary>
        public QuizImage CurrentImage
        {
            get
            {
                if (CurrentIndex >= Images.Count)
                    return null;

                return Images[CurrentIndex];
            }
        }

        /// <summary>
        /// Total image count.
        /// </summary>
        public int TotalImages
        {
            get
            {
                return Images.Count;
            }
        }

        /// <summary>
        /// Progress text.
        /// </summary>
        public string Progress
        {
            get
            {
                return $"{CurrentIndex + 1} / {Images.Count}";
            }
        }

        #endregion
    }
}