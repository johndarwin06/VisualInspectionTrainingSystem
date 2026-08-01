#region Namespaces

using System;
using System.Windows;
using System.Windows.Input;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.ViewModels;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

#endregion

namespace VisualInspectionTrainingSystem.Views.History
{
    /// <summary>
    /// Hosts current-user training history, safe shell return, and single-flight
    /// result-detail navigation.
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

        #region Shell Navigation

        /// <summary>
        /// Closes this owned history workspace so the existing authenticated
        /// trainee shell is restored without creating another shell instance.
        /// </summary>
        /// <param name="sender">The Back button.</param>
        /// <param name="eventArgs">Unused routed event data.</param>
        private void OnBackRequested(
            object sender,
            RoutedEventArgs eventArgs)
        {
            TryReturnToShell();
        }

        /// <summary>
        /// Provides Escape as the keyboard-equivalent Back action while the
        /// history list, rather than an owned session detail, is active.
        /// </summary>
        /// <param name="sender">This window.</param>
        /// <param name="eventArgs">Keyboard event data.</param>
        private void OnPreviewKeyDown(
            object sender,
            KeyEventArgs eventArgs)
        {
            if (eventArgs == null || eventArgs.Key != Key.Escape)
            {
                return;
            }

            eventArgs.Handled = TryReturnToShell();
        }

        /// <summary>
        /// Returns through the owner lifecycle exactly once and leaves an active
        /// detail workflow responsible for its own navigation.
        /// </summary>
        /// <returns>True when this history window began closing.</returns>
        private bool TryReturnToShell()
        {
            if (_isClosing || _activeDetailWindow != null)
            {
                return false;
            }

            _isClosing = true;
            Close();

            return true;
        }

        #endregion

        #region Detail Navigation

        /// <summary>
        /// Opens at most one read-only result and hides history while it is active.
        /// </summary>
        /// <param name="session">Selected current-user session.</param>
        private void ViewModel_OpenSessionRequested(
            TrainingHistorySessionSummary session)
        {
            if (_isClosing || session == null)
            {
                return;
            }

            if (_activeDetailWindow != null)
            {
                if (_activeDetailWindow.IsVisible)
                {
                    _activeDetailWindow.Activate();
                }

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
        /// <param name="sender">The closed detail window.</param>
        /// <param name="eventArgs">Unused close event data.</param>
        private void DetailWindow_Closed(object sender, EventArgs eventArgs)
        {
            TrainingHistoryDetailWindow detailWindow =
                sender as TrainingHistoryDetailWindow;

            if (!ReferenceEquals(detailWindow, _activeDetailWindow))
            {
                return;
            }

            detailWindow.Closed -= DetailWindow_Closed;
            _activeDetailWindow = null;

            if (_isClosing)
            {
                return;
            }

            Show();
            Activate();
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Cancels active work and detaches result navigation during close.
        /// </summary>
        /// <param name="eventArgs">The close event information.</param>
        protected override void OnClosed(EventArgs eventArgs)
        {
            _isClosing = true;
            _viewModel.OpenSessionRequested -= ViewModel_OpenSessionRequested;
            _viewModel.Dispose();

            if (_activeDetailWindow != null)
            {
                _activeDetailWindow.Closed -= DetailWindow_Closed;
            }

            DataContext = null;
            base.OnClosed(eventArgs);
        }

        #endregion
    }
}
