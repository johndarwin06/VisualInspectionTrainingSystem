#region Namespaces

using System;
using System.Windows;
using Wpf.Ui.Controls;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Login
{
    /// <summary>
    /// Hosts Login in Fluent window chrome and owns the single registration-window
    /// and authenticated-shell lifecycles without database logic.
    /// </summary>
    public partial class LoginWindow : FluentWindow
    {
        #region Fields

        private readonly LoginViewModel _viewModel;
        private RegistrationWindow _registrationWindow;
        private bool _isDisposed;
        private bool _shellOpened;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes Login and its registration and shell navigation events.
        /// </summary>
        public LoginWindow()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            _viewModel.RegisterRequested += OnRegisterRequested;
            _viewModel.LoginSucceeded += OnLoginSucceeded;
            DataContext = _viewModel;
            Closed += OnLoginClosed;
        }

        #endregion

        #region Shell Navigation

        /// <summary>
        /// Opens the single authenticated shell and transfers application
        /// lifetime ownership to it.
        /// </summary>
        /// <param name="user">The authenticated session user.</param>
        private void OnLoginSucceeded(User user)
        {
            if (_shellOpened)
            {
                return;
            }

            if (_isDisposed ||
                user == null ||
                !SessionService.IsLoggedIn)
            {
                throw new InvalidOperationException(
                    "An authenticated application session is required.");
            }

            MainWindow shell = null;

            try
            {
                shell = new MainWindow();
                _shellOpened = true;
                Application.Current.MainWindow = shell;
                shell.Show();
                Close();
            }
            catch
            {
                _shellOpened = false;

                if (Application.Current != null)
                {
                    Application.Current.MainWindow = this;
                }

                if (shell != null)
                {
                    try
                    {
                        shell.Close();
                    }
                    catch
                    {
                        // Preserve the original shell startup failure for Login's safe handler.
                    }
                }

                throw;
            }
        }

        #endregion

        #region Registration Navigation

        /// <summary>
        /// Opens or reactivates the one registration window owned by Login.
        /// </summary>
        private void OnRegisterRequested()
        {
            if (_registrationWindow != null)
            {
                if (_registrationWindow.WindowState == WindowState.Minimized)
                {
                    _registrationWindow.WindowState = WindowState.Normal;
                }

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

        /// <summary>
        /// Releases the closed registration reference and returns focus to the
        /// same Login window.
        /// </summary>
        private void OnRegistrationClosed(
            object sender,
            EventArgs eventArgs)
        {
            RegistrationWindow registration = sender as RegistrationWindow;

            if (registration != null)
            {
                registration.Closed -= OnRegistrationClosed;
            }

            if (ReferenceEquals(_registrationWindow, registration))
            {
                _registrationWindow = null;
            }

            if (!_isDisposed)
            {
                Activate();
            }
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
            {
                return;
            }

            _isDisposed = true;
            Closed -= OnLoginClosed;
            _viewModel.RegisterRequested -= OnRegisterRequested;
            _viewModel.LoginSucceeded -= OnLoginSucceeded;

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
