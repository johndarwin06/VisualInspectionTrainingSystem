#region Namespaces

using System;
using System.Windows;
using MahApps.Metro.Controls;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Admin
{
    /// <summary>
    /// Hosts the User Management ViewModel and forwards window lifecycle events only.
    /// </summary>
    public partial class UserManagementWindow : MetroWindow
    {
        #region Fields

        private readonly UserManagementViewModel _viewModel;
        private bool _isDisposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates the administrator-only User Management window.
        /// </summary>
        public UserManagementWindow()
        {
            InitializeComponent();
            _viewModel = new UserManagementViewModel();
            _viewModel.CloseRequested += OnCloseRequested;
            DataContext = _viewModel;
            Closed += OnWindowClosed;
        }

        #endregion

        #region Lifecycle

        private void OnCloseRequested()
        {
            Close();
        }

        /// <summary>
        /// Cancels ViewModel work and removes event subscriptions once.
        /// </summary>
        private void OnWindowClosed(
            object sender,
            EventArgs eventArgs)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            Closed -= OnWindowClosed;
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.Dispose();
            DataContext = null;
        }

        #endregion
    }
}
