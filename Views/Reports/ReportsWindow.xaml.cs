#region Namespaces

using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Reports
{
    /// <summary>
    /// Interaction logic for ReportsWindow.xaml.
    /// </summary>
    public partial class ReportsWindow : Window
    {
        #region Constructor

        public ReportsWindow()
        {
            InitializeComponent();

            DataContext = new ReportsViewModel();
        }

        #endregion
    }
}
