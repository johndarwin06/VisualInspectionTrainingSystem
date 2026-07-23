#region Namespaces

using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;
using VisualInspectionTrainingSystem.Views.Quiz;

#endregion

namespace VisualInspectionTrainingSystem.Views.Home
{
    /// <summary>
    /// Hosts the trainee home screen and its navigation-only event bridge.
    /// </summary>
    public partial class HomeWindow : Window
    {
        #region Fields

        private readonly HomeViewModel _viewModel;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates the home view model and attaches the existing training-request event.
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
        /// Opens a quiz with the explicitly selected sample size when the existing event is raised.
        /// </summary>
        private void Vm_StartTrainingRequested()
        {
            QuizWindow quiz = new QuizWindow(
                _viewModel.SelectedQuizSize);

            quiz.Show();

            Hide();
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Detaches the navigation bridge when the home window closes.
        /// </summary>
        protected override void OnClosed(System.EventArgs e)
        {
            _viewModel.StartTrainingRequested -= Vm_StartTrainingRequested;

            DataContext = null;

            base.OnClosed(e);
        }

        #endregion
    }
}
