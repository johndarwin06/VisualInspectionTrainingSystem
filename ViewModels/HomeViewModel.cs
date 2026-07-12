#region Namespaces

using System;
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
    /// Home screen ViewModel.
    /// </summary>
    public class HomeViewModel : BaseViewModel
    {
        #region Constructor

        public HomeViewModel()
        {
            StartTrainingCommand = new RelayCommand(StartTraining);

            AdminCommand = new RelayCommand(OpenAdmin);

            LogoutCommand = new RelayCommand(Logout);
        }

        #endregion

        #region Properties

        public string WelcomeMessage
        {
            get
            {
                return $"Welcome, {SessionService.CurrentUser.FullName}";
            }
        }

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

        public ICommand StartTrainingCommand { get; }

        public ICommand AdminCommand { get; }

        public ICommand LogoutCommand { get; }

        #endregion

        #region Methods

        private void StartTraining()
        {
            try
            {
                QuizWindow window = new QuizWindow();

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Quiz Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenAdmin()
        {
            AdminWindow window = new AdminWindow();

            window.Show();

            CloseCurrentWindow<VisualInspectionTrainingSystem.Views.Home.HomeWindow>();
        }

        private void Logout()
        {
            SessionService.Logout();

            LoginWindow window = new LoginWindow();

            window.Show();

            CloseCurrentWindow<VisualInspectionTrainingSystem.Views.Home.HomeWindow>();
        }

        #endregion

        #region Helpers

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
