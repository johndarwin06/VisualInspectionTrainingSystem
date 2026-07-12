#region Namespaces

using System;
using System.Collections.Generic;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Handles the quiz business logic.
    /// This class contains NO UI code and NO database code.
    /// </summary>
    public class QuizEngine
    {
        #region Fields

        private readonly TrainingSession _session;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new quiz engine.
        /// </summary>
        public QuizEngine(
            User user,
            List<QuizImage> images)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (images == null)
                throw new ArgumentNullException(nameof(images));

            _session = new TrainingSession
            {
                User = user
            };

            _session.Images.AddRange(images);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Current training session.
        /// </summary>
        public TrainingSession Session
        {
            get
            {
                return _session;
            }
        }

        /// <summary>
        /// Current image.
        /// </summary>
        public QuizImage CurrentImage
        {
            get
            {
                return _session.CurrentImage;
            }
        }

        /// <summary>
        /// Current progress.
        /// Example:
        /// 5 / 100
        /// </summary>
        public string Progress
        {
            get
            {
                return _session.Progress;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Records the selected answer.
        /// </summary>
        public void SubmitAnswer(QuizAnswerType answer)
        {
            if (_session.IsCompleted())
                return;

            QuizImage image = _session.CurrentImage;

            QuizAnswer quizAnswer = new QuizAnswer
            {
                Sequence = _session.CurrentQuestion,

                ImageID = image.ImageID,

                FileName = image.FileName,

                FilePath = image.FilePath,

                UserAnswer = answer,

                // Filled later by Admin
                CorrectAnswer = null,

                // Unknown until review
                IsCorrect = false,

                AnswerTime = DateTime.Now,

                ElapsedSeconds = 0
            };

            _session.AddAnswer(quizAnswer);

            _session.MoveNext();

            if (_session.IsCompleted())
            {
                _session.Finish();
            }
        }

        /// <summary>
        /// Returns true if quiz has ended.
        /// </summary>
        public bool IsCompleted()
        {
            return _session.IsCompleted();
        }

        #endregion
    }
}