#region Namespaces

using System;
using System.Globalization;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Presents administrator dashboard charts from one internally consistent snapshot.
    /// </summary>
    public sealed class DashboardChartViewModel : AnalyticsChartViewModel
    {
        #region Fields

        private string _generatedAtText;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes an empty administrator dashboard chart presentation.
        /// </summary>
        public DashboardChartViewModel()
            : base()
        {
            _generatedAtText = string.Empty;
        }

        /// <summary>
        /// Initializes an administrator dashboard chart presentation from a snapshot.
        /// </summary>
        /// <param name="snapshot">The internally consistent dashboard snapshot.</param>
        public DashboardChartViewModel(DashboardSnapshot snapshot)
            : this()
        {
            UpdateSnapshot(snapshot);
        }

        #endregion

        #region Presentation Properties

        /// <summary>
        /// Gets the administrator-facing chart section title.
        /// </summary>
        public string Title
        {
            get { return "Training activity"; }
        }

        /// <summary>
        /// Gets the administrator-facing chart section description.
        /// </summary>
        public string Description
        {
            get { return "Daily completion, trainee selections, review coverage, accuracy, and time spent."; }
        }

        /// <summary>
        /// Gets a local-time freshness label for the source snapshot.
        /// </summary>
        public string GeneratedAtText
        {
            get { return _generatedAtText; }
            private set { SetProperty(ref _generatedAtText, value); }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Replaces the chart presentation from one dashboard snapshot.
        /// </summary>
        /// <param name="snapshot">The latest internally consistent snapshot.</param>
        public void UpdateSnapshot(DashboardSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Update(null);
                GeneratedAtText = string.Empty;
                return;
            }

            Update(snapshot.ChartData);
            GeneratedAtText = FormatGeneratedAt(snapshot.GeneratedAtLocal);
        }

        #endregion

        #region Formatting

        private static string FormatGeneratedAt(DateTime generatedAtLocal)
        {
            if (generatedAtLocal == DateTime.MinValue)
            {
                return string.Empty;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "Updated {0:g}",
                generatedAtLocal);
        }

        #endregion
    }
}
