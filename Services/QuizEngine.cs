#region Namespaces

using System;
using System.Collections.Generic;
using System.Diagnostics;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Handles quiz flow and answer recording.
    /// This class contains no UI, navigation, database, or WPF code.
    /// </summary>
    public class QuizEngine
    {
        #region Fields

        private readonly object _syncRoot;

        private readonly TrainingSession _session;

        private readonly HashSet<int> _submittedSequences;

        private readonly Stopwatch _questionTimer;

        private bool _isCompleted;

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

            _syncRoot = new object();

            _submittedSequences = new HashSet<int>();

            _questionTimer = new Stopwatch();

            _session = CreateSession(user, images);

            if (_session.IsCompleted())
            {
                CompleteSession();
            }
            else
            {
                StartQuestionTimer();
            }
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
        /// Total number of questions.
        /// </summary>
        public int TotalQuestions
        {
            get
            {
                return _session.TotalQuestions;
            }
        }

        /// <summary>
        /// Current question number.
        /// </summary>
        public int CurrentQuestion
        {
            get
            {
                return _session.CurrentQuestion;
            }
        }

        /// <summary>
        /// Returns true when the current question can accept an answer.
        /// </summary>
        public bool CanSubmitAnswer
        {
            get
            {
                lock (_syncRoot)
                {
                    return CanSubmitAnswerCore();
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Records the selected answer.
        /// Duplicate submissions and submissions after completion are ignored.
        /// </summary>
        public void SubmitAnswer(QuizAnswerType answer)
        {
            TrySubmitAnswer(answer);
        }

        /// <summary>
        /// Attempts to record the selected answer.
        /// Returns false when the quiz is complete or the current question was already answered.
        /// </summary>
        public bool TrySubmitAnswer(QuizAnswerType answer)
        {
            ValidateAnswer(answer);

            lock (_syncRoot)
            {
                if (!CanSubmitAnswerCore())
                    return false;

                QuizImage image = _session.CurrentImage;

                int sequence = _session.CurrentQuestion;

                QuizAnswer quizAnswer = CreateAnswer(
                    sequence,
                    image,
                    answer);

                _session.AddAnswer(quizAnswer);

                _submittedSequences.Add(sequence);

                _session.MoveNext();

                if (_session.IsCompleted())
                {
                    CompleteSession();
                }
                else
                {
                    StartQuestionTimer();
                }

                return true;
            }
        }

        /// <summary>
        /// Returns true if quiz has ended.
        /// </summary>
        public bool IsCompleted()
        {
            lock (_syncRoot)
            {
                return _isCompleted || _session.IsCompleted();
            }
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
        /// Creates an answer for the current question.
        /// </summary>
        private QuizAnswer CreateAnswer(
            int sequence,
            QuizImage image,
            QuizAnswerType answer)
        {
            return new QuizAnswer
            {
                Sequence = sequence,

                ImageID = image.ImageID,

                FileName = image.FileName,

                FilePath = image.FilePath,

                UserAnswer = answer,

                CorrectAnswer = null,

                IsCorrect = false,

                AnswerTime = DateTime.Now,

                ElapsedSeconds = GetElapsedSeconds()
            };
        }

        /// <summary>
        /// Returns true when an answer may be recorded for the current question.
        /// </summary>
        private bool CanSubmitAnswerCore()
        {
            if (_isCompleted)
                return false;

            if (_session.IsCompleted())
                return false;

            if (_session.CurrentImage == null)
                return false;

            return !HasAnswerForCurrentQuestion();
        }

        /// <summary>
        /// Returns true when the current question already has an answer.
        /// </summary>
        private bool HasAnswerForCurrentQuestion()
        {
            int sequence = _session.CurrentQuestion;

            if (_submittedSequences.Contains(sequence))
                return true;

            foreach (QuizAnswer answer in _session.Answers)
            {
                if (answer != null &&
                    answer.Sequence == sequence)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Starts timing the active question.
        /// </summary>
        private void StartQuestionTimer()
        {
            _questionTimer.Reset();

            _questionTimer.Start();
        }

        /// <summary>
        /// Returns elapsed seconds for the active question.
        /// </summary>
        private double GetElapsedSeconds()
        {
            TimeSpan elapsed = _questionTimer.Elapsed;

            if (elapsed.TotalSeconds < 0)
                return 0;

            return Math.Round(
                elapsed.TotalSeconds,
                2);
        }

        /// <summary>
        /// Marks the session complete once.
        /// </summary>
        private void CompleteSession()
        {
            if (_isCompleted)
                return;

            _questionTimer.Stop();

            _session.Finish();

            _isCompleted = true;
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
