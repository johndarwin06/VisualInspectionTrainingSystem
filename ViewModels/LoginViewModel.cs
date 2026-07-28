#region Namespaces

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Coordinates asynchronous authentication, safe errors, registration navigation, and application exit.
    /// </summary>
    public class LoginViewModel : BaseViewModel
    {
        #region Constants

        private const string InvalidCredentialsMessage =
            "Invalid Employee Number or Password.";

        private const string LoginFailureMessage =
            "Sign-in could not be completed. Please try again or contact support if the problem continues.";

        #endregion

        #region Fields

        private readonly AuthenticationService _authenticationService;
        private readonly RelayCommand _loginCommand;
        private readonly RelayCommand _registerCommand;
        private readonly RelayCommand _exitCommand;

        private string _employeeNo;
        private string _password;
        private string _statusMessage;
        private bool _isBusy;

        #endregion

        #region Events

        /// <summary>
        /// Requests that the Login view open or reactivate one registration window.
        /// </summary>
        public event Action RegisterRequested;

        /// <summary>
        /// Requests that the Login view hand a successfully authenticated user to the application shell.
        /// </summary>
        public event Action<User> LoginSucceeded;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes Login commands and production authentication.
        /// </summary>
        public LoginViewModel()
        {
            _authenticationService = new AuthenticationService();
            _loginCommand = new RelayCommand(BeginLogin, CanRunLoginAction);
            _registerCommand = new RelayCommand(RequestRegistration, CanRunLoginAction);
            _exitCommand = new RelayCommand(Exit);
            LoginCommand = _loginCommand;
            RegisterCommand = _registerCommand;
            ExitCommand = _exitCommand;
            Version = "Version " +
                      System.Reflection.Assembly
                          .GetExecutingAssembly()
                          .GetName()
                          .Version;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the employee number entered for authentication.
        /// </summary>
        public string EmployeeNo
        {
            get { return _employeeNo; }
            set { SetProperty(ref _employeeNo, value); }
        }

        /// <summary>
        /// Gets or sets the transient Login password binding.
        /// </summary>
        public string Password
        {
            get { return _password; }
            set { SetProperty(ref _password, value); }
        }

        /// <summary>
        /// Gets or sets the current fixed or validation-safe Login status.
        /// </summary>
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        /// <summary>
        /// Gets whether one Login operation is active.
        /// </summary>
        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    _loginCommand.RaiseCanExecuteChanged();
                    _registerCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets the running application version label.
        /// </summary>
        public string Version { get; private set; }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the authentication command.
        /// </summary>
        public ICommand LoginCommand { get; private set; }

        /// <summary>
        /// Gets the public registration navigation command.
        /// </summary>
        public ICommand RegisterCommand { get; private set; }

        /// <summary>
        /// Gets the application exit command.
        /// </summary>
        public ICommand ExitCommand { get; private set; }

        #endregion

        #region Authentication

        private bool CanRunLoginAction()
        {
            return !IsBusy;
        }

        private async void BeginLogin()
        {
            if (IsBusy)
                return;

            string employeeNo = EmployeeNo == null
                ? string.Empty
                : EmployeeNo.Trim();
            string password = Password;
            bool authenticatedSessionEstablished = false;

            try
            {
                IsBusy = true;
                StatusMessage = string.Empty;

                if (string.IsNullOrWhiteSpace(employeeNo))
                {
                    StatusMessage = "Please enter Employee Number.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    StatusMessage = "Please enter Password.";
                    return;
                }

                Password = string.Empty;
                User user = await Task.Run(
                    () => _authenticationService.Login(
                        employeeNo,
                        password));
                password = null;

                if (user == null)
                {
                    StatusMessage = InvalidCredentialsMessage;
                    return;
                }

                authenticatedSessionEstablished = true;

                Action<User> loginSucceeded = LoginSucceeded;

                if (loginSucceeded == null)
                {
                    throw new InvalidOperationException(
                        "The application shell navigation handler is unavailable.");
                }

                loginSucceeded(user);
            }
            catch (Exception ex)
            {
                if (authenticatedSessionEstablished)
                {
                    SessionService.Logout();
                }

                ApplicationErrorLogger.LogUnhandledException(
                    "Login Authentication",
                    ex,
                    false);
                StatusMessage = LoginFailureMessage;
            }
            finally
            {
                password = null;
                Password = string.Empty;
                IsBusy = false;
            }
        }

        #endregion

        #region Registration and Exit

        private void RequestRegistration()
        {
            if (IsBusy)
                return;

            StatusMessage = string.Empty;
            RegisterRequested?.Invoke();
        }

        private void Exit()
        {
            Password = string.Empty;
            Application.Current.Shutdown();
        }

        #endregion
    }
}
