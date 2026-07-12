#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Summary values for the reports window.
    /// </summary>
    public class ReportSummary
    {
        public int SessionCount { get; set; }

        public int TotalQuestions { get; set; }

        public int CorrectAnswers { get; set; }

        public int WrongAnswers { get; set; }

        public int PendingAnswers { get; set; }

        public int ReviewedAnswers { get; set; }

        public int TraineeCount { get; set; }

        public decimal AverageAccuracy { get; set; }

        public DateTime? FirstSessionTime { get; set; }

        public DateTime? LastSessionTime { get; set; }
    }
}
