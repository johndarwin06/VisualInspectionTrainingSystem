#region Namespaces

using System;
using VisualInspectionTrainingSystem.ViewModels;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

#endregion

namespace VisualInspectionTrainingSystem.Views.Admin
{
    /// <summary>
    /// Hosts the focused administrator review workflow and forwards window lifecycle cleanup only.
    /// </summary>
    public partial class AdminWindow : FluentWindow
    {
        #region Fields

        private bool _isDisposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates the administrator review workflow window.
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
            {
                return;
            }

            _isDisposed = true;
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
