#region Namespaces

using System;
using System.Collections.Generic;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Calculates read-only quiz result statistics from a safe answer snapshot.
    /// </summary>
    public class StatisticsService
    {
        #region Public Methods

        /// <summary>
        /// Clones the supplied answers and calculates distribution, review, timing, and NG metrics.
        /// </summary>
        /// <param name="answers">Answers to snapshot. Null collections and elements are ignored safely.</param>
        /// <returns>Statistics that cannot be changed by later caller-list mutations.</returns>
        public ResultStatistics Calculate(IEnumerable<QuizAnswer> answers)
        {
            List<QuizAnswer> snapshot = CreateSnapshot(answers);
            ResultStatistics statistics = new ResultStatistics(snapshot)
            {
                TotalQuestions = snapshot.Count
            };
            double validElapsedTotal = 0;
            double fastestElapsed = double.MaxValue;
            double slowestElapsed = 0;

            foreach (QuizAnswer answer in snapshot)
            {
                bool hasValidUserAnswer = IsValidAnswer(answer.UserAnswer);
                bool isReviewed = answer.CorrectAnswer.HasValue &&
                                  IsValidAnswer(answer.CorrectAnswer.Value);

                if (hasValidUserAnswer)
                {
                    if (answer.UserAnswer == QuizAnswerType.Good)
                    {
                        statistics.UserGoodAnswers++;
                    }
                    else if (answer.UserAnswer == QuizAnswerType.Ng)
                    {
                        statistics.UserNgAnswers++;
                    }
                }

                if (isReviewed)
                {
                    CalculateReviewedMetrics(
                        statistics,
                        answer,
                        hasValidUserAnswer);
                }

                if (IsValidElapsed(answer.ElapsedSeconds))
                {
                    double candidateTotal = validElapsedTotal + answer.ElapsedSeconds;

                    if (!double.IsInfinity(candidateTotal) &&
                        !double.IsNaN(candidateTotal))
                    {
                        validElapsedTotal = candidateTotal;
                        statistics.ValidTimingAnswers++;
                        fastestElapsed = Math.Min(
                            fastestElapsed,
                            answer.ElapsedSeconds);
                        slowestElapsed = Math.Max(
                            slowestElapsed,
                            answer.ElapsedSeconds);
                    }
                }
            }

            statistics.PendingReviewAnswers = Math.Max(
                0,
                statistics.TotalQuestions - statistics.ReviewedAnswers);
            statistics.GoodPercentage = CalculatePercentage(
                statistics.UserGoodAnswers,
                statistics.TotalQuestions);
            statistics.NgPercentage = CalculatePercentage(
                statistics.UserNgAnswers,
                statistics.TotalQuestions);
            statistics.UserNgRatePercentage = statistics.NgPercentage;
            statistics.ReviewCoveragePercentage = CalculatePercentage(
                statistics.ReviewedAnswers,
                statistics.TotalQuestions);
            statistics.ReviewedAccuracyPercentage = CalculatePercentage(
                statistics.CorrectReviewedAnswers,
                statistics.ReviewedAnswers);
            statistics.NgDetectionRatePercentage = CalculatePercentage(
                statistics.CorrectlyDetectedNgAnswers,
                statistics.ReviewedActualNgAnswers);
            statistics.FalseNgRatePercentage = CalculatePercentage(
                statistics.FalseNgAnswers,
                statistics.ReviewedActualGoodAnswers);
            statistics.TotalElapsedSeconds = Math.Round(
                validElapsedTotal,
                2);
            statistics.AverageElapsedSeconds = statistics.ValidTimingAnswers == 0
                ? 0
                : Math.Round(
                    validElapsedTotal / statistics.ValidTimingAnswers,
                    2);
            statistics.FastestElapsedSeconds = statistics.ValidTimingAnswers == 0
                ? 0
                : Math.Round(
                    fastestElapsed,
                    2);
            statistics.SlowestElapsedSeconds = statistics.ValidTimingAnswers == 0
                ? 0
                : Math.Round(
                    slowestElapsed,
                    2);

            return statistics;
        }

        #endregion

        #region Calculation Helpers

        /// <summary>
        /// Adds reviewed accuracy and NG-classification values for one answer.
        /// </summary>
        private static void CalculateReviewedMetrics(
            ResultStatistics statistics,
            QuizAnswer answer,
            bool hasValidUserAnswer)
        {
            QuizAnswerType correctAnswer = answer.CorrectAnswer.Value;
            bool isCorrect = hasValidUserAnswer &&
                             answer.UserAnswer == correctAnswer;

            statistics.ReviewedAnswers++;

            if (isCorrect)
            {
                statistics.CorrectReviewedAnswers++;
            }
            else
            {
                statistics.WrongReviewedAnswers++;
            }

            if (correctAnswer == QuizAnswerType.Ng)
            {
                statistics.ReviewedActualNgAnswers++;

                if (hasValidUserAnswer &&
                    answer.UserAnswer == QuizAnswerType.Ng)
                {
                    statistics.CorrectlyDetectedNgAnswers++;
                }
                else if (hasValidUserAnswer &&
                         answer.UserAnswer == QuizAnswerType.Good)
                {
                    statistics.MissedNgAnswers++;
                }
            }
            else if (correctAnswer == QuizAnswerType.Good)
            {
                statistics.ReviewedActualGoodAnswers++;

                if (hasValidUserAnswer &&
                    answer.UserAnswer == QuizAnswerType.Ng)
                {
                    statistics.FalseNgAnswers++;
                }
            }
        }

        /// <summary>
        /// Calculates a bounded percentage with a safe zero denominator.
        /// </summary>
        private static double CalculatePercentage(
            int numerator,
            int denominator)
        {
            if (numerator <= 0 ||
                denominator <= 0)
            {
                return 0;
            }

            double percentage = (double)numerator / denominator * 100;

            return Math.Round(
                Math.Min(
                    100,
                    Math.Max(0, percentage)),
                2);
        }

        /// <summary>
        /// Returns whether an answer enum contains a supported GOOD or NG value.
        /// </summary>
        private static bool IsValidAnswer(QuizAnswerType answer)
        {
            return Enum.IsDefined(
                typeof(QuizAnswerType),
                answer);
        }

        /// <summary>
        /// Returns whether elapsed time is finite and non-negative.
        /// </summary>
        private static bool IsValidElapsed(double elapsedSeconds)
        {
            return elapsedSeconds >= 0 &&
                   !double.IsNaN(elapsedSeconds) &&
                   !double.IsInfinity(elapsedSeconds);
        }

        #endregion

        #region Snapshot Helpers

        /// <summary>
        /// Creates independent answer objects so later caller mutations cannot change this result.
        /// </summary>
        private static List<QuizAnswer> CreateSnapshot(
            IEnumerable<QuizAnswer> answers)
        {
            List<QuizAnswer> snapshot = new List<QuizAnswer>();

            if (answers == null)
            {
                return snapshot;
            }

            foreach (QuizAnswer answer in answers)
            {
                if (answer != null)
                {
                    snapshot.Add(CloneAnswer(answer));
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Clones all persisted and display-relevant fields from one answer.
        /// </summary>
        private static QuizAnswer CloneAnswer(QuizAnswer answer)
        {
            return new QuizAnswer
            {
                AnswerID = answer.AnswerID,
                SessionID = answer.SessionID,
                Sequence = answer.Sequence,
                ImageID = answer.ImageID,
                FileName = answer.FileName,
                FilePath = answer.FilePath,
                EmployeeNo = answer.EmployeeNo,
                UserAnswer = answer.UserAnswer,
                CorrectAnswer = answer.CorrectAnswer,
                IsCorrect = answer.IsCorrect,
                AnswerTime = answer.AnswerTime,
                ElapsedSeconds = answer.ElapsedSeconds
            };
        }

        #endregion
    }
}
