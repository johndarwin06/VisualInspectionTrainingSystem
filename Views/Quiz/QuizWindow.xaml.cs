#region Namespaces

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Quiz
{
    /// <summary>
    /// Hosts the quiz view and routes local keyboard shortcuts to its view model commands.
    /// </summary>
    public partial class QuizWindow : Window
    {
        #region Fields

        private readonly QuizViewModel _viewModel;

        private bool _isAnswerShortcutPending;

        private bool _isExitConfirmationVisible;

        private bool _isClosing;

        private bool _isViewModelDisposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates the quiz view model and subscribes to this window's local keyboard events.
        /// </summary>
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
        /// Routes G, N, and Escape through one local keyboard path.
        /// </summary>
        /// <param name="sender">The window that received the key event.</param>
        /// <param name="e">The key event information.</param>
        private void QuizWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.G:
                    e.Handled = true;
                    ExecuteAnswerShortcut(_viewModel.GoodCommand, e.IsRepeat);
                    break;

                case Key.N:
                    e.Handled = true;
                    ExecuteAnswerShortcut(_viewModel.NgCommand, e.IsRepeat);
                    break;

                case Key.Escape:
                    e.Handled = true;
                    ConfirmExit();
                    break;
            }
        }

        /// <summary>
        /// Executes an existing answer command at most once for the current input turn.
        /// </summary>
        /// <param name="command">The existing GOOD or NG command.</param>
        /// <param name="isRepeat">Whether Windows identified this key event as key repeat.</param>
        private void ExecuteAnswerShortcut(ICommand command, bool isRepeat)
        {
            if (_isClosing ||
                _isExitConfirmationVisible ||
                _isAnswerShortcutPending ||
                isRepeat ||
                command == null ||
                !command.CanExecute(null))
            {
                return;
            }

            _isAnswerShortcutPending = true;
            command.Execute(null);

            Dispatcher.BeginInvoke(
                new Action(ReleaseAnswerShortcut),
                DispatcherPriority.ContextIdle);
        }

        /// <summary>
        /// Releases the keyboard turn gate after queued input and display transitions have completed.
        /// </summary>
        private void ReleaseAnswerShortcut()
        {
            _isAnswerShortcutPending = false;
        }

        /// <summary>
        /// Shows one exit confirmation and cancels the incomplete quiz only when the user confirms.
        /// </summary>
        private void ConfirmExit()
        {
            if (_isClosing ||
                _isExitConfirmationVisible)
            {
                return;
            }

            _isExitConfirmationVisible = true;

            try
            {
                MessageBoxResult result =
                    MessageBox.Show(
                        "Do you want to end the current training?",
                        "Exit Training",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _isClosing = true;
                    _viewModel.CancelQuiz();
                    Close();
                }
            }
            finally
            {
                _isExitConfirmationVisible = false;
            }
        }

        #endregion

        #region Window Events

        /// <summary>
        /// Detaches the local keyboard handler and releases the view model exactly once.
        /// </summary>
        /// <param name="e">The close event arguments.</param>
        protected override void OnClosed(EventArgs e)
        {
            PreviewKeyDown -= QuizWindow_PreviewKeyDown;

            if (!_isViewModelDisposed)
            {
                _isViewModelDisposed = true;
                _viewModel.Dispose();
            }

            DataContext = null;

            base.OnClosed(e);
        }

        #endregion
    }
}
