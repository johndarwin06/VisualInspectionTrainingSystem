#region Namespaces

using System;
using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Reports
{
    /// <summary>
    /// Provides lifecycle integration for the MVVM Reports window.
    /// </summary>
    public partial class ReportsWindow : Window
    {
        #region Constructors

        /// <summary>
        /// Initializes the Reports window and its production ViewModel.
        /// </summary>
        public ReportsWindow()
        {
            InitializeComponent();

            DataContext = new ReportsViewModel();
            Closed += OnWindowClosed;
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Cancels active report work when the window closes.
        /// </summary>
        private void OnWindowClosed(object sender, EventArgs eventArgs)
        {
            Closed -= OnWindowClosed;

            IDisposable disposableViewModel = DataContext as IDisposable;

            if (disposableViewModel != null)
            {
                disposableViewModel.Dispose();
            }
        }

        #endregion
    }
}
