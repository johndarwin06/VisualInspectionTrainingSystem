#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Session-level row displayed in the reports window.
    /// </summary>
    public class ReportSessionRow
    {
        public int SessionID { get; set; }

        public string EmployeeNo { get; set; }

        public string FullName { get; set; }

        public string Department { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public int TotalQuestions { get; set; }

        public int CorrectAnswers { get; set; }

        public int WrongAnswers { get; set; }

        public int PendingAnswers { get; set; }

        public int ReviewedAnswers { get; set; }

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
