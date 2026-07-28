#region Namespaces

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Views.Admin;
using VisualInspectionTrainingSystem.Views.Login;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Provides commands, profile presentation, quiz-size selection, and navigation requests for Home.
    /// </summary>
    public class HomeViewModel : BaseViewModel
    {
        #region Constants

        private const string TrainingStartupErrorMessage =
            "Training could not be opened. Please try again. " +
            "Contact support if the problem continues.";

        private const string TrainingStartupErrorTitle =
            "Training Unavailable";

        private const string HistoryStartupErrorMessage =
            "Training history could not be opened. Please try again. " +
            "Contact support if the problem continues.";

        private const string HistoryStartupErrorTitle =
            "Training History Unavailable";

        #endregion

        #region Fields

        private readonly ReadOnlyCollection<int> _quizSizeOptions;
        private int _selectedQuizSize;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when Home should open one quiz using the selected sample size.
        /// </summary>
        public event Action StartTrainingRequested;

        /// <summary>
        /// Occurs when Home should open the current user's training history.
        /// </summary>
        public event Action HistoryRequested;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes home commands and defaults trainee quiz size to ten questions.
        /// </summary>
        public HomeViewModel()
        {
            _quizSizeOptions = new ReadOnlyCollection<int>(
                new[]
                {
                    ImageService.DefaultQuizSize,
                    ImageService.ExtendedQuizSize
                });
            _selectedQuizSize = ImageService.DefaultQuizSize;

            StartTrainingCommand = new RelayCommand(StartTraining);
            HistoryCommand = new RelayCommand(OpenHistory);
            AdminCommand = new RelayCommand(OpenAdmin);
            LogoutCommand = new RelayCommand(Logout);
        }

        #endregion

        #region Profile Properties

        /// <summary>
        /// Gets the personalized welcome message for the signed-in user.
        /// </summary>
        public string WelcomeMessage
        {
            get
            {
                User currentUser = SessionService.CurrentUser;
                string name = currentUser == null ||
                              string.IsNullOrWhiteSpace(currentUser.FullName)
                    ? "Trainee"
                    : currentUser.FullName.Trim();

                return "Welcome, " + name;
            }
        }

        /// <summary>
        /// Gets a concise, non-sensitive department and role summary.
        /// </summary>
        public string ProfileSummary
        {
            get
            {
                User currentUser = SessionService.CurrentUser;
                string department = currentUser == null ||
                                    string.IsNullOrWhiteSpace(currentUser.Department)
                    ? "Department not specified"
                    : currentUser.Department.Trim();
                string role = currentUser != null &&
                              string.Equals(
                                  UserRoles.Normalize(currentUser.Role),
                                  UserRoles.Admin,
                                  StringComparison.Ordinal)
                    ? "Administrator"
                    : "Trainee";

                return department + "  •  " + role;
            }
        }

        /// <summary>
        /// Gets the authenticated employee-number summary for the profile chip.
        /// </summary>
        public string EmployeeNumberSummary
        {
            get
            {
                User currentUser = SessionService.CurrentUser;
                string employeeNumber = currentUser == null ||
                                        string.IsNullOrWhiteSpace(currentUser.EmployeeNo)
                    ? "Unavailable"
                    : currentUser.EmployeeNo.Trim();

                return "Employee " + employeeNumber;
            }
        }

        #endregion

        #region Quiz Selection Properties

        /// <summary>
        /// Gets the two supported trainee quiz sizes.
        /// </summary>
        public ReadOnlyCollection<int> QuizSizeOptions
        {
            get { return _quizSizeOptions; }
        }

        /// <summary>
        /// Gets or sets the explicitly selected trainee quiz size.
        /// </summary>
        public int SelectedQuizSize
        {
            get { return _selectedQuizSize; }
            set
            {
                ValidateQuizSize(value);

                if (SetProperty(ref _selectedQuizSize, value))
                {
                    OnPropertyChanged(nameof(IsTenQuestionQuizSelected));
                    OnPropertyChanged(nameof(IsTwentyQuestionQuizSelected));
                    OnPropertyChanged(nameof(QuizSizeSummary));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether ten questions are selected.
        /// </summary>
        public bool IsTenQuestionQuizSelected
        {
            get { return SelectedQuizSize == ImageService.DefaultQuizSize; }
            set
            {
                if (value)
                    SelectedQuizSize = ImageService.DefaultQuizSize;
            }
        }

        /// <summary>
        /// Gets or sets whether twenty questions are selected.
        /// </summary>
        public bool IsTwentyQuestionQuizSelected
        {
            get { return SelectedQuizSize == ImageService.ExtendedQuizSize; }
            set
            {
                if (value)
                    SelectedQuizSize = ImageService.ExtendedQuizSize;
            }
        }

        /// <summary>
        /// Gets a clear summary of the selected quiz size.
        /// </summary>
        public string QuizSizeSummary
        {
            get { return SelectedQuizSize + " inspection images selected"; }
        }

        /// <summary>
        /// Gets administration-command visibility for an administrator session.
        /// </summary>
        public Visibility AdminVisibility
        {
            get
            {
                User currentUser = SessionService.CurrentUser;

                return currentUser != null &&
                       string.Equals(
                           UserRoles.Normalize(currentUser.Role),
                           UserRoles.Admin,
                           StringComparison.Ordinal)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the command that requests a new quiz window.
        /// </summary>
        public ICommand StartTrainingCommand { get; private set; }

        /// <summary>
        /// Gets the command that requests current-user training history.
        /// </summary>
        public ICommand HistoryCommand { get; private set; }

        /// <summary>
        /// Gets the command that opens administrator tools.
        /// </summary>
        public ICommand AdminCommand { get; private set; }

        /// <summary>
        /// Gets the command that signs out the current user.
        /// </summary>
        public ICommand LogoutCommand { get; private set; }

        #endregion

        #region Command Methods

        /// <summary>
        /// Validates the selected size and requests quiz navigation from Home.
        /// </summary>
        private void StartTraining()
        {
            try
            {
                ValidateQuizSize(SelectedQuizSize);
                StartTrainingRequested?.Invoke();
            }
            catch (Exception ex)
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "Home Start Training",
                    ex,
                    false);
                ApplicationDialogService.Show(
                    TrainingStartupErrorMessage,
                    TrainingStartupErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Requests one current-user training history window.
        /// </summary>
        private void OpenHistory()
        {
            try
            {
                if (!SessionService.IsLoggedIn)
                    throw new UnauthorizedAccessException("A signed-in user is required.");

                HistoryRequested?.Invoke();
            }
            catch (Exception ex)
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "Home Training History",
                    ex,
                    false);
                ApplicationDialogService.Show(
                    HistoryStartupErrorMessage,
                    HistoryStartupErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Opens administrator tools and closes the current Home window.
        /// </summary>
        private void OpenAdmin()
        {
            AdminWindow window = new AdminWindow();
            window.Show();
            CloseCurrentWindow<VisualInspectionTrainingSystem.Views.Home.HomeWindow>();
        }

        /// <summary>
        /// Clears the session, opens Login, and closes the current Home window.
        /// </summary>
        private void Logout()
        {
            SessionService.Logout();
            LoginWindow window = new LoginWindow();
            window.Show();
            CloseCurrentWindow<VisualInspectionTrainingSystem.Views.Home.HomeWindow>();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Rejects unsupported values before a quiz window is created.
        /// </summary>
        private static void ValidateQuizSize(int requestedQuizSize)
        {
            if (!ImageService.IsSupportedQuizSize(requestedQuizSize))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedQuizSize),
                    requestedQuizSize,
                    "Quiz size must be 10 or 20.");
            }
        }

        /// <summary>
        /// Closes the first open application window of the requested type.
        /// </summary>
        private static void CloseCurrentWindow<T>()
            where T : Window
        {
            if (Application.Current == null)
                return;

            foreach (Window window in Application.Current.Windows)
            {
                if (window is T)
                {
                    window.Close();
                    break;
                }
            }
        }

        #endregion
    }
}
