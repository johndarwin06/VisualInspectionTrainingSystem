#region Namespaces

using System.Collections.Generic;
using System.Collections.ObjectModel;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents immutable result calculations for one safe answer snapshot.
    /// </summary>
    public sealed class ResultStatistics
    {
        #region Constructor

        /// <summary>
        /// Creates statistics for the supplied cloned answers.
        /// </summary>
        /// <param name="answers">The answer snapshot owned by these statistics.</param>
        internal ResultStatistics(IList<QuizAnswer> answers)
        {
            List<QuizAnswer> snapshot = answers == null
                ? new List<QuizAnswer>()
                : new List<QuizAnswer>(answers);

            Answers = new ReadOnlyCollection<QuizAnswer>(snapshot);
        }

        #endregion

        #region Snapshot

        /// <summary>
        /// Gets the cloned answers used for every result calculation.
        /// </summary>
        public ReadOnlyCollection<QuizAnswer> Answers { get; private set; }

        #endregion

        #region Answer Distribution

        /// <summary>
        /// Gets the number of non-null answer rows.
        /// </summary>
        public int TotalQuestions { get; internal set; }

        /// <summary>
        /// Gets the number of trainee GOOD selections.
        /// </summary>
        public int UserGoodAnswers { get; internal set; }

        /// <summary>
        /// Gets the number of trainee NG selections.
        /// </summary>
        public int UserNgAnswers { get; internal set; }

        /// <summary>
        /// Gets the percentage of all answers selected as GOOD.
        /// </summary>
        public double GoodPercentage { get; internal set; }

        /// <summary>
        /// Gets the percentage of all answers selected as NG.
        /// </summary>
        public double NgPercentage { get; internal set; }

        #endregion

        #region Review Coverage And Accuracy

        /// <summary>
        /// Gets the number of answers with administrator-provided truth.
        /// </summary>
        public int ReviewedAnswers { get; internal set; }

        /// <summary>
        /// Gets the number of answers still awaiting administrator review.
        /// </summary>
        public int PendingReviewAnswers { get; internal set; }

        /// <summary>
        /// Gets reviewed answers as a percentage of all answers.
        /// </summary>
        public double ReviewCoveragePercentage { get; internal set; }

        /// <summary>
        /// Gets reviewed answers where trainee selection equals reviewed truth.
        /// </summary>
        public int CorrectReviewedAnswers { get; internal set; }

        /// <summary>
        /// Gets reviewed answers where trainee selection differs from reviewed truth.
        /// </summary>
        public int WrongReviewedAnswers { get; internal set; }

        /// <summary>
        /// Gets accuracy among reviewed answers only.
        /// </summary>
        public double ReviewedAccuracyPercentage { get; internal set; }

        #endregion

        #region Timing

        /// <summary>
        /// Gets the number of answers with a valid, finite, non-negative elapsed value.
        /// </summary>
        public int ValidTimingAnswers { get; internal set; }

        /// <summary>
        /// Gets the sum of valid elapsed values in seconds.
        /// </summary>
        public double TotalElapsedSeconds { get; internal set; }

        /// <summary>
        /// Gets the average of valid elapsed values in seconds.
        /// </summary>
        public double AverageElapsedSeconds { get; internal set; }

        /// <summary>
        /// Gets the fastest valid elapsed value in seconds.
        /// </summary>
        public double FastestElapsedSeconds { get; internal set; }

        /// <summary>
        /// Gets the slowest valid elapsed value in seconds.
        /// </summary>
        public double SlowestElapsedSeconds { get; internal set; }

        #endregion

        #region NG Analysis

        /// <summary>
        /// Gets trainee NG selections as a percentage of all answers.
        /// </summary>
        public double UserNgRatePercentage { get; internal set; }

        /// <summary>
        /// Gets reviewed answers whose administrator truth is NG.
        /// </summary>
        public int ReviewedActualNgAnswers { get; internal set; }

        /// <summary>
        /// Gets reviewed answers whose administrator truth is GOOD.
        /// </summary>
        public int ReviewedActualGoodAnswers { get; internal set; }

        /// <summary>
        /// Gets answers where both the trainee selection and reviewed truth are NG.
        /// </summary>
        public int CorrectlyDetectedNgAnswers { get; internal set; }

        /// <summary>
        /// Gets trainee NG selections whose reviewed truth is GOOD.
        /// </summary>
        public int FalseNgAnswers { get; internal set; }

        /// <summary>
        /// Gets trainee GOOD selections whose reviewed truth is NG.
        /// </summary>
        public int MissedNgAnswers { get; internal set; }

        /// <summary>
        /// Gets correctly detected NG as a percentage of reviewed actual NG.
        /// </summary>
        public double NgDetectionRatePercentage { get; internal set; }

        /// <summary>
        /// Gets false NG as a percentage of reviewed actual GOOD.
        /// </summary>
        public double FalseNgRatePercentage { get; internal set; }

        #endregion
    }
}
