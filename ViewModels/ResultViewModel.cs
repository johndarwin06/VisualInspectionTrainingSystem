#region Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// ViewModel for the result window.
    /// </summary>
    public class ResultViewModel : BaseViewModel
    {
        #region Constructor

        public ResultViewModel(List<QuizAnswer> answers)
        {
            Answers = answers ?? new List<QuizAnswer>();

            CompletedAt = DateTime.Now;

            TotalQuestions = Answers.Count;

            GoodAnswers = Answers.Count(
                answer => answer.UserAnswer == QuizAnswerType.Good);

            NgAnswers = Answers.Count(
                answer => answer.UserAnswer == QuizAnswerType.Ng);

            ReviewedAnswers = Answers.Count(
                answer => answer.IsReviewed);

            PendingReviewAnswers = TotalQuestions - ReviewedAnswers;

            CorrectAnswers = Answers.Count(
                answer => answer.IsReviewed &&
                          answer.IsCorrect);

            WrongAnswers = Answers.Count(
                answer => answer.IsReviewed &&
                          !answer.IsCorrect);

            TotalElapsedSeconds = Math.Round(
                Answers.Sum(answer => answer.ElapsedSeconds),
                2);

            AverageElapsedSeconds = TotalQuestions == 0
                ? 0
                : Math.Round(
                    TotalElapsedSeconds / TotalQuestions,
                    2);
        }

        #endregion

        #region Properties

        public List<QuizAnswer> Answers
        {
            get;
        }

        public int TotalQuestions
        {
            get;
        }

        public int GoodAnswers
        {
            get;
        }

        public int NgAnswers
        {
            get;
        }

        public int ReviewedAnswers
        {
            get;
        }

        public int PendingReviewAnswers
        {
            get;
        }

        public int CorrectAnswers
        {
            get;
        }

        public int WrongAnswers
        {
            get;
        }

        public DateTime CompletedAt
        {
            get;
        }

        public double TotalElapsedSeconds
        {
            get;
        }

        public double AverageElapsedSeconds
        {
            get;
        }

        public string CompletedAtText
        {
            get
            {
                return CompletedAt.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        public string TotalElapsedText
        {
            get
            {
                return $"{TotalElapsedSeconds:0.00} s";
            }
        }

        public string AverageElapsedText
        {
            get
            {
                return $"{AverageElapsedSeconds:0.00} s";
            }
        }

        public string ReviewStatusText
        {
            get
            {
                if (TotalQuestions == 0)
                    return "No answers recorded";

                if (PendingReviewAnswers == 0)
                    return "Review completed";

                return $"{PendingReviewAnswers} pending review";
            }
        }

        public string AccuracyText
        {
            get
            {
                if (ReviewedAnswers == 0)
                    return "Pending Review";

                double accuracy =
                    (double)CorrectAnswers /
                    ReviewedAnswers * 100;

                return $"{accuracy:0.00}%";
            }
        }

        #endregion
    }
}
