#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Views.Admin;
using VisualInspectionTrainingSystem.Views.Dashboard;
using VisualInspectionTrainingSystem.Views.Login;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// ViewModel for the administrator visual review screen.
    /// </summary>
    public class AdminViewModel : BaseViewModel
    {
        #region Constants

        private const string FilterAll = "All";

        private const string FilterPending = "Pending";

        private const string FilterReviewed = "Reviewed";

        #endregion

        #region Fields

        private readonly AnswerRepository _answerRepository;

        private readonly ImageService _imageService;

        private readonly List<QuizAnswer> _allAnswers;

        private readonly RelayCommand _refreshCommand;

        private readonly RelayCommand _showAllCommand;

        private readonly RelayCommand _showPendingCommand;

        private readonly RelayCommand _showReviewedCommand;

        private readonly RelayCommand _openDashboardCommand;

        private readonly RelayCommand _logoutCommand;

        private readonly RelayCommand _markGoodCommand;

        private readonly RelayCommand _markNgCommand;

        private QuizAnswer _selectedAnswer;

        private BitmapImage _selectedImage;

        private string _selectedImageCaption;

        private string _selectedImageStatus;

        private string _activeFilter;

        private string _statusMessage;

        private string _imageCatalogWarning;

        private bool _isBusy;

        #endregion

        #region Constructor

        public AdminViewModel()
        {
            _answerRepository = new AnswerRepository();

            _imageService = new ImageService();

            _allAnswers = new List<QuizAnswer>();

            Answers = new ObservableCollection<QuizAnswer>();

            _activeFilter = FilterPending;

            _refreshCommand = new RelayCommand(
                LoadAnswers,
                CanRunCommand);

            _showAllCommand = new RelayCommand(
                ShowAll,
                CanRunCommand);

            _showPendingCommand = new RelayCommand(
                ShowPending,
                CanRunCommand);

            _showReviewedCommand = new RelayCommand(
                ShowReviewed,
                CanRunCommand);

            _openDashboardCommand = new RelayCommand(
                OpenDashboard,
                CanRunCommand);

            _logoutCommand = new RelayCommand(
                Logout,
                CanRunCommand);

            _markGoodCommand = new RelayCommand(
                MarkSelectedGood,
                CanReviewSelectedAnswer);

            _markNgCommand = new RelayCommand(
                MarkSelectedNg,
                CanReviewSelectedAnswer);

            RefreshCommand = _refreshCommand;

            ShowAllCommand = _showAllCommand;

            ShowPendingCommand = _showPendingCommand;

            ShowReviewedCommand = _showReviewedCommand;

            OpenDashboardCommand = _openDashboardCommand;

            LogoutCommand = _logoutCommand;

            MarkGoodCommand = _markGoodCommand;

            MarkNgCommand = _markNgCommand;

            SelectedImageCaption = "No answer selected";

            SelectedImageStatus = "Select an answer to preview the inspection image.";

            LoadAnswers();
        }

        #endregion

        #region Collections

        public ObservableCollection<QuizAnswer> Answers
        {
            get;
        }

        #endregion

        #region Selected Answer

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
                    RefreshSelectedImage();

                    NotifySelectedAnswerChanged();

                    RefreshCommands();
                }
            }
        }

        public BitmapImage SelectedImage
        {
            get
            {
                return _selectedImage;
            }
            set
            {
                SetProperty(ref _selectedImage, value);
            }
        }

        public string SelectedImageCaption
        {
            get
            {
                return _selectedImageCaption;
            }
            set
            {
                SetProperty(ref _selectedImageCaption, value);
            }
        }

        public string SelectedImageStatus
        {
            get
            {
                return _selectedImageStatus;
            }
            set
            {
                SetProperty(ref _selectedImageStatus, value);
            }
        }

        public string SelectedUserAnswerText
        {
            get
            {
                if (SelectedAnswer == null)
                    return "-";

                return FormatAnswer(SelectedAnswer.UserAnswer);
            }
        }

        public string SelectedCorrectAnswerText
        {
            get
            {
                if (SelectedAnswer == null ||
                    !SelectedAnswer.CorrectAnswer.HasValue)
                {
                    return "Pending";
                }

                return FormatAnswer(SelectedAnswer.CorrectAnswer.Value);
            }
        }

        public string SelectedIsCorrectText
        {
            get
            {
                if (SelectedAnswer == null)
                    return "-";

                if (!SelectedAnswer.IsReviewed)
                    return "Pending";

                return SelectedAnswer.IsCorrect ? "Correct" : "Wrong";
            }
        }

        public string SelectedAnswerTimeText
        {
            get
            {
                if (SelectedAnswer == null)
                    return "-";

                return SelectedAnswer.AnswerTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        #endregion

        #region Review State

        public string ActiveFilter
        {
            get
            {
                return _activeFilter;
            }
            private set
            {
                if (SetProperty(ref _activeFilter, value))
                {
                    OnPropertyChanged(nameof(FilterSummary));
                }
            }
        }

        public string FilterSummary
        {
            get
            {
                return $"{ActiveFilter} view - {VisibleAnswers} shown";
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

        #endregion

        #region Summary

        public int TotalAnswers
        {
            get
            {
                return _allAnswers.Count;
            }
        }

        public int ReviewedAnswers
        {
            get
            {
                int count = 0;

                foreach (QuizAnswer answer in _allAnswers)
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

        public int VisibleAnswers
        {
            get
            {
                return Answers.Count;
            }
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand
        {
            get;
        }

        public ICommand ShowAllCommand
        {
            get;
        }

        public ICommand ShowPendingCommand
        {
            get;
        }

        public ICommand ShowReviewedCommand
        {
            get;
        }

        public ICommand OpenDashboardCommand
        {
            get;
        }

        public ICommand LogoutCommand
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
            LoadAnswers(null);
        }

        /// <summary>
        /// Loads saved quiz answers and optionally restores selection.
        /// </summary>
        private void LoadAnswers(int? preferredAnswerId)
        {
            try
            {
                IsBusy = true;

                _imageCatalogWarning = string.Empty;

                Dictionary<int, QuizImage> imagesById = LoadImageCatalog();

                _allAnswers.Clear();

                foreach (QuizAnswer answer in _answerRepository.GetForReview())
                {
                    AttachImageInfo(
                        answer,
                        imagesById);

                    _allAnswers.Add(answer);
                }

                ApplyFilter(preferredAnswerId);

                StatusMessage = BuildLoadedStatus();

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

        /// <summary>
        /// Loads the local quiz image folder used by training.
        /// </summary>
        private Dictionary<int, QuizImage> LoadImageCatalog()
        {
            Dictionary<int, QuizImage> imagesById = new Dictionary<int, QuizImage>();

            try
            {
                foreach (QuizImage image in _imageService.LoadImages(
                    AppConstants.QuizImageFolder,
                    false))
                {
                    if (image != null &&
                        !imagesById.ContainsKey(image.ImageID))
                    {
                        imagesById.Add(
                            image.ImageID,
                            image);
                    }
                }
            }
            catch (Exception ex)
            {
                _imageCatalogWarning =
                    "Image previews are unavailable: " + ex.Message;
            }

            return imagesById;
        }

        /// <summary>
        /// Adds file information to an answer from the local image catalog.
        /// </summary>
        private static void AttachImageInfo(
            QuizAnswer answer,
            Dictionary<int, QuizImage> imagesById)
        {
            if (answer == null ||
                imagesById == null)
            {
                return;
            }

            QuizImage image;

            if (!imagesById.TryGetValue(
                answer.ImageID,
                out image) ||
                image == null)
            {
                return;
            }

            answer.FileName = image.FileName;

            answer.FilePath = image.FilePath;
        }

        #endregion

        #region Filtering

        private void ShowAll()
        {
            SetFilter(FilterAll);
        }

        private void ShowPending()
        {
            SetFilter(FilterPending);
        }

        private void ShowReviewed()
        {
            SetFilter(FilterReviewed);
        }

        private void SetFilter(string filter)
        {
            ActiveFilter = filter;

            ApplyFilter(null);

            StatusMessage = BuildLoadedStatus();
        }

        /// <summary>
        /// Applies the active review filter to the visible answer queue.
        /// </summary>
        private void ApplyFilter(int? preferredAnswerId)
        {
            Answers.Clear();

            foreach (QuizAnswer answer in _allAnswers)
            {
                if (ShouldShowAnswer(answer))
                {
                    Answers.Add(answer);
                }
            }

            SelectedAnswer = FindPreferredAnswer(preferredAnswerId);

            NotifySummaryChanged();
        }

        private bool ShouldShowAnswer(QuizAnswer answer)
        {
            if (answer == null)
                return false;

            if (ActiveFilter == FilterPending)
                return !answer.IsReviewed;

            if (ActiveFilter == FilterReviewed)
                return answer.IsReviewed;

            return true;
        }

        private QuizAnswer FindPreferredAnswer(int? preferredAnswerId)
        {
            if (preferredAnswerId.HasValue)
            {
                foreach (QuizAnswer answer in Answers)
                {
                    if (answer != null &&
                        answer.AnswerID == preferredAnswerId.Value)
                    {
                        return answer;
                    }
                }
            }

            if (Answers.Count > 0)
                return Answers[0];

            return null;
        }

        #endregion

        #region Navigation

        private void OpenDashboard()
        {
            DashboardWindow window = new DashboardWindow();

            window.Show();
        }

        private void Logout()
        {
            SessionService.Logout();

            LoginWindow window = new LoginWindow();

            window.Show();

            CloseWindow<DashboardWindow>();

            CloseWindow<AdminWindow>();
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

            int selectedAnswerId = SelectedAnswer.AnswerID;

            try
            {
                IsBusy = true;

                _answerRepository.ReviewAnswer(
                    selectedAnswerId,
                    correctAnswer);

                StatusMessage =
                    $"Answer {selectedAnswerId} reviewed as {FormatAnswer(correctAnswer)}.";

                LoadAnswers(selectedAnswerId);
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

        #region Image Preview

        /// <summary>
        /// Refreshes the large preview image for the selected answer.
        /// </summary>
        private void RefreshSelectedImage()
        {
            SelectedImage = null;

            if (SelectedAnswer == null)
            {
                SelectedImageCaption = "No answer selected";

                SelectedImageStatus = "Select an answer to preview the inspection image.";

                return;
            }

            SelectedImageCaption = string.IsNullOrWhiteSpace(SelectedAnswer.FileName)
                ? $"Image {SelectedAnswer.ImageID}"
                : SelectedAnswer.FileName;

            if (string.IsNullOrWhiteSpace(SelectedAnswer.FilePath))
            {
                SelectedImageStatus =
                    $"No local file path found for ImageID {SelectedAnswer.ImageID}.";

                return;
            }

            if (!File.Exists(SelectedAnswer.FilePath))
            {
                SelectedImageStatus =
                    $"Image file not found: {SelectedAnswer.FilePath}";

                return;
            }

            try
            {
                SelectedImage = LoadBitmap(SelectedAnswer.FilePath);

                SelectedImageStatus = SelectedAnswer.FilePath;
            }
            catch (Exception ex)
            {
                SelectedImage = null;

                SelectedImageStatus =
                    "Image could not be opened: " + ex.Message;
            }
        }

        /// <summary>
        /// Loads a bitmap without locking the source file.
        /// </summary>
        private static BitmapImage LoadBitmap(string filePath)
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

            _showAllCommand.RaiseCanExecuteChanged();

            _showPendingCommand.RaiseCanExecuteChanged();

            _showReviewedCommand.RaiseCanExecuteChanged();

            _openDashboardCommand.RaiseCanExecuteChanged();

            _logoutCommand.RaiseCanExecuteChanged();

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

            OnPropertyChanged(nameof(VisibleAnswers));

            OnPropertyChanged(nameof(FilterSummary));
        }

        /// <summary>
        /// Raises selected answer detail property changes.
        /// </summary>
        private void NotifySelectedAnswerChanged()
        {
            OnPropertyChanged(nameof(SelectedUserAnswerText));

            OnPropertyChanged(nameof(SelectedCorrectAnswerText));

            OnPropertyChanged(nameof(SelectedIsCorrectText));

            OnPropertyChanged(nameof(SelectedAnswerTimeText));
        }

        private string BuildLoadedStatus()
        {
            string status =
                $"Loaded {TotalAnswers} answer(s). {FilterSummary}.";

            if (!string.IsNullOrWhiteSpace(_imageCatalogWarning))
            {
                status += " " + _imageCatalogWarning;
            }

            return status;
        }

        private static string FormatAnswer(QuizAnswerType answer)
        {
            return answer.ToString().ToUpperInvariant();
        }

        private static void CloseWindow<T>()
            where T : Window
        {
            for (int index = Application.Current.Windows.Count - 1; index >= 0; index--)
            {
                Window window = Application.Current.Windows[index];

                if (window is T)
                {
                    window.Close();
                }
            }
        }

        #endregion
    }
}
