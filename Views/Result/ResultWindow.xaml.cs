#region Namespaces

using System.Collections.Generic;
using System.Windows;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Views.Result
{
    /// <summary>
    /// Interaction logic for ResultWindow.xaml.
    /// </summary>
    public partial class ResultWindow : Window
    {
        #region Constructors

        public ResultWindow()
            : this(new List<QuizAnswer>())
        {
        }

        public ResultWindow(List<QuizAnswer> answers)
        {
            InitializeComponent();

            DataContext = new ResultViewModel(
                answers ?? new List<QuizAnswer>());
        }

        #endregion
    }
}
