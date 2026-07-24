#region Namespaces

using System;
using System.Collections.Generic;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Immutable-in-use report data loaded for display or document export.
    /// </summary>
    public sealed class ReportSnapshot
    {
        #region Constructors

        /// <summary>
        /// Initializes an empty snapshot.
        /// </summary>
        public ReportSnapshot()
        {
            Summary = new ReportSummary();
            Sessions = new List<ReportSessionRow>();
            GeneratedAtLocal = DateTime.Now;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the period represented by this snapshot.
        /// </summary>
        public ReportPeriod Period { get; set; }

        /// <summary>
        /// Gets or sets the aggregate report values.
        /// </summary>
        public ReportSummary Summary { get; set; }

        /// <summary>
        /// Gets or sets the deterministically ordered session rows.
        /// </summary>
        public List<ReportSessionRow> Sessions { get; set; }

        /// <summary>
        /// Gets or sets when the snapshot was generated in local time.
        /// </summary>
        public DateTime GeneratedAtLocal { get; set; }

        /// <summary>
        /// Gets or sets whether the interactive row set omits additional matching rows.
        /// </summary>
        public bool IsDisplayLimited { get; set; }

        /// <summary>
        /// Gets or sets whether the selection exceeds the documented export safeguard.
        /// </summary>
        public bool IsExportLimitExceeded { get; set; }

        #endregion
    }
}
