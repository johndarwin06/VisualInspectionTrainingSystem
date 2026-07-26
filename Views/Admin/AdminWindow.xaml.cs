#region Namespaces

using System;
using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Admin
{
    /// <summary>
    /// Hosts the administrator review ViewModel and forwards window lifecycle cleanup only.
    /// </summary>
    public partial class AdminWindow : Window
    {
        #region Fields

        private bool _isDisposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates the administrator review window.
        /// </summary>
        public AdminWindow()
        {
            InitializeComponent();
            DataContext = new AdminViewModel();
            Closed += OnWindowClosed;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Cancels ViewModel work once without adding data access to code-behind.
        /// </summary>
        private void OnWindowClosed(
            object sender,
            EventArgs eventArgs)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            Closed -= OnWindowClosed;

            IDisposable disposable = DataContext as IDisposable;

            if (disposable != null)
                disposable.Dispose();

            DataContext = null;
        }

        #endregion
    }
}
