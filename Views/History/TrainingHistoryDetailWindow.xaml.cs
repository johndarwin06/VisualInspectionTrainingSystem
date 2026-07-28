#region Namespaces

using System;
using System.Windows;
using MahApps.Metro.Controls;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.History
{
    /// <summary>
    /// Hosts one authorized read-only trainee result and disposes active background work on close.
    /// </summary>
    public partial class TrainingHistoryDetailWindow : MetroWindow
    {
        #region Fields

        private readonly TrainingHistoryDetailViewModel _viewModel;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates one current-user result window.
        /// </summary>
        /// <param name="sessionId">Session identity checked by the current-user service.</param>
        public TrainingHistoryDetailWindow(int sessionId)
        {
            InitializeComponent();
            _viewModel = new TrainingHistoryDetailViewModel(sessionId);
            DataContext = _viewModel;
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Cancels database and image work before releasing the window.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _viewModel.Dispose();
            DataContext = null;
            base.OnClosed(e);
        }

        #endregion
    }
}
