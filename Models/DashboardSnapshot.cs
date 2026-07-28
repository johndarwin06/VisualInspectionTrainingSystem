#region Namespaces

using System;
using System.Collections.Generic;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents one internally consistent administrator dashboard read.
    /// </summary>
    public class DashboardSnapshot
    {
        #region Constructors

        /// <summary>
        /// Initializes an empty dashboard snapshot.
        /// </summary>
        public DashboardSnapshot()
        {
            Metrics = new DashboardMetrics();
            RecentSessions = new List<DashboardSessionSummary>();
            ChartData = new AnalyticsChartData();
        }

        #endregion

        #region Snapshot Metadata

        /// <summary>
        /// Gets or sets the inclusive local start for the headline metrics.
        /// </summary>
        public DateTime DayStartInclusive { get; set; }

        /// <summary>
        /// Gets or sets the exclusive local end for the headline metrics.
        /// </summary>
        public DateTime DayEndExclusive { get; set; }

        /// <summary>
        /// Gets or sets the local time when the in-memory snapshot was constructed.
        /// </summary>
        public DateTime GeneratedAtLocal { get; set; }

        #endregion

        #region Snapshot Data

        /// <summary>
        /// Gets or sets the headline dashboard metrics.
        /// </summary>
        public DashboardMetrics Metrics { get; set; }

        /// <summary>
        /// Gets or sets recent sessions in deterministic descending order.
        /// </summary>
        public List<DashboardSessionSummary> RecentSessions { get; set; }

        /// <summary>
        /// Gets or sets the bounded daily chart data.
        /// </summary>
        public AnalyticsChartData ChartData { get; set; }

        #endregion
    }
}
