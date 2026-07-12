#region Namespaces

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// ViewModel for the administrator review screen.
    /// </summary>
    public class AdminViewModel : BaseViewModel
    {
        #region Fields

        private readonly AnswerRepository _answerRepository;

        private readonly RelayCommand _refreshCommand;

        private readonly RelayCommand _markGoodCommand;

        private readonly RelayCommand _markNgCommand;

        private QuizAnswer _selectedAnswer;

        private string _statusMessage;

        private bool _isBusy;

        #endregion

        #region Constructor

        public AdminViewModel()
        {
            _answerRepository = new AnswerRepository();

            Answers = new ObservableCollection<QuizAnswer>();

            _refreshCommand = new RelayCommand(
                LoadAnswers,
                CanRunCommand);

            _markGoodCommand = new RelayCommand(
                MarkSelectedGood,
                CanReviewSelectedAnswer);

            _markNgCommand = new RelayCommand(
                MarkSelectedNg,
                CanReviewSelectedAnswer);

            RefreshCommand = _refreshCommand;

            MarkGoodCommand = _markGoodCommand;

            MarkNgCommand = _markNgCommand;

            LoadAnswers();
        }

        #endregion

        #region Properties

        public ObservableCollection<QuizAnswer> Answers
        {
            get;
        }

        public QuizAnswer SelectedAnswer
        {
            get
            {
                return _selectedAnswer;
            }
            set
            {
                if (SetProperty(ref _selectedAnswer, value))
                {
                    RefreshCommands();
                }
            }
        }

        public string StatusMessage
        {
            get
            {
                return _statusMessage;
            }
            set
            {
                SetProperty(ref _statusMessage, value);
            }
        }

        public bool IsBusy
        {
            get
            {
                return _isBusy;
            }
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RefreshCommands();
                }
            }
        }

        public int TotalAnswers
        {
            get
            {
                return Answers.Count;
            }
        }

        public int ReviewedAnswers
        {
            get
            {
                int count = 0;

                foreach (QuizAnswer answer in Answers)
                {
                    if (answer != null &&
                        answer.IsReviewed)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int PendingAnswers
        {
            get
            {
                return TotalAnswers - ReviewedAnswers;
            }
        }

        public ICommand RefreshCommand
        {
            get;
        }

        public ICommand MarkGoodCommand
        {
            get;
        }

        public ICommand MarkNgCommand
        {
            get;
        }

        #endregion

        #region Load

        /// <summary>
        /// Loads saved quiz answers from MySQL.
        /// </summary>
        private void LoadAnswers()
        {
            try
            {
                IsBusy = true;

                Answers.Clear();

                foreach (QuizAnswer answer in _answerRepository.GetForReview())
                {
                    Answers.Add(answer);
                }

                SelectedAnswer = null;

                StatusMessage = $"Loaded {TotalAnswers} answer(s).";

                NotifySummaryChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;

                MessageBox.Show(
                    ex.Message,
                    "Admin Review Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Review

        /// <summary>
        /// Marks the selected answer as GOOD.
        /// </summary>
        private void MarkSelectedGood()
        {
            ReviewSelectedAnswer(QuizAnswerType.Good);
        }

        /// <summary>
        /// Marks the selected answer as NG.
        /// </summary>
        private void MarkSelectedNg()
        {
            ReviewSelectedAnswer(QuizAnswerType.Ng);
        }

        /// <summary>
        /// Saves the administrator's correct answer.
        /// </summary>
        private void ReviewSelectedAnswer(QuizAnswerType correctAnswer)
        {
            if (SelectedAnswer == null)
                return;

            try
            {
                IsBusy = true;

                int selectedAnswerId = SelectedAnswer.AnswerID;

                _answerRepository.ReviewAnswer(
                    selectedAnswerId,
                    correctAnswer);

                StatusMessage =
                    $"Answer {selectedAnswerId} reviewed as {correctAnswer.ToString().ToUpperInvariant()}.";

                LoadAnswers();
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;

                MessageBox.Show(
                    ex.Message,
                    "Save Review Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Command State

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanReviewSelectedAnswer()
        {
            return !IsBusy &&
                   SelectedAnswer != null;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Refreshes command states.
        /// </summary>
        private void RefreshCommands()
        {
            _refreshCommand.RaiseCanExecuteChanged();

            _markGoodCommand.RaiseCanExecuteChanged();

            _markNgCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Raises summary property changes.
        /// </summary>
        private void NotifySummaryChanged()
        {
            OnPropertyChanged(nameof(TotalAnswers));

            OnPropertyChanged(nameof(ReviewedAnswers));

            OnPropertyChanged(nameof(PendingAnswers));
        }

        #endregion
    }
}
