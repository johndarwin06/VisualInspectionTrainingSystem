#region Namespaces

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Views.Result;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// ViewModel for the Quiz Window.
    /// </summary>
    public class QuizViewModel : BaseViewModel
    {
        #region Fields

        private readonly ImageService _imageService;

        private readonly RelayCommand _goodCommand;

        private readonly RelayCommand _ngCommand;

        private QuizEngine _quizEngine;

        private BitmapImage _currentImage;

        private string _progress;

        private string _currentUser;

        private bool _isFinished;

        #endregion

        #region Constructor

        public QuizViewModel()
        {
            _imageService = new ImageService();

            _goodCommand = new RelayCommand(
                OnGood,
                CanSubmitAnswer);

            _ngCommand = new RelayCommand(
                OnNg,
                CanSubmitAnswer);

            GoodCommand = _goodCommand;

            NgCommand = _ngCommand;

            InitializeQuiz();
        }

        #endregion

        #region Properties

        public BitmapImage CurrentImage
        {
            get
            {
                return _currentImage;
            }
            set
            {
                SetProperty(ref _currentImage, value);
            }
        }

        public string Progress
        {
            get
            {
                return _progress;
            }
            set
            {
                SetProperty(ref _progress, value);
            }
        }

        public string CurrentUser
        {
            get
            {
                return _currentUser;
            }
            set
            {
                SetProperty(ref _currentUser, value);
            }
        }

        public ICommand GoodCommand
        {
            get;
        }

        public ICommand NgCommand
        {
            get;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Loads quiz data and starts the quiz engine.
        /// </summary>
        private void InitializeQuiz()
        {
            try
            {
                User user = GetCurrentUser();

                CurrentUser = user.FullName;

                List<QuizImage> images =
                    _imageService.LoadImages(AppConstants.QuizImageFolder);

                _quizEngine = new QuizEngine(
                    user,
                    images);

                if (_quizEngine.IsCompleted())
                {
                    Progress = _quizEngine.Progress;

                    CurrentImage = null;

                    ShowNoImagesMessage();

                    RefreshCommands();

                    return;
                }

                DisplayCurrentImage();
            }
            catch (Exception ex)
            {
                _isFinished = true;

                CurrentImage = null;

                Progress = "0 / 0";

                MessageBox.Show(
                    ex.Message,
                    "Quiz Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                RefreshCommands();
            }
        }

        /// <summary>
        /// Returns the current logged-in user or a safe design-time fallback.
        /// </summary>
        private User GetCurrentUser()
        {
            if (SessionService.CurrentUser != null)
                return SessionService.CurrentUser;

            return new User
            {
                FullName = "Trainee",
                Role = UserRoles.User,
                IsActive = true,
                CreatedDate = DateTime.Now
            };
        }

        #endregion

        #region Display

        /// <summary>
        /// Displays the image currently selected by the quiz engine.
        /// </summary>
        private void DisplayCurrentImage()
        {
            if (_quizEngine == null ||
                _quizEngine.IsCompleted())
            {
                FinishQuiz();
                return;
            }

            QuizImage image = _quizEngine.CurrentImage;

            if (image == null)
            {
                FinishQuiz();
                return;
            }

            CurrentImage = LoadBitmap(image.FilePath);

            Progress = _quizEngine.Progress;

            RefreshCommands();
        }

        /// <summary>
        /// Loads an image from disk without keeping the file locked.
        /// </summary>
        private BitmapImage LoadBitmap(string filePath)
        {
            BitmapImage bitmap = new BitmapImage();

            bitmap.BeginInit();

            bitmap.CacheOption = BitmapCacheOption.OnLoad;

            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

            bitmap.UriSource = new Uri(
                filePath,
                UriKind.Absolute);

            bitmap.EndInit();

            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }

            return bitmap;
        }

        #endregion

        #region Commands

        private bool CanSubmitAnswer()
        {
            return !_isFinished &&
                   _quizEngine != null &&
                   _quizEngine.CanSubmitAnswer;
        }

        private void OnGood()
        {
            SubmitAnswer(QuizAnswerType.Good);
        }

        private void OnNg()
        {
            SubmitAnswer(QuizAnswerType.Ng);
        }

        #endregion

        #region Answer Handling

        /// <summary>
        /// Submits the selected answer through the quiz engine.
        /// </summary>
        private void SubmitAnswer(QuizAnswerType answer)
        {
            if (_quizEngine == null)
                return;

            bool accepted = _quizEngine.TrySubmitAnswer(answer);

            if (!accepted)
                return;

            if (_quizEngine.IsCompleted())
            {
                FinishQuiz();
                return;
            }

            DisplayCurrentImage();
        }

        #endregion

        #region Finish

        /// <summary>
        /// Handles quiz completion.
        /// </summary>
        private void FinishQuiz()
        {
            if (_isFinished)
                return;

            _isFinished = true;

            CurrentImage = null;

            if (_quizEngine != null)
            {
                if (_quizEngine.TotalQuestions > 0)
                {
                    Progress = $"{_quizEngine.TotalQuestions} / {_quizEngine.TotalQuestions}";
                }
                else
                {
                    Progress = _quizEngine.Progress;
                }
            }

            RefreshCommands();

            ShowResultWindow();

            CloseQuizWindow();
        }

        /// <summary>
        /// Shows the empty-image message.
        /// </summary>
        private void ShowNoImagesMessage()
        {
            _isFinished = true;

            MessageBox.Show(
                "No BMP images were found.",
                "Quiz",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        /// <summary>
        /// Closes the active quiz window.
        /// </summary>
        private void CloseQuizWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is Views.Quiz.QuizWindow)
                {
                    window.Close();
                    break;
                }
            }
        }

        /// <summary>
        /// Opens the result window for the completed quiz.
        /// </summary>
        private void ShowResultWindow()
        {
            if (_quizEngine == null)
                return;

            ResultWindow resultWindow =
                new ResultWindow(
                    new List<QuizAnswer>(_quizEngine.Session.Answers));

            resultWindow.Show();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Refreshes command enabled states.
        /// </summary>
        private void RefreshCommands()
        {
            _goodCommand.RaiseCanExecuteChanged();

            _ngCommand.RaiseCanExecuteChanged();
        }

        #endregion
    }
}
