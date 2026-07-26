#region Namespaces

using System;
using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Login
{
    /// <summary>
    /// Hosts Login and owns the single registration-window lifecycle without database logic.
    /// </summary>
    public partial class LoginWindow : Window
    {
        #region Fields

        private readonly LoginViewModel _viewModel;
        private RegistrationWindow _registrationWindow;
        private bool _isDisposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes Login and its registration navigation event.
        /// </summary>
        public LoginWindow()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            _viewModel.RegisterRequested += OnRegisterRequested;
            DataContext = _viewModel;
            Closed += OnLoginClosed;
        }

        #endregion

        #region Registration Navigation

        private void OnRegisterRequested()
        {
            if (_registrationWindow != null)
            {
                if (_registrationWindow.WindowState == WindowState.Minimized)
                    _registrationWindow.WindowState = WindowState.Normal;

                _registrationWindow.Activate();
                return;
            }

            _registrationWindow = new RegistrationWindow
            {
                Owner = this
            };
            _registrationWindow.Closed += OnRegistrationClosed;
            _registrationWindow.Show();
        }

        private void OnRegistrationClosed(
            object sender,
            EventArgs eventArgs)
        {
            RegistrationWindow registration = sender as RegistrationWindow;

            if (registration != null)
                registration.Closed -= OnRegistrationClosed;

            if (ReferenceEquals(_registrationWindow, registration))
                _registrationWindow = null;

            if (!_isDisposed)
                Activate();
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Removes subscriptions and closes registration when Login itself closes.
        /// </summary>
        private void OnLoginClosed(
            object sender,
            EventArgs eventArgs)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            Closed -= OnLoginClosed;
            _viewModel.RegisterRequested -= OnRegisterRequested;

            if (_registrationWindow != null)
            {
                RegistrationWindow registration = _registrationWindow;
                _registrationWindow = null;
                registration.Closed -= OnRegistrationClosed;
                registration.Close();
            }

            DataContext = null;
        }

        #endregion
    }
}
