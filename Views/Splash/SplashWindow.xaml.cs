#region Namespaces

using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;
using VisualInspectionTrainingSystem.Views.Login;

#endregion

namespace VisualInspectionTrainingSystem.Views.Splash
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    public partial class SplashWindow : Window
    {
        #region Fields

        private readonly SplashViewModel _viewModel;

        #endregion

        #region Constructor

        public SplashWindow()
        {
            InitializeComponent();

            _viewModel = new SplashViewModel();

            _viewModel.InitializationCompleted += ViewModel_InitializationCompleted;

            DataContext = _viewModel;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Opens the Login Window after initialization completes.
        /// </summary>
        private void ViewModel_InitializationCompleted(object sender, System.EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LoginWindow loginWindow = new LoginWindow();

                loginWindow.Show();

                Close();
            });
        }

        #endregion
    }
}