#region Namespaces

using System;
using MahApps.Metro.Controls;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Login
{
    /// <summary>
    /// Hosts the registration ViewModel in MahApps chrome and forwards only
    /// window lifecycle events.
    /// </summary>
    public partial class RegistrationWindow : MetroWindow
    {
        #region Fields

        private readonly RegistrationViewModel _viewModel;
        private bool _isDisposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates the inactive-trainee registration surface.
        /// </summary>
        public RegistrationWindow()
        {
            InitializeComponent();
            _viewModel = new RegistrationViewModel();
            _viewModel.CloseRequested += OnCloseRequested;
            DataContext = _viewModel;
            Closed += OnWindowClosed;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Returns registration to its existing owner through the established
        /// close-request contract.
        /// </summary>
        private void OnCloseRequested()
        {
            Close();
        }

        /// <summary>
        /// Clears transient credentials and removes event subscriptions once.
        /// </summary>
        private void OnWindowClosed(
            object sender,
            EventArgs eventArgs)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Closed -= OnWindowClosed;
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.Dispose();
            DataContext = null;
        }

        #endregion
    }
}
