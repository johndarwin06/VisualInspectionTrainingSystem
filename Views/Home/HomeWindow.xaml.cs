#region Namespaces

using System;
using System.Windows;
using VisualInspectionTrainingSystem.ViewModels;
using VisualInspectionTrainingSystem.Views.Quiz;

#endregion

namespace VisualInspectionTrainingSystem.Views.Home
{
    public partial class HomeWindow : Window
    {
        public HomeWindow()
        {
            InitializeComponent();

            HomeViewModel vm = new HomeViewModel();

            vm.StartTrainingRequested += Vm_StartTrainingRequested;

            DataContext = vm;
        }

        private void Vm_StartTrainingRequested()
        {
            QuizWindow quiz = new QuizWindow();

            quiz.Show();

            Hide();
        }
    }
}