#region Namespaces

using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Login
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        #region Constructor

        public LoginWindow()
        {
            InitializeComponent();

            DataContext = new LoginViewModel();
        }

        #endregion
    }
}