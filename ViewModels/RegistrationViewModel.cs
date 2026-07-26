#region Namespaces

using System;
using System.Threading;
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
    /// Coordinates one asynchronous inactive-trainee registration without retaining credentials after submission.
    /// </summary>
    public sealed class RegistrationViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        public const string SuccessMessage =
            "Registration submitted. Ask an administrator to activate your account.";

        private const string FailureMessage =
            "Registration could not be completed. Please try again or contact support if the problem continues.";

        #endregion

        #region Fields

        private readonly RegistrationService _service;
        private readonly CancellationTokenSource _lifetimeCancellation;
        private readonly RelayCommand _submitCommand;
        private readonly RelayCommand _cancelCommand;

        private string _employeeNo;
        private string _fullName;
        private string _department;
        private string _password;
        private string _confirmPassword;
        private string _statusMessage;
        private bool _isBusy;
        private int _isDisposed;

        #endregion

        #region Events

        /// <summary>
        /// Requests that the registration view close and return focus to Login.
        /// </summary>
        public event Action CloseRequested;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes registration with production services and commands.
        /// </summary>
        public RegistrationViewModel()
        {
            _service = new RegistrationService();
            _lifetimeCancellation = new CancellationTokenSource();
            _submitCommand = new RelayCommand(BeginSubmit, CanSubmit);
            _cancelCommand = new RelayCommand(RequestClose, CanClose);
            SubmitCommand = _submitCommand;
            CancelCommand = _cancelCommand;
            StatusMessage =
                "New accounts require administrator activation before login.";
        }

        #endregion

        #region Form Properties

        /// <summary>
        /// Gets or sets the requested employee number.
        /// </summary>
        public string EmployeeNo
        {
            get { return _employeeNo; }
            set { SetProperty(ref _employeeNo, value); }
        }

        /// <summary>
        /// Gets or sets the registrant full name.
        /// </summary>
        public string FullName
        {
            get { return _fullName; }
            set { SetProperty(ref _fullName, value); }
        }

        /// <summary>
        /// Gets or sets the registrant department.
        /// </summary>
        public string Department
        {
            get { return _department; }
            set { SetProperty(ref _department, value); }
        }

        /// <summary>
        /// Gets or sets the transient password binding.
        /// </summary>
        public string Password
        {
            get { return _password; }
            set { SetProperty(ref _password, value); }
        }

        /// <summary>
        /// Gets or sets the transient confirmation binding.
        /// </summary>
        public string ConfirmPassword
        {
            get { return _confirmPassword; }
            set { SetProperty(ref _confirmPassword, value); }
        }

        #endregion

        #region State Properties

        /// <summary>
        /// Gets whether one registration operation is active.
        /// </summary>
        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                if (SetProperty(ref _isBusy, value))
                    RefreshCommands();
            }
        }

        /// <summary>
        /// Gets the current fixed or validation-safe status.
        /// </summary>
        public string StatusMessage
        {
            get { return _statusMessage; }
            private set { SetProperty(ref _statusMessage, value); }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the single-submit command.
        /// </summary>
        public ICommand SubmitCommand { get; private set; }

        /// <summary>
        /// Gets the cancellation and close command.
        /// </summary>
        public ICommand CancelCommand { get; private set; }

        #endregion

        #region Submission

        private async void BeginSubmit()
        {
            if (!CanSubmit())
                return;

            string employeeNo = EmployeeNo;
            string fullName = FullName;
            string department = Department;
            string password = Password;
            string confirmation = ConfirmPassword;
            CancellationToken token = _lifetimeCancellation.Token;

            ClearPasswords();

            try
            {
                IsBusy = true;
                StatusMessage = "Submitting registration...";
                Task<User> workTask = Task.Run(
                    () => _service.Register(
                        employeeNo,
                        fullName,
                        department,
                        password,
                        confirmation),
                    token);
                User registered = await AwaitOrCancelAsync(
                    workTask,
                    token);
                password = null;
                confirmation = null;

                if (IsDisposed || token.IsCancellationRequested)
                    return;

                if (registered == null || registered.IsActive)
                    throw new InvalidOperationException("Registration returned an invalid account state.");

                ClearAllFields();
                StatusMessage = SuccessMessage;
                MessageBox.Show(
                    SuccessMessage,
                    "Registration Submitted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                if (!IsDisposed)
                    CloseRequested?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Closing registration ends UI waiting promptly.
            }
            catch (UserManagementException ex)
            {
                if (IsDisposed)
                    return;

                if (ex.ErrorCode != UserManagementErrorCode.Validation &&
                    ex.ErrorCode != UserManagementErrorCode.DuplicateEmployeeNumber)
                {
                    ApplicationErrorLogger.LogUnhandledException(
                        "Public Registration",
                        ex,
                        false);
                }

                StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                    return;

                ApplicationErrorLogger.LogUnhandledException(
                    "Public Registration",
                    ex,
                    false);
                StatusMessage = FailureMessage;
            }
            finally
            {
                password = null;
                confirmation = null;
                ClearPasswords();

                if (!IsDisposed)
                    IsBusy = false;
            }
        }

        #endregion

        #region Command State and Lifecycle

        private bool CanSubmit()
        {
            return !IsBusy && !IsDisposed;
        }

        private bool CanClose()
        {
            return !IsDisposed;
        }

        private void RequestClose()
        {
            ClearPasswords();
            CloseRequested?.Invoke();
        }

        private void RefreshCommands()
        {
            _submitCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Cancels UI waiting, observes abandoned work, and clears transient credentials.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            _lifetimeCancellation.Cancel();
            ClearPasswords();
            _lifetimeCancellation.Dispose();
        }

        private bool IsDisposed
        {
            get { return Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0; }
        }

        #endregion

        #region Async Helpers

        private static async Task<T> AwaitOrCancelAsync<T>(
            Task<T> workTask,
            CancellationToken cancellationToken)
        {
            Task cancellationTask = Task.Delay(
                Timeout.Infinite,
                cancellationToken);
            Task completed = await Task.WhenAny(
                workTask,
                cancellationTask);

            if (completed != workTask)
            {
                ObserveAbandonedTask(workTask);
                throw new OperationCanceledException(cancellationToken);
            }

            return await workTask;
        }

        private static async void ObserveAbandonedTask(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Observation prevents later unobserved exceptions after closing.
            }
        }

        #endregion

        #region Field Cleanup

        private void ClearPasswords()
        {
            Password = string.Empty;
            ConfirmPassword = string.Empty;
        }

        private void ClearAllFields()
        {
            EmployeeNo = string.Empty;
            FullName = string.Empty;
            Department = string.Empty;
            ClearPasswords();
        }

        #endregion
    }
}
