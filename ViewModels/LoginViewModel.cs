#region Namespaces

using System;
using System.Windows;
using System.Windows.Input;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Views;
using VisualInspectionTrainingSystem.Views.Admin;
using VisualInspectionTrainingSystem.Views.Home;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// ViewModel for the Login Window.
    /// </summary>
    public class LoginViewModel : BaseViewModel
    {
        #region Fields

        private string _employeeNo;
        private string _password;
        private string _statusMessage;
        private bool _isBusy;

        private readonly AuthenticationService _authenticationService;

        #endregion

        #region Constructor

        public LoginViewModel()
        {
            _authenticationService = new AuthenticationService();

            LoginCommand = new RelayCommand(Login, CanLogin);

            ExitCommand = new RelayCommand(Exit);

            Version = "Version " +
                      System.Reflection.Assembly
                      .GetExecutingAssembly()
                      .GetName()
                      .Version;
        }

        #endregion

        #region Properties

        public string EmployeeNo
        {
            get => _employeeNo;
            set
            {
                SetProperty(ref _employeeNo, value);
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                SetProperty(ref _password, value);
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                SetProperty(ref _statusMessage, value);
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                SetProperty(ref _isBusy, value);
            }
        }

        public string Version
        {
            get;
        }

        #endregion

        #region Commands

        public ICommand LoginCommand { get; }

        public ICommand ExitCommand { get; }

        #endregion

        #region Private Methods

        private bool CanLogin()
        {
            return !IsBusy;
        }

        private void Login()
        {
            try
            {
                IsBusy = true;

                StatusMessage = string.Empty;

                if (string.IsNullOrWhiteSpace(EmployeeNo))
                {
                    StatusMessage = "Please enter Employee Number.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(Password))
                {
                    StatusMessage = "Please enter Password.";
                    return;
                }

                User user = _authenticationService.Login(
                    EmployeeNo.Trim(),
                    Password);

                Password = string.Empty;

                if (user == null)
                {
                    StatusMessage = "Invalid Employee Number or Password.";
                    return;
                }

                Window home = user.Role == UserRoles.Admin
                    ? (Window)new AdminWindow()
                    : new HomeWindow();

                home.Show();

                foreach (Window window in Application.Current.Windows)
                {
                    if (window is Views.Login.LoginWindow)
                    {
                        window.Close();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Exit()
        {
            Application.Current.Shutdown();
        }

        #endregion
    }
}