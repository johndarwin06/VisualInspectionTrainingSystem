#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    /// Coordinates asynchronous administrator-only user listing and account mutations.
    /// Plain-text password fields are cleared after every attempted password operation.
    /// </summary>
    public sealed class UserManagementViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        private const string LoadErrorMessage =
            "The user list could not be loaded. Please try again or contact support if the problem continues.";

        private const string SaveErrorMessage =
            "The user-management change could not be completed. No changes were saved.";

        #endregion

        #region Fields

        private readonly UserManagementService _service;
        private readonly CancellationTokenSource _lifetimeCancellation;
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _addUserCommand;
        private readonly RelayCommand _updateRoleCommand;
        private readonly RelayCommand _toggleActiveCommand;
        private readonly RelayCommand _resetPasswordCommand;
        private readonly RelayCommand _closeCommand;

        private User _selectedUser;
        private string _newEmployeeNo;
        private string _newFullName;
        private string _newDepartment;
        private string _newRole;
        private string _newPassword;
        private string _newConfirmPassword;
        private string _editedRole;
        private string _resetPassword;
        private string _resetConfirmPassword;
        private string _statusMessage;
        private bool _isBusy;
        private int _loadGeneration;
        private int _isDisposed;

        #endregion

        #region Events

        /// <summary>
        /// Requests that the host window close without placing lifecycle logic in the repository.
        /// </summary>
        public event Action CloseRequested;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates the administrator-only workflow and starts one asynchronous load.
        /// </summary>
        public UserManagementViewModel()
        {
            if (!UserManagementService.IsCurrentSessionAdministrator)
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Unauthorized,
                    "Administrator authorization is required for User Management.");
            }

            _service = new UserManagementService();
            _lifetimeCancellation = new CancellationTokenSource();
            Users = new ObservableCollection<User>();
            RoleOptions = new ObservableCollection<string>(
                new[] { UserRoles.Admin, UserRoles.User });
            _newRole = UserRoles.User;

            _refreshCommand = new RelayCommand(BeginLoadUsers, CanRunCommand);
            _addUserCommand = new RelayCommand(BeginAddUser, CanRunCommand);
            _updateRoleCommand = new RelayCommand(BeginUpdateRole, CanChangeSelectedUser);
            _toggleActiveCommand = new RelayCommand(BeginToggleActive, CanChangeSelectedUser);
            _resetPasswordCommand = new RelayCommand(BeginResetPassword, CanChangeSelectedUser);
            _closeCommand = new RelayCommand(RequestClose, CanRunCommand);

            RefreshCommand = _refreshCommand;
            AddUserCommand = _addUserCommand;
            UpdateRoleCommand = _updateRoleCommand;
            ToggleActiveCommand = _toggleActiveCommand;
            ResetPasswordCommand = _resetPasswordCommand;
            CloseCommand = _closeCommand;

            StatusMessage = "Loading users...";
            BeginLoadUsers();
        }

        #endregion

        #region Collections

        /// <summary>
        /// Gets the safe user-management projection. PasswordHash is always empty.
        /// </summary>
        public ObservableCollection<User> Users { get; private set; }

        /// <summary>
        /// Gets the two canonical roles established by the application and database.
        /// </summary>
        public ObservableCollection<string> RoleOptions { get; private set; }

        #endregion

        #region Selection Properties

        /// <summary>
        /// Gets or sets the selected account for administrator operations.
        /// </summary>
        public User SelectedUser
        {
            get { return _selectedUser; }
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    EditedRole = value == null ? null : value.Role;
                    ClearResetPasswordFields();
                    NotifySelectedUserChanged();
                    RefreshCommands();
                }
            }
        }

        /// <summary>
        /// Gets the selected account activation action label.
        /// </summary>
        public string ActivationActionText
        {
            get
            {
                if (SelectedUser == null)
                    return "Disable User";

                return SelectedUser.IsActive
                    ? "Disable User"
                    : "Reactivate User";
            }
        }

        /// <summary>
        /// Gets a safe selected-account summary.
        /// </summary>
        public string SelectedUserSummary
        {
            get
            {
                if (SelectedUser == null)
                    return "Select a user to manage role, activation, or password.";

                return SelectedUser.EmployeeNo + " - " +
                       SelectedUser.FullName + " (" +
                       SelectedUser.Role + ", " +
                       (SelectedUser.IsActive ? "Active" : "Inactive") + ")";
            }
        }

        #endregion

        #region Add User Properties

        public string NewEmployeeNo
        {
            get { return _newEmployeeNo; }
            set { SetProperty(ref _newEmployeeNo, value); }
        }

        public string NewFullName
        {
            get { return _newFullName; }
            set { SetProperty(ref _newFullName, value); }
        }

        public string NewDepartment
        {
            get { return _newDepartment; }
            set { SetProperty(ref _newDepartment, value); }
        }

        public string NewRole
        {
            get { return _newRole; }
            set { SetProperty(ref _newRole, value); }
        }

        public string NewPassword
        {
            get { return _newPassword; }
            set { SetProperty(ref _newPassword, value); }
        }

        public string NewConfirmPassword
        {
            get { return _newConfirmPassword; }
            set { SetProperty(ref _newConfirmPassword, value); }
        }

        #endregion

        #region Selected User Edit Properties

        public string EditedRole
        {
            get { return _editedRole; }
            set { SetProperty(ref _editedRole, value); }
        }

        public string ResetPassword
        {
            get { return _resetPassword; }
            set { SetProperty(ref _resetPassword, value); }
        }

        public string ResetConfirmPassword
        {
            get { return _resetConfirmPassword; }
            set { SetProperty(ref _resetConfirmPassword, value); }
        }

        #endregion

        #region State Properties

        /// <summary>
        /// Gets or sets whether one database operation is active.
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
        /// Gets or sets the current fixed or validation-safe status message.
        /// </summary>
        public string StatusMessage
        {
            get { return _statusMessage; }
            private set { SetProperty(ref _statusMessage, value); }
        }

        public int UserCount { get { return Users.Count; } }

        public int ActiveUserCount
        {
            get { return Users.Count(user => user != null && user.IsActive); }
        }

        public int ActiveAdministratorCount
        {
            get
            {
                return Users.Count(
                    user => user != null &&
                            user.IsActive &&
                            string.Equals(
                                UserRoles.Normalize(user.Role),
                                UserRoles.Admin,
                                StringComparison.Ordinal));
            }
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; private set; }

        public ICommand AddUserCommand { get; private set; }

        public ICommand UpdateRoleCommand { get; private set; }

        public ICommand ToggleActiveCommand { get; private set; }

        public ICommand ResetPasswordCommand { get; private set; }

        public ICommand CloseCommand { get; private set; }

        #endregion

        #region Loading

        private async void BeginLoadUsers()
        {
            await LoadUsersAsync(null);
        }

        /// <summary>
        /// Replaces the user list atomically so Refresh never duplicates rows.
        /// </summary>
        private async Task LoadUsersAsync(int? preferredUserId)
        {
            if (IsBusy || IsDisposed)
                return;

            int generation = Interlocked.Increment(ref _loadGeneration);
            CancellationToken token = _lifetimeCancellation.Token;

            try
            {
                IsBusy = true;
                StatusMessage = "Loading users...";
                Task<IList<User>> workTask = Task.Run(
                    () => _service.GetUsers(),
                    token);
                IList<User> users = await AwaitOrCancelAsync(workTask, token);

                if (!CanApplyResult(generation, token))
                    return;

                ReplaceUsers(users, preferredUserId);
                StatusMessage = "Loaded " + UserCount + " user(s).";
            }
            catch (OperationCanceledException)
            {
                // Window closing is an expected lifecycle outcome.
            }
            catch (UserManagementException ex)
            {
                if (!CanApplyResult(generation, token))
                    return;

                LogTechnicalFailure("User Management Load", ex);
                ClearUsers();
                StatusMessage = ex.Message;
                ShowSafeError(ex.Message, "User Management");
            }
            catch (Exception ex)
            {
                if (!CanApplyResult(generation, token))
                    return;

                ApplicationErrorLogger.LogUnhandledException(
                    "User Management Load",
                    ex,
                    false);
                ClearUsers();
                StatusMessage = LoadErrorMessage;
                ShowSafeError(LoadErrorMessage, "User Management");
            }
            finally
            {
                if (generation == _loadGeneration && !IsDisposed)
                    IsBusy = false;
            }
        }

        private void ReplaceUsers(
            IEnumerable<User> users,
            int? preferredUserId)
        {
            Users.Clear();

            foreach (User user in users ?? Enumerable.Empty<User>())
            {
                if (user != null)
                    Users.Add(user);
            }

            SelectedUser = preferredUserId.HasValue
                ? Users.FirstOrDefault(user => user.UserID == preferredUserId.Value)
                : Users.FirstOrDefault();
            NotifySummaryChanged();
        }

        private void ClearUsers()
        {
            Users.Clear();
            SelectedUser = null;
            NotifySummaryChanged();
        }

        #endregion

        #region Add User

        private async void BeginAddUser()
        {
            if (IsBusy || IsDisposed)
                return;

            string employeeNo = NewEmployeeNo;
            string fullName = NewFullName;
            string department = NewDepartment;
            string role = NewRole;
            string password = NewPassword;
            string confirmation = NewConfirmPassword;
            CancellationToken token = _lifetimeCancellation.Token;

            ClearNewPasswordFields();

            try
            {
                IsBusy = true;
                StatusMessage = "Creating user...";
                Task<User> workTask = Task.Run(
                    () => _service.CreateUser(
                        employeeNo,
                        fullName,
                        department,
                        role,
                        password,
                        confirmation),
                    token);
                User created = await AwaitOrCancelAsync(workTask, token);

                if (IsDisposed || token.IsCancellationRequested)
                    return;

                int createdUserId = created.UserID;
                ClearNewUserFields();
                StatusMessage = "User " + created.EmployeeNo + " was created securely.";
                IsBusy = false;
                await LoadUsersAsync(createdUserId);
            }
            catch (OperationCanceledException)
            {
                // Window closing is expected.
            }
            catch (UserManagementException ex)
            {
                HandleSafeFailure("User Management Create", ex, "Add User");
            }
            catch (Exception ex)
            {
                HandleUnexpectedFailure("User Management Create", ex, "Add User");
            }
            finally
            {
                ClearNewPasswordFields();

                if (!IsDisposed)
                    IsBusy = false;
            }
        }

        #endregion

        #region Role and Activation

        private async void BeginUpdateRole()
        {
            User selected = SelectedUser;

            if (selected == null || IsBusy || IsDisposed)
                return;

            string requestedRole = EditedRole;

            if (MessageBox.Show(
                    "Change " + selected.EmployeeNo + " from " +
                    selected.Role + " to " + requestedRole +
                    "? The new role applies on the user's next login.",
                    "Confirm Role Change",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            await RunSelectedMutationAsync(
                "Updating user role...",
                "User role updated. The change applies on the next login.",
                "User Management Role",
                "Update Role",
                () => _service.SetUserRole(
                    selected.UserID,
                    selected.Role,
                    requestedRole));
        }

        private async void BeginToggleActive()
        {
            User selected = SelectedUser;

            if (selected == null || IsBusy || IsDisposed)
                return;

            bool newIsActive = !selected.IsActive;

            if (!newIsActive &&
                MessageBox.Show(
                    "Disable " + selected.EmployeeNo +
                    "? The user will be unable to authenticate, but their history will remain intact.",
                    "Confirm Disable User",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            await RunSelectedMutationAsync(
                newIsActive ? "Reactivating user..." : "Disabling user...",
                newIsActive ? "User reactivated." : "User disabled.",
                "User Management Activation",
                newIsActive ? "Reactivate User" : "Disable User",
                () => _service.SetUserActive(
                    selected.UserID,
                    selected.IsActive,
                    newIsActive));
        }

        #endregion

        #region Password Reset

        private async void BeginResetPassword()
        {
            User selected = SelectedUser;

            if (selected == null || IsBusy || IsDisposed)
                return;

            string password = ResetPassword;
            string confirmation = ResetConfirmPassword;
            ClearResetPasswordFields();

            if (MessageBox.Show(
                    "Replace the password for " + selected.EmployeeNo + "?",
                    "Confirm Password Reset",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            await RunSelectedMutationAsync(
                "Resetting password...",
                "Password reset completed securely.",
                "User Management Password Reset",
                "Reset Password",
                () => _service.ResetPassword(
                    selected.UserID,
                    password,
                    confirmation));

            ClearResetPasswordFields();
        }

        #endregion

        #region Mutation Helper

        private async Task RunSelectedMutationAsync(
            string progressMessage,
            string successMessage,
            string logSource,
            string dialogTitle,
            Action mutation)
        {
            User selected = SelectedUser;

            if (selected == null || mutation == null || IsBusy || IsDisposed)
                return;

            int userId = selected.UserID;
            CancellationToken token = _lifetimeCancellation.Token;

            try
            {
                IsBusy = true;
                StatusMessage = progressMessage;
                Task workTask = Task.Run(mutation, token);
                await AwaitOrCancelAsync(workTask, token);

                if (IsDisposed || token.IsCancellationRequested)
                    return;

                StatusMessage = successMessage;
                IsBusy = false;
                await LoadUsersAsync(userId);
            }
            catch (OperationCanceledException)
            {
                // Window closing is expected.
            }
            catch (UserManagementException ex)
            {
                HandleSafeFailure(logSource, ex, dialogTitle);
            }
            catch (Exception ex)
            {
                HandleUnexpectedFailure(logSource, ex, dialogTitle);
            }
            finally
            {
                if (!IsDisposed)
                    IsBusy = false;
            }
        }

        #endregion

        #region Command and Lifecycle

        private bool CanRunCommand()
        {
            return !IsBusy && !IsDisposed;
        }

        private bool CanChangeSelectedUser()
        {
            return CanRunCommand() && SelectedUser != null;
        }

        private void RequestClose()
        {
            CloseRequested?.Invoke();
        }

        private void RefreshCommands()
        {
            _refreshCommand.RaiseCanExecuteChanged();
            _addUserCommand.RaiseCanExecuteChanged();
            _updateRoleCommand.RaiseCanExecuteChanged();
            _toggleActiveCommand.RaiseCanExecuteChanged();
            _resetPasswordCommand.RaiseCanExecuteChanged();
            _closeCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Cancels UI waiting and observes any database work that completes after closing.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            Interlocked.Increment(ref _loadGeneration);
            _lifetimeCancellation.Cancel();
            ClearNewPasswordFields();
            ClearResetPasswordFields();
            ClearUsers();
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
            Task cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
            Task completed = await Task.WhenAny(workTask, cancellationTask);

            if (completed != workTask)
            {
                ObserveAbandonedTask(workTask);
                throw new OperationCanceledException(cancellationToken);
            }

            return await workTask;
        }

        private static async Task AwaitOrCancelAsync(
            Task workTask,
            CancellationToken cancellationToken)
        {
            Task cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
            Task completed = await Task.WhenAny(workTask, cancellationTask);

            if (completed != workTask)
            {
                ObserveAbandonedTask(workTask);
                throw new OperationCanceledException(cancellationToken);
            }

            await workTask;
        }

        private static async void ObserveAbandonedTask(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Observation prevents a later unobserved exception after the screen closes.
            }
        }

        private bool CanApplyResult(
            int generation,
            CancellationToken token)
        {
            return !IsDisposed &&
                   !token.IsCancellationRequested &&
                   generation == _loadGeneration;
        }

        #endregion

        #region Notification and Cleanup Helpers

        private void NotifySelectedUserChanged()
        {
            OnPropertyChanged(nameof(ActivationActionText));
            OnPropertyChanged(nameof(SelectedUserSummary));
        }

        private void NotifySummaryChanged()
        {
            OnPropertyChanged(nameof(UserCount));
            OnPropertyChanged(nameof(ActiveUserCount));
            OnPropertyChanged(nameof(ActiveAdministratorCount));
            NotifySelectedUserChanged();
            RefreshCommands();
        }

        private void ClearNewUserFields()
        {
            NewEmployeeNo = string.Empty;
            NewFullName = string.Empty;
            NewDepartment = string.Empty;
            NewRole = UserRoles.User;
            ClearNewPasswordFields();
        }

        private void ClearNewPasswordFields()
        {
            NewPassword = string.Empty;
            NewConfirmPassword = string.Empty;
        }

        private void ClearResetPasswordFields()
        {
            ResetPassword = string.Empty;
            ResetConfirmPassword = string.Empty;
        }

        private void HandleSafeFailure(
            string logSource,
            UserManagementException exception,
            string dialogTitle)
        {
            if (IsDisposed)
                return;

            LogTechnicalFailure(logSource, exception);
            StatusMessage = exception.Message;
            ShowSafeError(exception.Message, dialogTitle);
        }

        private void HandleUnexpectedFailure(
            string logSource,
            Exception exception,
            string dialogTitle)
        {
            if (IsDisposed)
                return;

            ApplicationErrorLogger.LogUnhandledException(
                logSource,
                exception,
                false);
            StatusMessage = SaveErrorMessage;
            ShowSafeError(SaveErrorMessage, dialogTitle);
        }

        private static void LogTechnicalFailure(
            string source,
            UserManagementException exception)
        {
            if (exception.ErrorCode == UserManagementErrorCode.Validation)
                return;

            ApplicationErrorLogger.LogUnhandledException(
                source,
                exception,
                false);
        }

        private static void ShowSafeError(string message, string title)
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        #endregion
    }
}
