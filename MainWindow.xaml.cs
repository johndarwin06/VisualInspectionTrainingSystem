#region Namespaces

using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using MahApps.Metro.Controls;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.ViewModels;
using VisualInspectionTrainingSystem.Views.Admin;
using VisualInspectionTrainingSystem.Views.Dashboard;
using VisualInspectionTrainingSystem.Views.History;
using VisualInspectionTrainingSystem.Views.Home;
using VisualInspectionTrainingSystem.Views.Login;
using VisualInspectionTrainingSystem.Views.Reports;

#endregion

namespace VisualInspectionTrainingSystem
{
    /// <summary>
    /// Owns the single authenticated, role-aware application shell and one active workflow window.
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        #region Constants

        private const string NavigationFailureMessage =
            "The requested workspace could not be opened. Please try again. " +
            "Contact support if the problem continues.";

        private const string NavigationFailureTitle =
            "Workspace Unavailable";

        private const string AuthorizationFailureMessage =
            "Your account is not authorized to open this workspace.";

        private const string AuthorizationFailureTitle =
            "Access Not Available";

        private const string LogoutFailureMessage =
            "Sign-out could not be completed safely. Please close the application and try again.";

        #endregion

        #region Fields

        private readonly MainShellViewModel _viewModel;
        private Window _activeWorkspaceWindow;
        private bool _isClosing;
        private bool _isLoggingOut;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates the shell for the active supported session and attaches view-owned navigation.
        /// </summary>
        public MainWindow()
        {
            EnsureSupportedSession();
            InitializeComponent();

            _viewModel = new MainShellViewModel();
            SubscribeNavigationRequests();
            DataContext = _viewModel;
        }

        #endregion

        #region Navigation Requests

        private void SubscribeNavigationRequests()
        {
            _viewModel.TrainingRequested += ViewModel_TrainingRequested;
            _viewModel.HistoryRequested += ViewModel_HistoryRequested;
            _viewModel.ReviewWorkflowRequested += ViewModel_ReviewWorkflowRequested;
            _viewModel.DashboardRequested += ViewModel_DashboardRequested;
            _viewModel.ReportsRequested += ViewModel_ReportsRequested;
            _viewModel.UserManagementRequested += ViewModel_UserManagementRequested;
            _viewModel.LogoutRequested += ViewModel_LogoutRequested;
        }

        private void UnsubscribeNavigationRequests()
        {
            _viewModel.TrainingRequested -= ViewModel_TrainingRequested;
            _viewModel.HistoryRequested -= ViewModel_HistoryRequested;
            _viewModel.ReviewWorkflowRequested -= ViewModel_ReviewWorkflowRequested;
            _viewModel.DashboardRequested -= ViewModel_DashboardRequested;
            _viewModel.ReportsRequested -= ViewModel_ReportsRequested;
            _viewModel.UserManagementRequested -= ViewModel_UserManagementRequested;
            _viewModel.LogoutRequested -= ViewModel_LogoutRequested;
        }

        private void ViewModel_TrainingRequested()
        {
            OpenWorkspace(
                () => new HomeWindow(),
                WorkspaceAuthorization.Trainee,
                "Training");
        }

        private void ViewModel_HistoryRequested()
        {
            OpenWorkspace(
                () => new TrainingHistoryWindow(),
                WorkspaceAuthorization.Trainee,
                "My Training History");
        }

        private void ViewModel_ReviewWorkflowRequested()
        {
            OpenWorkspace(
                () => new AdminWindow(),
                WorkspaceAuthorization.Administrator,
                "Review Workflow");
        }

        private void ViewModel_DashboardRequested()
        {
            OpenWorkspace(
                () => new DashboardWindow(),
                WorkspaceAuthorization.Administrator,
                "Dashboard");
        }

        private void ViewModel_ReportsRequested()
        {
            OpenWorkspace(
                () => new ReportsWindow(),
                WorkspaceAuthorization.Administrator,
                "Reports");
        }

        private void ViewModel_UserManagementRequested()
        {
            OpenWorkspace(
                () => new UserManagementWindow(),
                WorkspaceAuthorization.Administrator,
                "User Management");
        }

        #endregion

        #region Workspace Ownership

        /// <summary>
        /// Opens one authorized modeless workflow while keeping global navigation single-flight.
        /// </summary>
        private void OpenWorkspace(
            Func<Window> windowFactory,
            WorkspaceAuthorization authorization,
            string workspaceName)
        {
            if (_isClosing || _isLoggingOut)
            {
                return;
            }

            if (!IsAuthorized(authorization))
            {
                ApplicationDialogService.Show(
                    AuthorizationFailureMessage,
                    AuthorizationFailureTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_activeWorkspaceWindow != null)
            {
                RestoreAndActivate(_activeWorkspaceWindow);
                return;
            }

            Window workspace = null;

            try
            {
                workspace = windowFactory();

                if (workspace == null)
                {
                    throw new InvalidOperationException(
                        "The workspace factory returned no window.");
                }

                workspace.Owner = this;
                workspace.Closed += WorkspaceWindow_Closed;
                _activeWorkspaceWindow = workspace;
                IsEnabled = false;
                workspace.Show();
            }
            catch (Exception ex)
            {
                CleanupFailedWorkspace(workspace);
                IsEnabled = true;

                ApplicationErrorLogger.LogUnhandledException(
                    "Shell Open " + workspaceName,
                    ex,
                    false);
                ApplicationDialogService.Show(
                    NavigationFailureMessage,
                    NavigationFailureTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void WorkspaceWindow_Closed(
            object sender,
            EventArgs eventArgs)
        {
            Window closedWindow = sender as Window;

            if (closedWindow != null)
            {
                closedWindow.Closed -= WorkspaceWindow_Closed;
            }

            if (!ReferenceEquals(closedWindow, _activeWorkspaceWindow))
            {
                return;
            }

            _activeWorkspaceWindow = null;

            if (_isClosing || _isLoggingOut)
            {
                return;
            }

            IsEnabled = true;
            RestoreAndActivate(this);
        }

        private void CleanupFailedWorkspace(Window workspace)
        {
            if (workspace != null)
            {
                workspace.Closed -= WorkspaceWindow_Closed;

                try
                {
                    workspace.Close();
                }
                catch
                {
                    // Preserve the original startup failure for the fixed shell error path.
                }
            }

            if (ReferenceEquals(_activeWorkspaceWindow, workspace))
            {
                _activeWorkspaceWindow = null;
            }
        }

        private static void RestoreAndActivate(Window window)
        {
            if (window == null)
            {
                return;
            }

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Show();
            window.Activate();
        }

        #endregion

        #region Logout

        /// <summary>
        /// Creates Login first, clears the session, transfers MainWindow, and then closes the shell.
        /// </summary>
        private void ViewModel_LogoutRequested()
        {
            if (_isClosing || _isLoggingOut)
            {
                return;
            }

            LoginWindow loginWindow = null;

            try
            {
                loginWindow = new LoginWindow();
                _isLoggingOut = true;
                Application.Current.MainWindow = loginWindow;
                loginWindow.Show();
                SessionService.Logout();
                Close();
            }
            catch (Exception ex)
            {
                _isLoggingOut = false;

                if (Application.Current != null)
                {
                    Application.Current.MainWindow = this;
                }

                if (loginWindow != null)
                {
                    try
                    {
                        loginWindow.Close();
                    }
                    catch
                    {
                        // Preserve the original logout transition failure.
                    }
                }

                ApplicationErrorLogger.LogUnhandledException(
                    "Shell Logout",
                    ex,
                    false);
                ApplicationDialogService.Show(
                    LogoutFailureMessage,
                    "Sign Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Authorization

        private static void EnsureSupportedSession()
        {
            User currentUser = SessionService.CurrentUser;
            string role = currentUser == null
                ? null
                : UserRoles.Normalize(currentUser.Role);

            if (currentUser == null ||
                !currentUser.IsActive ||
                (!string.Equals(role, UserRoles.Admin, StringComparison.Ordinal) &&
                 !string.Equals(role, UserRoles.User, StringComparison.Ordinal)))
            {
                throw new UnauthorizedAccessException(
                    "An active supported application session is required.");
            }
        }

        private static bool IsAuthorized(
            WorkspaceAuthorization authorization)
        {
            User currentUser = SessionService.CurrentUser;

            if (currentUser == null || !currentUser.IsActive)
            {
                return false;
            }

            string requiredRole = authorization ==
                WorkspaceAuthorization.Administrator
                ? UserRoles.Admin
                : UserRoles.User;

            return string.Equals(
                UserRoles.Normalize(currentUser.Role),
                requiredRole,
                StringComparison.Ordinal);
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Marks the shell as closing before WPF begins closing owned windows so their
        /// close callbacks cannot attempt to reactivate this window.
        /// </summary>
        /// <param name="e">The cancelable shell-closing event data.</param>
        protected override void OnClosing(CancelEventArgs e)
        {
            _isClosing = true;
            base.OnClosing(e);

            if (e.Cancel)
            {
                _isClosing = false;
            }
        }

        /// <summary>
        /// Closes owned workflows, releases the shell ViewModel, and clears the in-memory session.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;
            UnsubscribeNavigationRequests();

            Window[] ownedWindows = OwnedWindows
                .Cast<Window>()
                .ToArray();

            foreach (Window ownedWindow in ownedWindows)
            {
                try
                {
                    ownedWindow.Close();
                }
                catch
                {
                    // Shell shutdown must continue even when an owned surface is already closing.
                }
            }

            if (_activeWorkspaceWindow != null)
            {
                _activeWorkspaceWindow.Closed -= WorkspaceWindow_Closed;
                _activeWorkspaceWindow = null;
            }

            if (!_isLoggingOut)
            {
                SessionService.Logout();
            }

            _viewModel.Dispose();
            DataContext = null;
            base.OnClosed(e);
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Identifies the live role required before a workspace may be constructed.
        /// </summary>
        private enum WorkspaceAuthorization
        {
            Trainee,
            Administrator
        }

        #endregion
    }
}
