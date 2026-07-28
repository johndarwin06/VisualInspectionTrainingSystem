#region Namespaces

using System;
using System.Collections.Generic;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Contains one bounded, chart-neutral daily analytics series.
    /// </summary>
    public class AnalyticsChartData
    {
        #region Constructors

        /// <summary>
        /// Initializes an empty analytics series.
        /// </summary>
        public AnalyticsChartData()
        {
            DailyPoints = new List<ChartPoint>();
            IsAvailable = true;
            UnavailableReason = string.Empty;
        }

        #endregion

        #region Range

        /// <summary>
        /// Gets or sets the inclusive local start boundary for the series.
        /// </summary>
        public DateTime RangeStartInclusive { get; set; }

        /// <summary>
        /// Gets or sets the exclusive local end boundary for the series.
        /// </summary>
        public DateTime RangeEndExclusive { get; set; }

        /// <summary>
        /// Gets or sets the zero-filled daily points in ascending date order.
        /// </summary>
        public List<ChartPoint> DailyPoints { get; set; }

        /// <summary>
        /// Gets or sets whether the selected range has bounded chart data.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets a non-sensitive explanation when chart data is unavailable.
        /// </summary>
        public string UnavailableReason { get; set; }

        #endregion

        #region Aggregate Review Values

        /// <summary>
        /// Gets or sets the reviewed-answer total across the bounded range.
        /// </summary>
        public int ReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets the correct reviewed-answer total across the bounded range.
        /// </summary>
        public int CorrectReviewedAnswers { get; set; }

        /// <summary>
        /// Gets or sets the pending-answer total across the bounded range.
        /// </summary>
        public int PendingAnswers { get; set; }

        /// <summary>
        /// Gets reviewed answers that do not match a supported trainee selection.
        /// </summary>
        public int WrongReviewedAnswers
        {
            get
            {
                return Math.Max(
                    0,
                    ReviewedAnswers - CorrectReviewedAnswers);
            }
        }

        /// <summary>
        /// Gets reviewed-only accuracy, or null when no answer is reviewed.
        /// </summary>
        public decimal? ReviewedAccuracyPercent
        {
            get
            {
                if (ReviewedAnswers == 0)
                {
                    return null;
                }

                return Math.Round(
                    CorrectReviewedAnswers * 100m / ReviewedAnswers,
                    2,
                    MidpointRounding.AwayFromZero);
            }
        }

        #endregion
    }
}
