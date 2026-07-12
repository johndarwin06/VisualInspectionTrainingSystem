#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Recent training-session row shown on the dashboard.
    /// </summary>
    public class DashboardSessionSummary
    {
        public int SessionID { get; set; }

        public string EmployeeNo { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public int TotalQuestions { get; set; }

        public int CorrectAnswers { get; set; }

        public int WrongAnswers { get; set; }

        public decimal Accuracy { get; set; }

        public string Status
        {
            get
            {
                return EndTime.HasValue ? "Completed" : "Open";
            }
        }
    }
}
