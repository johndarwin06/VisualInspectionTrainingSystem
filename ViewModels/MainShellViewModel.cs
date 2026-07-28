#region Namespaces

using System;
using System.Windows;
using System.Windows.Input;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Presents the authenticated shell identity, role-authorized navigation
    /// requests, application theme state, and logout request without owning views.
    /// </summary>
    public sealed class MainShellViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        private const int MaximumDisplayNameLength = 100;
        private const int MaximumEmployeeNumberLength = 40;
        private const int MaximumDepartmentLength = 100;

        #endregion

        #region Fields

        private readonly ApplicationThemeService _themeService;
        private readonly RelayCommand _trainingCommand;
        private readonly RelayCommand _historyCommand;
        private readonly RelayCommand _reviewWorkflowCommand;
        private readonly RelayCommand _dashboardCommand;
        private readonly RelayCommand _reportsCommand;
        private readonly RelayCommand _userManagementCommand;
        private readonly RelayCommand _toggleThemeCommand;
        private readonly RelayCommand _logoutCommand;

        private readonly bool _isAdministrator;
        private readonly bool _isTrainee;
        private readonly string _displayName;
        private readonly string _employeeNumberText;
        private readonly string _profileSummaryText;
        private readonly string _roleText;

        private bool _isDarkTheme;
        private bool _isDisposed;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when an authorized trainee requests the training workspace.
        /// </summary>
        public event Action TrainingRequested;

        /// <summary>
        /// Occurs when an authorized trainee requests personal training history.
        /// </summary>
        public event Action HistoryRequested;

        /// <summary>
        /// Occurs when an authorized administrator requests the review workflow.
        /// </summary>
        public event Action ReviewWorkflowRequested;

        /// <summary>
        /// Occurs when an authorized administrator requests the dashboard.
        /// </summary>
        public event Action DashboardRequested;

        /// <summary>
        /// Occurs when an authorized administrator requests reports.
        /// </summary>
        public event Action ReportsRequested;

        /// <summary>
        /// Occurs when an authorized administrator requests user management.
        /// </summary>
        public event Action UserManagementRequested;

        /// <summary>
        /// Occurs when the shell requests a controlled logout transition.
        /// </summary>
        public event Action LogoutRequested;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes one role-aware shell snapshot from the authenticated session
        /// and observes the process-wide application theme.
        /// </summary>
        public MainShellViewModel()
        {
            User currentUser = SessionService.CurrentUser;
            string canonicalRole = currentUser == null || !currentUser.IsActive
                ? null
                : UserRoles.Normalize(currentUser.Role);

            _isAdministrator = string.Equals(
                canonicalRole,
                UserRoles.Admin,
                StringComparison.Ordinal);
            _isTrainee = string.Equals(
                canonicalRole,
                UserRoles.User,
                StringComparison.Ordinal);

            _displayName = NormalizeDisplayText(
                currentUser == null ? null : currentUser.FullName,
                "Signed-in user",
                MaximumDisplayNameLength);
            _employeeNumberText = "Employee " + NormalizeDisplayText(
                currentUser == null ? null : currentUser.EmployeeNo,
                "number unavailable",
                MaximumEmployeeNumberLength);
            _roleText = _isAdministrator
                ? "Administrator"
                : _isTrainee
                    ? "Trainee"
                    : "Session unavailable";

            string department = NormalizeDisplayText(
                currentUser == null ? null : currentUser.Department,
                "Department not specified",
                MaximumDepartmentLength);
            _profileSummaryText = department + " | " + _roleText;

            _themeService = ApplicationThemeService.Current;
            _isDarkTheme = _themeService.IsDarkTheme;
            _themeService.ThemeChanged += ThemeService_ThemeChanged;

            _trainingCommand = new RelayCommand(
                RequestTraining,
                CanUseTraineeDestination);
            _historyCommand = new RelayCommand(
                RequestHistory,
                CanUseTraineeDestination);
            _reviewWorkflowCommand = new RelayCommand(
                RequestReviewWorkflow,
                CanUseAdministratorDestination);
            _dashboardCommand = new RelayCommand(
                RequestDashboard,
                CanUseAdministratorDestination);
            _reportsCommand = new RelayCommand(
                RequestReports,
                CanUseAdministratorDestination);
            _userManagementCommand = new RelayCommand(
                RequestUserManagement,
                CanUseAdministratorDestination);
            _toggleThemeCommand = new RelayCommand(
                ToggleTheme,
                CanUseShellCommand);
            _logoutCommand = new RelayCommand(
                RequestLogout,
                CanUseShellCommand);

            TrainingCommand = _trainingCommand;
            HistoryCommand = _historyCommand;
            ReviewWorkflowCommand = _reviewWorkflowCommand;
            DashboardCommand = _dashboardCommand;
            ReportsCommand = _reportsCommand;
            UserManagementCommand = _userManagementCommand;
            ToggleThemeCommand = _toggleThemeCommand;
            LogoutCommand = _logoutCommand;
        }

        #endregion

        #region Profile Properties

        /// <summary>
        /// Gets a bounded display name for the authenticated user.
        /// </summary>
        public string DisplayName
        {
            get { return _displayName; }
        }

        /// <summary>
        /// Gets the personalized shell welcome text.
        /// </summary>
        public string WelcomeText
        {
            get { return "Welcome, " + DisplayName; }
        }

        /// <summary>
        /// Gets a bounded authenticated employee-number label.
        /// </summary>
        public string EmployeeNumberText
        {
            get { return _employeeNumberText; }
        }

        /// <summary>
        /// Gets a non-sensitive department and role summary.
        /// </summary>
        public string ProfileSummaryText
        {
            get { return _profileSummaryText; }
        }

        /// <summary>
        /// Gets the canonical user-facing role label.
        /// </summary>
        public string RoleText
        {
            get { return _roleText; }
        }

        /// <summary>
        /// Gets whether the shell was created for an active administrator session.
        /// </summary>
        public bool IsAdministrator
        {
            get { return _isAdministrator; }
        }

        /// <summary>
        /// Gets whether the shell was created for an active trainee session.
        /// </summary>
        public bool IsTrainee
        {
            get { return _isTrainee; }
        }

        /// <summary>
        /// Gets administrator navigation visibility for the authenticated role.
        /// </summary>
        public Visibility AdministratorVisibility
        {
            get
            {
                return IsAdministrator
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Gets trainee navigation visibility for the authenticated role.
        /// </summary>
        public Visibility TraineeVisibility
        {
            get
            {
                return IsTrainee
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        #endregion

        #region Theme Properties

        /// <summary>
        /// Gets whether the active application theme is dark.
        /// </summary>
        public bool IsDarkTheme
        {
            get { return _isDarkTheme; }
            private set
            {
                if (SetProperty(ref _isDarkTheme, value))
                {
                    OnPropertyChanged(nameof(ThemeToggleText));
                }
            }
        }

        /// <summary>
        /// Gets the accessible action text for changing the current theme.
        /// </summary>
        public string ThemeToggleText
        {
            get
            {
                return IsDarkTheme
                    ? "Switch to light theme"
                    : "Switch to dark theme";
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the trainee training-workspace request command.
        /// </summary>
        public ICommand TrainingCommand { get; private set; }

        /// <summary>
        /// Gets the trainee personal-history request command.
        /// </summary>
        public ICommand HistoryCommand { get; private set; }

        /// <summary>
        /// Gets the administrator review-workflow request command.
        /// </summary>
        public ICommand ReviewWorkflowCommand { get; private set; }

        /// <summary>
        /// Gets the administrator dashboard request command.
        /// </summary>
        public ICommand DashboardCommand { get; private set; }

        /// <summary>
        /// Gets the administrator reports request command.
        /// </summary>
        public ICommand ReportsCommand { get; private set; }

        /// <summary>
        /// Gets the administrator user-management request command.
        /// </summary>
        public ICommand UserManagementCommand { get; private set; }

        /// <summary>
        /// Gets the always-available application-theme toggle command.
        /// </summary>
        public ICommand ToggleThemeCommand { get; private set; }

        /// <summary>
        /// Gets the controlled logout request command.
        /// </summary>
        public ICommand LogoutCommand { get; private set; }

        #endregion

        #region Command Execution

        /// <summary>
        /// Raises the training request only for a currently authorized trainee.
        /// </summary>
        private void RequestTraining()
        {
            if (CanUseTraineeDestination())
            {
                TrainingRequested?.Invoke();
            }
        }

        /// <summary>
        /// Raises the personal-history request only for a currently authorized trainee.
        /// </summary>
        private void RequestHistory()
        {
            if (CanUseTraineeDestination())
            {
                HistoryRequested?.Invoke();
            }
        }

        /// <summary>
        /// Raises the review-workflow request only for a currently authorized administrator.
        /// </summary>
        private void RequestReviewWorkflow()
        {
            if (CanUseAdministratorDestination())
            {
                ReviewWorkflowRequested?.Invoke();
            }
        }

        /// <summary>
        /// Raises the dashboard request only for a currently authorized administrator.
        /// </summary>
        private void RequestDashboard()
        {
            if (CanUseAdministratorDestination())
            {
                DashboardRequested?.Invoke();
            }
        }

        /// <summary>
        /// Raises the reports request only for a currently authorized administrator.
        /// </summary>
        private void RequestReports()
        {
            if (CanUseAdministratorDestination())
            {
                ReportsRequested?.Invoke();
            }
        }

        /// <summary>
        /// Raises the user-management request only for a currently authorized administrator.
        /// </summary>
        private void RequestUserManagement()
        {
            if (CanUseAdministratorDestination())
            {
                UserManagementRequested?.Invoke();
            }
        }

        /// <summary>
        /// Toggles the process-wide application theme through the existing theme service.
        /// </summary>
        private void ToggleTheme()
        {
            if (!CanUseShellCommand())
            {
                return;
            }

            _themeService.SetTheme(!_themeService.IsDarkTheme);
        }

        /// <summary>
        /// Raises a logout request without mutating the authenticated session.
        /// </summary>
        private void RequestLogout()
        {
            if (CanUseShellCommand())
            {
                LogoutRequested?.Invoke();
            }
        }

        #endregion

        #region Authorization

        /// <summary>
        /// Returns whether the live session remains an active canonical trainee.
        /// </summary>
        private bool CanUseTraineeDestination()
        {
            return !_isDisposed && HasCurrentRole(UserRoles.User);
        }

        /// <summary>
        /// Returns whether the live session remains an active canonical administrator.
        /// </summary>
        private bool CanUseAdministratorDestination()
        {
            return !_isDisposed && HasCurrentRole(UserRoles.Admin);
        }

        /// <summary>
        /// Returns whether a shell-level command remains available.
        /// </summary>
        private bool CanUseShellCommand()
        {
            return !_isDisposed;
        }

        /// <summary>
        /// Performs a fail-closed role check against the current authenticated identity.
        /// </summary>
        /// <param name="expectedRole">Canonical role required by the destination.</param>
        /// <returns>True only for an active current user with the expected role.</returns>
        private static bool HasCurrentRole(string expectedRole)
        {
            User currentUser = SessionService.CurrentUser;

            return currentUser != null &&
                   currentUser.IsActive &&
                   string.Equals(
                       UserRoles.Normalize(currentUser.Role),
                       expectedRole,
                       StringComparison.Ordinal);
        }

        #endregion

        #region Theme Synchronization

        /// <summary>
        /// Synchronizes bindable theme state after the application theme changes.
        /// </summary>
        private void ThemeService_ThemeChanged(
            object sender,
            EventArgs eventArgs)
        {
            if (_isDisposed)
            {
                return;
            }

            IsDarkTheme = _themeService.IsDarkTheme;
        }

        #endregion

        #region Display Helpers

        /// <summary>
        /// Normalizes one user-facing identity value without exposing other user fields.
        /// </summary>
        private static string NormalizeDisplayText(
            string value,
            string fallback,
            int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string normalized = value
                .Trim()
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ');

            return normalized.Length <= maximumLength
                ? normalized
                : normalized.Substring(0, maximumLength) + "...";
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases theme observation and request subscribers exactly once.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _themeService.ThemeChanged -= ThemeService_ThemeChanged;

            TrainingRequested = null;
            HistoryRequested = null;
            ReviewWorkflowRequested = null;
            DashboardRequested = null;
            ReportsRequested = null;
            UserManagementRequested = null;
            LogoutRequested = null;

            RaiseCommandStatesChanged();
        }

        /// <summary>
        /// Invalidates every shell command after authorization or lifecycle changes.
        /// </summary>
        private void RaiseCommandStatesChanged()
        {
            _trainingCommand.RaiseCanExecuteChanged();
            _historyCommand.RaiseCanExecuteChanged();
            _reviewWorkflowCommand.RaiseCanExecuteChanged();
            _dashboardCommand.RaiseCanExecuteChanged();
            _reportsCommand.RaiseCanExecuteChanged();
            _userManagementCommand.RaiseCanExecuteChanged();
            _toggleThemeCommand.RaiseCanExecuteChanged();
            _logoutCommand.RaiseCanExecuteChanged();
        }

        #endregion
    }
}
