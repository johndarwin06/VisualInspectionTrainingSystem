#region Namespaces

using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Admin
{
    /// <summary>
    /// Interaction logic for AdminWindow.xaml.
    /// </summary>
    public partial class AdminWindow : Window
    {
        #region Constructor

        public AdminWindow()
        {
            InitializeComponent();

            DataContext = new AdminViewModel();
        }

        #endregion
    }
}
