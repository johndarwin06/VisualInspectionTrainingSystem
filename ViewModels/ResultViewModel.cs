#region Namespaces

using System.Collections.Generic;
using System.Linq;

using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Result window ViewModel.
    /// </summary>
    public class ResultViewModel : BaseViewModel
    {
        #region Constructor

        public ResultViewModel(List<QuizAnswer> answers)
        {
            Answers = answers;

            TotalQuestions = answers.Count;

            GoodAnswers = answers.Count(x => x.UserAnswer == QuizAnswerType.Good);

            NgAnswers = answers.Count(x => x.UserAnswer == QuizAnswerType.Ng);
        }

        #endregion

        #region Properties

        public List<QuizAnswer> Answers { get; }

        public int TotalQuestions { get; }

        public int GoodAnswers { get; }

        public int NgAnswers { get; }

        #endregion
    }
}