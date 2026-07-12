#region Namespaces

using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Dashboard
{
    /// <summary>
    /// Interaction logic for DashboardWindow.xaml.
    /// </summary>
    public partial class DashboardWindow : Window
    {
        #region Constructor

        public DashboardWindow()
        {
            InitializeComponent();

            DataContext = new DashboardViewModel();
        }

        #endregion
    }
}
