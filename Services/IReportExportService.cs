#region Namespaces

using System.Threading;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Defines file generation for complete report snapshots.
    /// </summary>
    public interface IReportExportService
    {
        #region Export Methods

        /// <summary>
        /// Writes a UTF-8 CSV report.
        /// </summary>
        void ExportCsv(
            ReportSnapshot snapshot,
            string filePath,
            CancellationToken cancellationToken);

        /// <summary>
        /// Writes a real Office Open XML workbook.
        /// </summary>
        void ExportExcel(
            ReportSnapshot snapshot,
            string filePath,
            CancellationToken cancellationToken);

        /// <summary>
        /// Writes a paginated PDF report.
        /// </summary>
        void ExportPdf(
            ReportSnapshot snapshot,
            string filePath,
            CancellationToken cancellationToken);

        #endregion
    }
}
