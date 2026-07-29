#region Namespaces

using System;
using System.Windows;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.ViewModels;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

#endregion

namespace VisualInspectionTrainingSystem.Views.History
{
    /// <summary>
    /// Hosts current-user training history and owns single-flight result-detail navigation.
    /// </summary>
    public partial class TrainingHistoryWindow : FluentWindow
    {
        #region Fields

        private readonly TrainingHistoryViewModel _viewModel;
        private TrainingHistoryDetailWindow _activeDetailWindow;
        private bool _isClosing;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates history state and attaches read-only detail navigation.
        /// </summary>
        public TrainingHistoryWindow()
        {
            InitializeComponent();
            _viewModel = new TrainingHistoryViewModel();
            _viewModel.OpenSessionRequested += ViewModel_OpenSessionRequested;
            DataContext = _viewModel;
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Opens at most one read-only result and hides history while it is active.
        /// </summary>
        private void ViewModel_OpenSessionRequested(
            TrainingHistorySessionSummary session)
        {
            if (_isClosing || session == null)
                return;

            if (_activeDetailWindow != null)
            {
                if (_activeDetailWindow.IsVisible)
                    _activeDetailWindow.Activate();

                return;
            }

            TrainingHistoryDetailWindow detailWindow = null;

            try
            {
                detailWindow = new TrainingHistoryDetailWindow(session.SessionID);
                detailWindow.Owner = Owner;
                detailWindow.Closed += DetailWindow_Closed;
                _activeDetailWindow = detailWindow;
                detailWindow.Show();
                Hide();
            }
            catch
            {
                if (detailWindow != null)
                {
                    detailWindow.Closed -= DetailWindow_Closed;

                    try
                    {
                        detailWindow.Close();
                    }
                    catch
                    {
                        // Preserve the original startup failure for the global safe handler.
                    }
                }

                _activeDetailWindow = null;
                throw;
            }
        }

        /// <summary>
        /// Restores history after the result window closes.
        /// </summary>
        private void DetailWindow_Closed(object sender, EventArgs e)
        {
            TrainingHistoryDetailWindow detailWindow =
                sender as TrainingHistoryDetailWindow;

            if (!ReferenceEquals(detailWindow, _activeDetailWindow))
                return;

            detailWindow.Closed -= DetailWindow_Closed;
            _activeDetailWindow = null;

            if (_isClosing)
                return;

            Show();
            Activate();
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Cancels active work and detaches result navigation during close.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;
            _viewModel.OpenSessionRequested -= ViewModel_OpenSessionRequested;
            _viewModel.Dispose();

            if (_activeDetailWindow != null)
                _activeDetailWindow.Closed -= DetailWindow_Closed;

            DataContext = null;
            base.OnClosed(e);
        }

        #endregion
    }
}
