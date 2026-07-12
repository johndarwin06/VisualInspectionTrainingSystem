#region Namespaces

using System;
using System.Collections.Generic;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Handles the quiz business logic.
    /// This class contains no UI code and no database code.
    /// </summary>
    public class QuizEngine
    {
        #region Fields

        private readonly TrainingSession _session;

        private DateTime _questionStarted;

        private bool _sessionFinished;

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

            _session = CreateSession(user, images);

            _questionStarted = DateTime.Now;

            FinishIfCompleted();
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

        /// <summary>
        /// Returns the number of questions in the session.
        /// </summary>
        public int TotalQuestions
        {
            get
            {
                return _session.TotalQuestions;
            }
        }

        /// <summary>
        /// Returns the current question number.
        /// </summary>
        public int CurrentQuestion
        {
            get
            {
                return _session.CurrentQuestion;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Records the selected answer.
        /// </summary>
        public void SubmitAnswer(QuizAnswerType answer)
        {
            ValidateAnswer(answer);

            if (IsCompleted())
                return;

            QuizImage image = CurrentImage;

            if (image == null)
            {
                FinishIfCompleted();
                return;
            }

            _session.AddAnswer(CreateAnswer(image, answer));

            _session.MoveNext();

            if (!FinishIfCompleted())
            {
                _questionStarted = DateTime.Now;
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

        #region Private Methods

        /// <summary>
        /// Creates the training session owned by this engine.
        /// </summary>
        private static TrainingSession CreateSession(
            User user,
            IEnumerable<QuizImage> images)
        {
            TrainingSession session = new TrainingSession
            {
                User = user
            };

            foreach (QuizImage image in images)
            {
                if (image != null)
                {
                    session.Images.Add(image);
                }
            }

            return session;
        }

        /// <summary>
        /// Creates an answer record for the current image.
        /// </summary>
        private QuizAnswer CreateAnswer(
            QuizImage image,
            QuizAnswerType answer)
        {
            DateTime answeredAt = DateTime.Now;

            return new QuizAnswer
            {
                Sequence = _session.CurrentQuestion,

                ImageID = image.ImageID,

                FileName = image.FileName,

                FilePath = image.FilePath,

                UserAnswer = answer,

                CorrectAnswer = null,

                IsCorrect = false,

                AnswerTime = answeredAt,

                ElapsedSeconds = Math.Round(
                    (answeredAt - _questionStarted).TotalSeconds,
                    2)
            };
        }

        /// <summary>
        /// Finishes the session once, when all questions are complete.
        /// </summary>
        private bool FinishIfCompleted()
        {
            if (!IsCompleted())
                return false;

            if (!_sessionFinished)
            {
                _session.Finish();

                _sessionFinished = true;
            }

            return true;
        }

        /// <summary>
        /// Validates the selected answer.
        /// </summary>
        private static void ValidateAnswer(QuizAnswerType answer)
        {
            if (!Enum.IsDefined(typeof(QuizAnswerType), answer))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(answer),
                    answer,
                    "Unsupported quiz answer.");
            }
        }

        #endregion
    }
}
