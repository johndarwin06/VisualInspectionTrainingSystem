#region Namespaces

using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Repositories
{
    /// <summary>
    /// Defines complete and display-bounded report snapshot loading.
    /// </summary>
    public interface IReportRepository
    {
        #region Snapshot Loading

        /// <summary>
        /// Loads an interactive report snapshot with disclosed display bounding.
        /// </summary>
        /// <param name="period">The half-open report period.</param>
        /// <returns>The interactive report snapshot.</returns>
        ReportSnapshot GetDisplaySnapshot(ReportPeriod period);

        /// <summary>
        /// Loads the complete export snapshot subject to the export safeguard.
        /// </summary>
        /// <param name="period">The half-open report period.</param>
        /// <returns>The complete report snapshot, or a snapshot marked over-limit.</returns>
        ReportSnapshot GetExportSnapshot(ReportPeriod period);

        #endregion
    }
}
