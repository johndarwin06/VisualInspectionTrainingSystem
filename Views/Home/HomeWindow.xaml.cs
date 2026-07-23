#region Namespaces

using System;
using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;
using VisualInspectionTrainingSystem.Views.Quiz;

#endregion

namespace VisualInspectionTrainingSystem.Views.Home
{
    /// <summary>
    /// Hosts the trainee home screen and owns single-flight quiz-window navigation.
    /// </summary>
    public partial class HomeWindow : Window
    {
        #region Fields

        private readonly HomeViewModel _viewModel;

        private QuizWindow _activeQuizWindow;

        private bool _isClosing;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates the Home view model and attaches its training-navigation request.
        /// </summary>
        public HomeWindow()
        {
            InitializeComponent();

            _viewModel = new HomeViewModel();
            _viewModel.StartTrainingRequested += Vm_StartTrainingRequested;

            DataContext = _viewModel;
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Opens at most one quiz with the explicitly selected sample size and hides Home while it is active.
        /// </summary>
        private void Vm_StartTrainingRequested()
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
                quiz = new QuizWindow(
                    _viewModel.SelectedQuizSize);

                _activeQuizWindow = quiz;
                quiz.Closed += QuizWindow_Closed;

                quiz.Show();
                Hide();
            }
            catch
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
                        // Preserve the original startup failure for the view model's safe handler.
                    }
                }

                _activeQuizWindow = null;

                throw;
            }
        }

        /// <summary>
        /// Restores the existing Home window after normal completion or early cancellation.
        /// </summary>
        /// <param name="sender">The quiz window that closed.</param>
        /// <param name="e">Close event data.</param>
        private void QuizWindow_Closed(object sender, EventArgs e)
        {
            QuizWindow closedQuiz = sender as QuizWindow;

            if (!ReferenceEquals(
                    closedQuiz,
                    _activeQuizWindow))
            {
                return;
            }

            closedQuiz.Closed -= QuizWindow_Closed;
            _activeQuizWindow = null;

            if (_isClosing)
                return;

            Show();
            Activate();
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Detaches navigation handlers without reopening Home during application shutdown.
        /// </summary>
        /// <param name="e">Close event data.</param>
        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;
            _viewModel.StartTrainingRequested -= Vm_StartTrainingRequested;

            if (_activeQuizWindow != null)
                _activeQuizWindow.Closed -= QuizWindow_Closed;

            DataContext = null;

            base.OnClosed(e);
        }

        #endregion
    }
}
