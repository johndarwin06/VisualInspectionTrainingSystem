#region Namespaces

using System;
using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;
using VisualInspectionTrainingSystem.Views.History;
using VisualInspectionTrainingSystem.Views.Quiz;

#endregion

namespace VisualInspectionTrainingSystem.Views.Home
{
    /// <summary>
    /// Hosts trainee Home and owns single-flight quiz and history navigation.
    /// </summary>
    public partial class HomeWindow : Window
    {
        #region Fields

        private readonly HomeViewModel _viewModel;
        private QuizWindow _activeQuizWindow;
        private TrainingHistoryWindow _activeHistoryWindow;
        private bool _isClosing;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates Home state and attaches view-owned navigation requests.
        /// </summary>
        public HomeWindow()
        {
            InitializeComponent();

            _viewModel = new HomeViewModel();
            _viewModel.StartTrainingRequested += ViewModel_StartTrainingRequested;
            _viewModel.HistoryRequested += ViewModel_HistoryRequested;
            DataContext = _viewModel;
        }

        #endregion

        #region Quiz Navigation

        /// <summary>
        /// Opens at most one quiz and hides Home while it is active.
        /// </summary>
        private void ViewModel_StartTrainingRequested()
        {
            if (_isClosing)
                return;

            if (_activeQuizWindow != null)
            {
                if (_activeQuizWindow.IsVisible)
                    _activeQuizWindow.Activate();

                return;
            }

            QuizWindow quiz = null;

            try
            {
                quiz = new QuizWindow(_viewModel.SelectedQuizSize);
                _activeQuizWindow = quiz;
                quiz.Closed += QuizWindow_Closed;
                quiz.Show();
                Hide();
            }
            catch
            {
                CleanupFailedQuiz(quiz);
                throw;
            }
        }

        /// <summary>
        /// Restores Home after normal quiz completion or cancellation.
        /// </summary>
        private void QuizWindow_Closed(object sender, EventArgs e)
        {
            QuizWindow closedQuiz = sender as QuizWindow;

            if (!ReferenceEquals(closedQuiz, _activeQuizWindow))
                return;

            closedQuiz.Closed -= QuizWindow_Closed;
            _activeQuizWindow = null;
            RestoreHome();
        }

        /// <summary>
        /// Releases a quiz that failed during window startup.
        /// </summary>
        private void CleanupFailedQuiz(QuizWindow quiz)
        {
            if (quiz != null)
            {
                quiz.Closed -= QuizWindow_Closed;

                try
                {
                    quiz.Close();
                }
                catch
                {
                    // Preserve the original startup failure for the safe Home handler.
                }
            }

            _activeQuizWindow = null;
        }

        #endregion

        #region History Navigation

        /// <summary>
        /// Opens at most one current-user training-history window.
        /// </summary>
        private void ViewModel_HistoryRequested()
        {
            if (_isClosing)
                return;

            if (_activeHistoryWindow != null)
            {
                if (_activeHistoryWindow.IsVisible)
                    _activeHistoryWindow.Activate();

                return;
            }

            TrainingHistoryWindow historyWindow = null;

            try
            {
                historyWindow = new TrainingHistoryWindow();
                _activeHistoryWindow = historyWindow;
                historyWindow.Closed += HistoryWindow_Closed;
                historyWindow.Show();
                Hide();
            }
            catch
            {
                CleanupFailedHistory(historyWindow);
                throw;
            }
        }

        /// <summary>
        /// Restores Home after history closes.
        /// </summary>
        private void HistoryWindow_Closed(object sender, EventArgs e)
        {
            TrainingHistoryWindow closedHistory =
                sender as TrainingHistoryWindow;

            if (!ReferenceEquals(closedHistory, _activeHistoryWindow))
                return;

            closedHistory.Closed -= HistoryWindow_Closed;
            _activeHistoryWindow = null;
            RestoreHome();
        }

        /// <summary>
        /// Releases a history window that failed during startup.
        /// </summary>
        private void CleanupFailedHistory(TrainingHistoryWindow historyWindow)
        {
            if (historyWindow != null)
            {
                historyWindow.Closed -= HistoryWindow_Closed;

                try
                {
                    historyWindow.Close();
                }
                catch
                {
                    // Preserve the original startup failure for the safe Home handler.
                }
            }

            _activeHistoryWindow = null;
        }

        #endregion

        #region Navigation Helpers

        /// <summary>
        /// Restores and activates Home unless application shutdown is underway.
        /// </summary>
        private void RestoreHome()
        {
            if (_isClosing)
                return;

            Show();
            Activate();
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Detaches navigation handlers without reopening Home during shutdown.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;
            _viewModel.StartTrainingRequested -= ViewModel_StartTrainingRequested;
            _viewModel.HistoryRequested -= ViewModel_HistoryRequested;

            if (_activeQuizWindow != null)
                _activeQuizWindow.Closed -= QuizWindow_Closed;

            if (_activeHistoryWindow != null)
                _activeHistoryWindow.Closed -= HistoryWindow_Closed;

            DataContext = null;
            base.OnClosed(e);
        }

        #endregion
    }
}
