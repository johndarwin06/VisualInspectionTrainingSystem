#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Summary values shown on the admin dashboard.
    /// </summary>
    public class DashboardMetrics
    {
        public int TotalSessions { get; set; }

        public int TotalAnswers { get; set; }

        public int ReviewedAnswers { get; set; }

        public int PendingAnswers { get; set; }

        public int ActiveTrainees { get; set; }

        public decimal AverageAccuracy { get; set; }

        public DateTime? LatestSessionTime { get; set; }
    }
}
