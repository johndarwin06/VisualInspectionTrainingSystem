#region Namespaces

using System;
using System.ComponentModel;
using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;
using VisualInspectionTrainingSystem.Views.Login;

#endregion

namespace VisualInspectionTrainingSystem.Views.Splash
{
    /// <summary>
    /// Displays startup progress and opens the login screen after initialization succeeds.
    /// </summary>
    public partial class SplashWindow : Window
    {
        #region Fields

        private readonly SplashViewModel _viewModel;
        private bool _isClosing;
        private bool _loginOpened;
        private bool _startupRequested;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SplashWindow"/> class.
        /// </summary>
        public SplashWindow()
        {
            InitializeComponent();

            _viewModel = new SplashViewModel();
            _viewModel.InitializationCompleted += ViewModel_InitializationCompleted;
            _viewModel.CloseRequested += ViewModel_CloseRequested;

            DataContext = _viewModel;

            Loaded += SplashWindow_Loaded;
            Closing += SplashWindow_Closing;
            Closed += SplashWindow_Closed;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Starts initialization after the splash window is visible.
        /// </summary>
        private async void SplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_startupRequested)
            {
                return;
            }

            _startupRequested = true;

            await _viewModel.StartInitializationAsync();
        }

        /// <summary>
        /// Opens the login window after startup initialization succeeds.
        /// </summary>
        private void ViewModel_InitializationCompleted(object sender, EventArgs e)
        {
            OpenLoginWindowOnce();
        }

        /// <summary>
        /// Closes the splash window when the view model requests shutdown.
        /// </summary>
        private void ViewModel_CloseRequested(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Cancels initialization when the splash window is closing.
        /// </summary>
        private void SplashWindow_Closing(object sender, CancelEventArgs e)
        {
            _isClosing = true;
            _viewModel.CancelInitialization();
        }

        /// <summary>
        /// Releases event subscriptions owned by the splash window.
        /// </summary>
        private void SplashWindow_Closed(object sender, EventArgs e)
        {
            _viewModel.InitializationCompleted -= ViewModel_InitializationCompleted;
            _viewModel.CloseRequested -= ViewModel_CloseRequested;

            Loaded -= SplashWindow_Loaded;
            Closing -= SplashWindow_Closing;
            Closed -= SplashWindow_Closed;
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Opens the login window only once after successful startup initialization.
        /// </summary>
        private void OpenLoginWindowOnce()
        {
            if (_loginOpened || _isClosing)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(OpenLoginWindowOnce));
                return;
            }

            if (_loginOpened || _isClosing)
            {
                return;
            }

            _loginOpened = true;

            LoginWindow loginWindow = new LoginWindow();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();

            Close();
        }

        #endregion
    }
}
