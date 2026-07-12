#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Views;
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

        private List<QuizImage> _images;

        private readonly List<QuizAnswer> _answers;

        private int _currentIndex;

        private BitmapImage _currentImage;

        private string _progress;

        private string _currentUser;

        #endregion

        #region Constructor

        public QuizViewModel()
        {
            _imageService = new ImageService();

            _answers = new List<QuizAnswer>();

            GoodCommand = new RelayCommand(OnGood);

            NgCommand = new RelayCommand(OnNg);

            LoadImages();
        }

        #endregion

        #region Properties

        public BitmapImage CurrentImage
        {
            get => _currentImage;
            set => SetProperty(ref _currentImage, value);
        }

        public string Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        public ICommand GoodCommand { get; }

        public ICommand NgCommand { get; }

        #endregion

        #region Load Images

        private void LoadImages()
        {
            CurrentUser = SessionService.CurrentUser.FullName;

            _images = _imageService.LoadImages(AppConstants.QuizImageFolder);

            if (_images == null || _images.Count == 0)
            {
                MessageBox.Show(
                    "No BMP images were found.",
                    "Quiz",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            _currentIndex = 0;

            DisplayCurrentImage();
        }

        #endregion

        #region Display Image

        private void DisplayCurrentImage()
        {
            if (_currentIndex >= _images.Count)
            {
                FinishQuiz();
                return;
            }

            QuizImage image = _images[_currentIndex];

            CurrentImage = new BitmapImage();

            CurrentImage.BeginInit();
            CurrentImage.CacheOption = BitmapCacheOption.OnLoad;
            CurrentImage.UriSource = new Uri(image.FilePath);
            CurrentImage.EndInit();

            Progress = $"{_currentIndex + 1} / {_images.Count}";
        }

        #endregion

        #region GOOD

        private void OnGood()
        {
            SaveAnswer(QuizAnswerType.Good);
        }

        #endregion

        #region NG

        private void OnNg()
        {
            SaveAnswer(QuizAnswerType.Ng);
        }

        #endregion

        #region Save Answer

        private void SaveAnswer(QuizAnswerType answer)
        {
            QuizImage image = _images[_currentIndex];

            _answers.Add(new QuizAnswer
            {
                ImageID = image.ImageID,
                FileName = image.FileName,
                FilePath = image.FilePath,
                UserAnswer = answer,
                AnswerTime = DateTime.Now
            });

            _currentIndex++;

            DisplayCurrentImage();
        }

        #endregion

        #region Finish

        private void FinishQuiz()
        {
            /*ResultWindow resultWindow =
                new ResultWindow(_answers);

            resultWindow.Show();*/

            MessageBox.Show(
    "Training Completed!",
    "Quiz",
    MessageBoxButton.OK,
    MessageBoxImage.Information);

            foreach (Window window in Application.Current.Windows)
            {
                if (window is Views.Quiz.QuizWindow)
                {
                    window.Close();
                    break;
                }
            }
        }

        #endregion
    }
}