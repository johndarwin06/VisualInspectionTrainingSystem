#region Namespaces

using System;
using System.Windows;
using MahApps.Metro.Controls;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Dashboard
{
    /// <summary>
    /// Hosts the administrator Dashboard and cancels refresh work during window closure.
    /// </summary>
    public partial class DashboardWindow : MetroWindow
    {
        #region Constructors

        /// <summary>
        /// Initializes the production Dashboard ViewModel.
        /// </summary>
        public DashboardWindow()
        {
            InitializeComponent();
            DataContext = new DashboardViewModel();
            Closed += OnWindowClosed;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Disposes cancellable Dashboard state before releasing the window binding.
        /// </summary>
        private void OnWindowClosed(
            object sender,
            EventArgs eventArgs)
        {
            Closed -= OnWindowClosed;

            IDisposable disposable = DataContext as IDisposable;

            if (disposable != null)
            {
                disposable.Dispose();
            }

            DataContext = null;
        }

        #endregion
    }
}
