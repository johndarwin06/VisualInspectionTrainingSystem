#region Namespaces

using System;
using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Login
{
    /// <summary>
    /// Hosts the registration ViewModel and forwards only window lifecycle events.
    /// </summary>
    public partial class RegistrationWindow : Window
    {
        #region Fields

        private readonly RegistrationViewModel _viewModel;
        private bool _isDisposed;

        #endregion

        #region Constructor

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
                return;

            _isDisposed = true;
            Closed -= OnWindowClosed;
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.Dispose();
            DataContext = null;
        }

        #endregion
    }
}
