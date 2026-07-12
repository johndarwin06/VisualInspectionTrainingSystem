#region Namespaces

using System.Windows;
using System.Windows.Input;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Quiz
{
    /// <summary>
    /// Interaction logic for QuizWindow.xaml
    /// </summary>
    public partial class QuizWindow : Window
    {
        #region Fields

        private readonly QuizViewModel _viewModel;

        #endregion

        #region Constructor

        public QuizWindow()
        {
            InitializeComponent();

            _viewModel = new QuizViewModel();

            DataContext = _viewModel;

            PreviewKeyDown += QuizWindow_PreviewKeyDown;
        }

        #endregion

        #region Keyboard Shortcuts

        /// <summary>
        /// Handles keyboard shortcuts.
        /// G = GOOD
        /// N = NG
        /// ESC = Exit Training
        /// </summary>
        private void QuizWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.G:
                    if (_viewModel.GoodCommand.CanExecute(null))
                        _viewModel.GoodCommand.Execute(null);
                    break;

                case Key.N:
                    if (_viewModel.NgCommand.CanExecute(null))
                        _viewModel.NgCommand.Execute(null);
                    break;

                case Key.Escape:

                    MessageBoxResult result =
                        MessageBox.Show(
                            "Do you want to end the current training?",
                            "Exit Training",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        Close();
                    }

                    break;
            }
        }

        #endregion

        #region Window Events

        protected override void OnClosed(System.EventArgs e)
        {
            PreviewKeyDown -= QuizWindow_PreviewKeyDown;

            base.OnClosed(e);
        }

        #endregion
    }
}