#region Namespaces

using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Home
{
    public partial class HomeWindow : Window
    {
        public HomeWindow()
        {
            InitializeComponent();

            HomeViewModel vm = new HomeViewModel();

            DataContext = vm;
        }
    }
}
