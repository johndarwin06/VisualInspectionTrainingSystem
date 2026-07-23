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
using VisualInspectionTrainingSystem.Views.Quiz;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Provides commands, quiz-size selection, and display state for the application home screen.
    /// </summary>
    public class HomeViewModel : BaseViewModel
    {
        #region Constants

        private const string TrainingStartupErrorMessage =
            "Training could not be opened. Please try again. " +
            "Contact support if the problem continues.";

        private const string TrainingStartupErrorTitle =
            "Training Unavailable";

        #endregion

        #region Fields

        private readonly ReadOnlyCollection<int> _quizSizeOptions;

        private int _selectedQuizSize;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a training session is requested by a host that subscribes to the event.
        /// </summary>
        public event Action StartTrainingRequested;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes commands and defaults the trainee quiz size to ten questions.
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

            AdminCommand = new RelayCommand(OpenAdmin);

            LogoutCommand = new RelayCommand(Logout);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the personalized welcome message for the signed-in user.
        /// </summary>
        public string WelcomeMessage
        {
            get
            {
                return $"Welcome, {SessionService.CurrentUser.FullName}";
            }
        }

        /// <summary>
        /// Gets the two supported trainee quiz sizes.
        /// </summary>
        public ReadOnlyCollection<int> QuizSizeOptions
        {
            get
            {
                return _quizSizeOptions;
            }
        }

        /// <summary>
        /// Gets or sets the explicitly selected trainee quiz size.
        /// </summary>
        public int SelectedQuizSize
        {
            get
            {
                return _selectedQuizSize;
            }
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
        /// Gets or sets whether the ten-question option is selected.
        /// </summary>
        public bool IsTenQuestionQuizSelected
        {
            get
            {
                return SelectedQuizSize == ImageService.DefaultQuizSize;
            }
            set
            {
                if (value)
                    SelectedQuizSize = ImageService.DefaultQuizSize;
            }
        }

        /// <summary>
        /// Gets or sets whether the twenty-question option is selected.
        /// </summary>
        public bool IsTwentyQuestionQuizSelected
        {
            get
            {
                return SelectedQuizSize == ImageService.ExtendedQuizSize;
            }
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
            get
            {
                return "Selected: " + SelectedQuizSize + " questions";
            }
        }

        /// <summary>
        /// Gets the visibility of the administration command for the signed-in user.
        /// </summary>
        public Visibility AdminVisibility
        {
            get
            {
                if (SessionService.CurrentUser.Role == UserRoles.Admin)
                    return Visibility.Visible;

                return Visibility.Collapsed;
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the command that opens a new quiz window.
        /// </summary>
        public ICommand StartTrainingCommand { get; }

        /// <summary>
        /// Gets the command that opens the administration window.
        /// </summary>
        public ICommand AdminCommand { get; }

        /// <summary>
        /// Gets the command that signs out the current user.
        /// </summary>
        public ICommand LogoutCommand { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates and displays one quiz window with the explicitly selected size.
        /// </summary>
        private void StartTraining()
        {
            try
            {
                ValidateQuizSize(SelectedQuizSize);

                QuizWindow window = new QuizWindow(SelectedQuizSize);

                window.Show();
            }
            catch (Exception ex)
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "Home Start Training",
                    ex,
                    false);

                MessageBox.Show(
                    TrainingStartupErrorMessage,
                    TrainingStartupErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Opens the administration window and closes the current home window.
        /// </summary>
        private void OpenAdmin()
        {
            AdminWindow window = new AdminWindow();

            window.Show();

            CloseCurrentWindow<VisualInspectionTrainingSystem.Views.Home.HomeWindow>();
        }

        /// <summary>
        /// Clears the current session, opens the login window, and closes the home window.
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
        /// <typeparam name="T">The window type to close.</typeparam>
        private void CloseCurrentWindow<T>()
            where T : Window
        {
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
