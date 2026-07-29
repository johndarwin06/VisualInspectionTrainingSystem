#region Namespaces

using System;
using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;
using VisualInspectionTrainingSystem.Views.History;
using VisualInspectionTrainingSystem.Views.Quiz;
using VisualInspectionTrainingSystem.Views.Result;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

#endregion

namespace VisualInspectionTrainingSystem.Views.Home
{
    /// <summary>
    /// Hosts the focused trainee setup task and owns single-flight quiz and compatibility history navigation.
    /// </summary>
    public partial class HomeWindow : FluentWindow
    {
        #region Fields

        private readonly HomeViewModel _viewModel;
        private QuizWindow _activeQuizWindow;
        private ResultWindow _activeResultWindow;
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
                SetNestedWindowOwner(quiz);
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
        /// <param name="sender">The quiz window that closed.</param>
        /// <param name="e">Unused close event information.</param>
        private void QuizWindow_Closed(object sender, EventArgs e)
        {
            QuizWindow closedQuiz = sender as QuizWindow;

            if (!ReferenceEquals(closedQuiz, _activeQuizWindow))
                return;

            closedQuiz.Closed -= QuizWindow_Closed;
            _activeQuizWindow = null;

            ResultWindow resultWindow = FindActiveResultWindow();

            if (resultWindow != null)
            {
                _activeResultWindow = resultWindow;
                resultWindow.Closed += ResultWindow_Closed;
                return;
            }

            RestoreHome();
        }

        /// <summary>
        /// Restores Home only after the completed quiz result has closed.
        /// </summary>
        /// <param name="sender">The completed-session result window.</param>
        /// <param name="e">Unused close event information.</param>
        private void ResultWindow_Closed(object sender, EventArgs e)
        {
            ResultWindow resultWindow = sender as ResultWindow;

            if (!ReferenceEquals(resultWindow, _activeResultWindow))
                return;

            resultWindow.Closed -= ResultWindow_Closed;
            _activeResultWindow = null;
            RestoreHome();
        }

        /// <summary>
        /// Releases a quiz that failed during window startup.
        /// </summary>
        /// <param name="quiz">Partially constructed quiz window, when available.</param>
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

        #region History Compatibility Navigation

        /// <summary>
        /// Opens at most one current-user history window for compatibility callers.
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
                SetNestedWindowOwner(historyWindow);
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
        /// Restores Home after a compatibility history window closes.
        /// </summary>
        /// <param name="sender">The history window that closed.</param>
        /// <param name="e">Unused close event information.</param>
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
        /// <param name="historyWindow">Partially constructed history window, when available.</param>
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

        #region Window Ownership

        /// <summary>
        /// Keeps nested workflow windows in the authenticated shell ownership tree.
        /// </summary>
        /// <param name="nestedWindow">New nested workflow window.</param>
        private void SetNestedWindowOwner(Window nestedWindow)
        {
            if (nestedWindow == null)
                throw new ArgumentNullException(nameof(nestedWindow));

            nestedWindow.Owner = Owner ?? this;
        }

        /// <summary>
        /// Finds the result created immediately before Quiz closes so Home cannot cover it.
        /// </summary>
        /// <returns>The visible result in this shell ownership tree, or null.</returns>
        private ResultWindow FindActiveResultWindow()
        {
            if (Application.Current == null)
                return null;

            Window expectedOwner = Owner ?? this;

            foreach (Window window in Application.Current.Windows)
            {
                ResultWindow resultWindow = window as ResultWindow;

                if (resultWindow != null &&
                    resultWindow.IsVisible &&
                    ReferenceEquals(resultWindow.Owner, expectedOwner))
                {
                    return resultWindow;
                }
            }

            return null;
        }

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
        /// <param name="e">The close event information.</param>
        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;
            _viewModel.StartTrainingRequested -= ViewModel_StartTrainingRequested;
            _viewModel.HistoryRequested -= ViewModel_HistoryRequested;

            if (_activeQuizWindow != null)
                _activeQuizWindow.Closed -= QuizWindow_Closed;

            if (_activeResultWindow != null)
                _activeResultWindow.Closed -= ResultWindow_Closed;

            if (_activeHistoryWindow != null)
                _activeHistoryWindow.Closed -= HistoryWindow_Closed;

            DataContext = null;
            base.OnClosed(e);
        }

        #endregion
    }
}
