#region Namespaces

using System;
using System.Collections.Generic;
using System.Windows;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Result
{
    /// <summary>
    /// Displays the read-only result dashboard for one completed quiz.
    /// </summary>
    public partial class ResultWindow : Window
    {
        #region Fields

        private readonly ResultViewModel _viewModel;
        private bool _viewModelDisposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an empty result window for designer and safe fallback use.
        /// </summary>
        public ResultWindow()
            : this(new List<QuizAnswer>())
        {
        }

        /// <summary>
        /// Creates a result window for the supplied completed quiz answers.
        /// </summary>
        /// <param name="answers">Quiz answers to snapshot and display.</param>
        public ResultWindow(List<QuizAnswer> answers)
        {
            InitializeComponent();

            _viewModel = new ResultViewModel(
                answers ?? new List<QuizAnswer>());
            DataContext = _viewModel;
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Cancels image work before releasing the result window.
        /// </summary>
        /// <param name="e">Close event data.</param>
        protected override void OnClosed(EventArgs e)
        {
            if (!_viewModelDisposed)
            {
                _viewModelDisposed = true;
                _viewModel.Dispose();
                DataContext = null;
            }

            base.OnClosed(e);
        }

        #endregion
    }
}
