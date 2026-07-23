#region Namespaces

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Views.Result;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Coordinates quiz display state, bounded image preloading, and answer submission.
    /// </summary>
    public class QuizViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        private const int MaximumCachedImages = 2;

        #endregion

        #region Fields

        private readonly ImageService _imageService;

        private readonly SessionRepository _sessionRepository;

        private readonly int _requestedQuizSize;

        private readonly string _imageFolderPath;

        private readonly RelayCommand _goodCommand;

        private readonly RelayCommand _ngCommand;

        private readonly object _imageCacheSyncRoot;

        private readonly Dictionary<string, BitmapImage> _imageCache;

        private readonly LinkedList<string> _imageCacheOrder;

        private QuizEngine _quizEngine;

        private CancellationTokenSource _imageLoadCancellation;

        private BitmapImage _currentImage;

        private string _progress;

        private string _questionProgressText;

        private string _answeredRemainingText;

        private string _imageStatus;

        private string _currentUser;

        private string _quizNotice;

        private int _currentQuestion;

        private int _totalQuestions;

        private int _answeredQuestions;

        private int _remainingQuestions;

        private double _completionPercentage;

        private bool _isImageLoading;

        private bool _isFinished;

        private bool _resultWindowShown;

        private int _imageLoadGeneration;

        private int _isDisposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a quiz using the default ten-question request.
        /// </summary>
        public QuizViewModel()
            : this(ImageService.DefaultQuizSize)
        {
        }

        /// <summary>
        /// Creates a quiz using one supported requested sample size.
        /// </summary>
        /// <param name="requestedQuizSize">Requested quiz size of 10 or 20.</param>
        public QuizViewModel(int requestedQuizSize)
            : this(
                ValidateAndReturnQuizSize(requestedQuizSize),
                new ImageService(),
                new SessionRepository(),
                AppConstants.QuizImageFolder)
        {
        }

        /// <summary>
        /// Creates the quiz with explicit dependencies for deterministic verification.
        /// </summary>
        internal QuizViewModel(
            int requestedQuizSize,
            ImageService imageService,
            SessionRepository sessionRepository,
            string imageFolderPath)
        {
            ValidateRequestedQuizSize(requestedQuizSize);

            if (imageService == null)
                throw new ArgumentNullException(nameof(imageService));

            if (sessionRepository == null)
                throw new ArgumentNullException(nameof(sessionRepository));

            if (string.IsNullOrWhiteSpace(imageFolderPath))
                throw new ArgumentException(
                    "Image folder path cannot be empty.",
                    nameof(imageFolderPath));

            _requestedQuizSize = requestedQuizSize;
            _imageService = imageService;
            _sessionRepository = sessionRepository;
            _imageFolderPath = imageFolderPath;
            _imageCacheSyncRoot = new object();
            _imageCache = new Dictionary<string, BitmapImage>(
                StringComparer.OrdinalIgnoreCase);
            _imageCacheOrder = new LinkedList<string>();

            _goodCommand = new RelayCommand(
                OnGood,
                CanSubmitAnswer);
            _ngCommand = new RelayCommand(
                OnNg,
                CanSubmitAnswer);

            GoodCommand = _goodCommand;
            NgCommand = _ngCommand;
            ImageStatus = "Preparing inspection image...";
            QuizNotice = "Preparing a " +
                         requestedQuizSize +
                         "-question quiz...";

            InitializeQuiz();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the frozen image displayed for the active question.
        /// </summary>
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

        /// <summary>
        /// Gets the legacy current-question progress text in current/total form.
        /// </summary>
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

        /// <summary>
        /// Gets the user-facing current-question progress text.
        /// </summary>
        public string QuestionProgressText
        {
            get
            {
                return _questionProgressText;
            }
            private set
            {
                SetProperty(ref _questionProgressText, value);
            }
        }

        /// <summary>
        /// Gets the user-facing answered and remaining question summary.
        /// </summary>
        public string AnsweredRemainingText
        {
            get
            {
                return _answeredRemainingText;
            }
            private set
            {
                SetProperty(ref _answeredRemainingText, value);
            }
        }

        /// <summary>
        /// Gets a non-sensitive current image loading status.
        /// </summary>
        public string ImageStatus
        {
            get
            {
                return _imageStatus;
            }
            private set
            {
                SetProperty(ref _imageStatus, value);
            }
        }

        /// <summary>
        /// Gets the active one-based question number, or zero for an empty quiz.
        /// </summary>
        public int CurrentQuestion
        {
            get
            {
                return _currentQuestion;
            }
            private set
            {
                SetProperty(ref _currentQuestion, value);
            }
        }

        /// <summary>
        /// Gets the number of questions loaded into the quiz.
        /// </summary>
        public int TotalQuestions
        {
            get
            {
                return _totalQuestions;
            }
            private set
            {
                SetProperty(ref _totalQuestions, value);
            }
        }

        /// <summary>
        /// Gets the number of accepted answers.
        /// </summary>
        public int AnsweredQuestions
        {
            get
            {
                return _answeredQuestions;
            }
            private set
            {
                SetProperty(ref _answeredQuestions, value);
            }
        }

        /// <summary>
        /// Gets the number of questions without an accepted answer.
        /// </summary>
        public int RemainingQuestions
        {
            get
            {
                return _remainingQuestions;
            }
            private set
            {
                SetProperty(ref _remainingQuestions, value);
            }
        }

        /// <summary>
        /// Gets the answered-question percentage in the inclusive range from zero to one hundred.
        /// </summary>
        public double CompletionPercentage
        {
            get
            {
                return _completionPercentage;
            }
            private set
            {
                SetProperty(ref _completionPercentage, value);
            }
        }

        /// <summary>
        /// Gets whether the active image is unavailable while it is loading or changing.
        /// </summary>
        public bool IsImageLoading
        {
            get
            {
                return _isImageLoading;
            }
            private set
            {
                SetProperty(ref _isImageLoading, value);
            }
        }

        /// <summary>
        /// Gets the active user's display name.
        /// </summary>
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

        /// <summary>
        /// Gets the supported quiz size requested from the trainee start screen.
        /// </summary>
        public int RequestedQuizSize
        {
            get
            {
                return _requestedQuizSize;
            }
        }

        /// <summary>
        /// Gets a fixed non-sensitive notice describing the actual quiz sample.
        /// </summary>
        public string QuizNotice
        {
            get
            {
                return _quizNotice;
            }
            private set
            {
                SetProperty(ref _quizNotice, value);
            }
        }

        /// <summary>
        /// Gets the command that records a GOOD answer.
        /// </summary>
        public ICommand GoodCommand
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the command that records an NG answer.
        /// </summary>
        public ICommand NgCommand
        {
            get;
            private set;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Loads quiz metadata and starts the first non-blocking bitmap load.
        /// </summary>
        private void InitializeQuiz()
        {
            try
            {
                User user = GetCurrentUser();

                CurrentUser = user.FullName;

                List<QuizImage> images =
                    _imageService.LoadQuizImages(
                        _imageFolderPath,
                        _requestedQuizSize);

                _quizEngine = new QuizEngine(
                    user,
                    images);

                QuizNotice = BuildQuizNotice(images.Count);
                UpdateProgress();

                if (_quizEngine.IsCompleted())
                {
                    CurrentImage = null;
                    ImageStatus = "No inspection images are available.";
                    ShowNoImagesMessage();
                    RefreshCommands();

                    return;
                }

                BeginDisplayCurrentImage();
            }
            catch (Exception ex)
            {
                _isFinished = true;
                CurrentImage = null;
                ImageStatus = "The quiz could not be started.";
                QuizNotice = "Training is unavailable.";
                UpdateProgress();

                ApplicationErrorLogger.LogUnhandledException(
                    "Quiz Initialization",
                    ex,
                    false);

                MessageBox.Show(
                    "The quiz could not be started. Verify that the training images are available and try again.",
                    "Quiz Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                RefreshCommands();
            }
        }

        /// <summary>
        /// Returns the current logged-in user or a safe design-time fallback.
        /// </summary>
        /// <returns>The user that owns the quiz session.</returns>
        private static User GetCurrentUser()
        {
            if (SessionService.CurrentUser != null)
            {
                return SessionService.CurrentUser;
            }

            return new User
            {
                FullName = "Trainee",
                Role = UserRoles.User,
                IsActive = true,
                CreatedDate = DateTime.Now
            };
        }

        #endregion

        #region Image Loading And Cache

        /// <summary>
        /// Begins the current-image task and observes its completion.
        /// </summary>
        private void BeginDisplayCurrentImage()
        {
            ObserveBackgroundTask(DisplayCurrentImageAsync());
        }

        /// <summary>
        /// Loads the active image off the UI thread, then queues one upcoming image for preload.
        /// </summary>
        /// <returns>A task that completes after the active image is available or safely rejected.</returns>
        private async Task DisplayCurrentImageAsync()
        {
            if (_quizEngine == null ||
                _quizEngine.IsCompleted())
            {
                FinishQuiz();

                return;
            }

            QuizImage image = _quizEngine.CurrentImage;

            if (image == null ||
                string.IsNullOrWhiteSpace(image.FilePath))
            {
                HandleCurrentImageFailure();

                return;
            }

            CancelPendingImageWork();

            int generation = Interlocked.Increment(
                ref _imageLoadGeneration);
            CancellationTokenSource cancellation =
                new CancellationTokenSource();

            _imageLoadCancellation = cancellation;
            IsImageLoading = true;
            CurrentImage = null;
            ImageStatus = "Loading inspection image...";
            RefreshCommands();

            try
            {
                BitmapImage bitmap;

                if (!TryGetCachedImage(
                        image.FilePath,
                        out bitmap))
                {
                    bitmap = await _imageService.LoadBitmapAsync(
                        image.FilePath,
                        cancellation.Token);

                    if (!IsCurrentImageLoad(
                            generation,
                            image,
                            cancellation.Token) ||
                        !CacheImageIfCurrent(
                            image.FilePath,
                            bitmap,
                            image.FilePath,
                            GetUpcomingImagePath(),
                            delegate
                            {
                                return IsCurrentImageLoad(
                                    generation,
                                    image,
                                    cancellation.Token);
                            }))
                    {
                        return;
                    }
                }

                if (!IsCurrentImageLoad(
                        generation,
                        image,
                        cancellation.Token))
                {
                    return;
                }

                CurrentImage = bitmap;
                ImageStatus = "Ready for inspection.";
                UpdateProgress();
                QueueUpcomingImagePreload(
                    generation,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // A newer question or closed window owns the display now.
            }
            catch
            {
                if (IsCurrentImageLoad(
                        generation,
                        image,
                        cancellation.Token))
                {
                    HandleCurrentImageFailure();
                }
            }
            finally
            {
                if (generation == _imageLoadGeneration &&
                    Interlocked.CompareExchange(
                        ref _isDisposed,
                        0,
                        0) == 0)
                {
                    IsImageLoading = false;
                    RefreshCommands();
                }
            }
        }

        /// <summary>
        /// Queues the next image only, keeping the current plus next bitmap cache bounded to two entries.
        /// </summary>
        /// <param name="generation">The active display generation.</param>
        /// <param name="cancellationToken">The lifetime token for the active quiz display.</param>
        private void QueueUpcomingImagePreload(
            int generation,
            CancellationToken cancellationToken)
        {
            QuizImage upcomingImage = GetUpcomingImage();

            if (upcomingImage == null ||
                string.IsNullOrWhiteSpace(upcomingImage.FilePath) ||
                IsImageCached(upcomingImage.FilePath))
            {
                return;
            }

            string currentImagePath = _quizEngine.CurrentImage == null
                ? null
                : _quizEngine.CurrentImage.FilePath;
            Task preloadTask = PreloadImageAsync(
                upcomingImage,
                currentImagePath,
                generation,
                cancellationToken);

            ObserveBackgroundTask(preloadTask);
        }

        /// <summary>
        /// Loads one upcoming image without changing UI state from a worker thread.
        /// </summary>
        /// <param name="image">The upcoming image to load.</param>
        /// <param name="currentImagePath">The current cache entry that must not be evicted.</param>
        /// <param name="generation">The display generation that queued this preload.</param>
        /// <param name="cancellationToken">The lifetime token for the active quiz display.</param>
        /// <returns>A task that completes after the preload is cached or safely ignored.</returns>
        private async Task PreloadImageAsync(
            QuizImage image,
            string currentImagePath,
            int generation,
            CancellationToken cancellationToken)
        {
            try
            {
                BitmapImage bitmap = await _imageService.LoadBitmapAsync(
                    image.FilePath,
                    cancellationToken).ConfigureAwait(false);

                if (!IsCurrentImagePreload(
                        generation,
                        image,
                        currentImagePath,
                        cancellationToken))
                {
                    return;
                }

                CacheImageIfCurrent(
                    image.FilePath,
                    bitmap,
                    currentImagePath,
                    image.FilePath,
                    delegate
                    {
                        return IsCurrentImagePreload(
                            generation,
                            image,
                            currentImagePath,
                            cancellationToken);
                    });
            }
            catch (OperationCanceledException)
            {
                // Window closure and question changes intentionally abandon stale preloads.
            }
            catch
            {
                // The image will fail safely if it later becomes the active question.
            }
        }

        /// <summary>
        /// Returns the next quiz image, if one remains after the active image.
        /// </summary>
        /// <returns>The image eligible for preload, or null.</returns>
        private QuizImage GetUpcomingImage()
        {
            if (_quizEngine == null ||
                _quizEngine.Session == null)
            {
                return null;
            }

            int nextIndex = _quizEngine.Session.CurrentIndex + 1;

            if (nextIndex < 0 ||
                nextIndex >= _quizEngine.Session.Images.Count)
            {
                return null;
            }

            return _quizEngine.Session.Images[nextIndex];
        }

        /// <summary>
        /// Returns the next image path for cache eviction protection.
        /// </summary>
        /// <returns>The upcoming image path, or null.</returns>
        private string GetUpcomingImagePath()
        {
            QuizImage upcomingImage = GetUpcomingImage();

            return upcomingImage == null
                ? null
                : upcomingImage.FilePath;
        }

        /// <summary>
        /// Returns a cached bitmap and promotes it to the most recently used cache position.
        /// </summary>
        /// <param name="filePath">The bitmap cache key.</param>
        /// <param name="bitmap">The cached bitmap when found.</param>
        /// <returns>True when the cache contained the requested bitmap.</returns>
        private bool TryGetCachedImage(
            string filePath,
            out BitmapImage bitmap)
        {
            lock (_imageCacheSyncRoot)
            {
                if (!_imageCache.TryGetValue(filePath, out bitmap))
                {
                    return false;
                }

                _imageCacheOrder.Remove(filePath);
                _imageCacheOrder.AddLast(filePath);

                return true;
            }
        }

        /// <summary>
        /// Returns whether an image is already present without changing cache order.
        /// </summary>
        /// <param name="filePath">The bitmap cache key.</param>
        /// <returns>True when the image is cached.</returns>
        private bool IsImageCached(string filePath)
        {
            lock (_imageCacheSyncRoot)
            {
                return _imageCache.ContainsKey(filePath);
            }
        }

        /// <summary>
        /// Caches a bitmap only while the owning operation still belongs to the active quiz state.
        /// The second validation is performed under the cache lock so cleanup cannot clear the cache
        /// and then allow a late continuation to add a stale bitmap.
        /// </summary>
        /// <param name="filePath">The bitmap cache key.</param>
        /// <param name="bitmap">The frozen bitmap to cache.</param>
        /// <param name="currentImagePath">The current image that must remain available.</param>
        /// <param name="upcomingImagePath">The upcoming image that must remain available.</param>
        /// <param name="isCurrent">Returns whether the image operation still owns the cache.</param>
        /// <returns>True when the bitmap was added to the cache.</returns>
        private bool CacheImageIfCurrent(
            string filePath,
            BitmapImage bitmap,
            string currentImagePath,
            string upcomingImagePath,
            Func<bool> isCurrent)
        {
            if (bitmap == null ||
                string.IsNullOrWhiteSpace(filePath) ||
                isCurrent == null)
            {
                return false;
            }

            lock (_imageCacheSyncRoot)
            {
                if (!isCurrent())
                {
                    return false;
                }

                CacheImageCore(
                    filePath,
                    bitmap,
                    currentImagePath,
                    upcomingImagePath);

                return true;
            }
        }

        /// <summary>
        /// Adds a bitmap to the bounded cache while the caller owns the cache lock.
        /// </summary>
        /// <param name="filePath">The bitmap cache key.</param>
        /// <param name="bitmap">The frozen bitmap to cache.</param>
        /// <param name="currentImagePath">The current image that must remain available.</param>
        /// <param name="upcomingImagePath">The upcoming image that must remain available.</param>
        private void CacheImageCore(
            string filePath,
            BitmapImage bitmap,
            string currentImagePath,
            string upcomingImagePath)
        {
            _imageCache[filePath] = bitmap;
            _imageCacheOrder.Remove(filePath);
            _imageCacheOrder.AddLast(filePath);

            while (_imageCache.Count > MaximumCachedImages)
            {
                string evictionKey = FindEvictionKey(
                    currentImagePath,
                    upcomingImagePath);

                if (evictionKey == null)
                {
                    break;
                }

                _imageCache.Remove(evictionKey);
                _imageCacheOrder.Remove(evictionKey);
            }
        }

        /// <summary>
        /// Finds the least-recently-used entry that is not the current or upcoming image.
        /// </summary>
        /// <param name="currentImagePath">The active image path.</param>
        /// <param name="upcomingImagePath">The preloaded next image path.</param>
        /// <returns>The cache key to evict, or null.</returns>
        private string FindEvictionKey(
            string currentImagePath,
            string upcomingImagePath)
        {
            LinkedListNode<string> node = _imageCacheOrder.First;

            while (node != null)
            {
                string filePath = node.Value;

                if (!string.Equals(
                        filePath,
                        currentImagePath,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        filePath,
                        upcomingImagePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return filePath;
                }

                node = node.Next;
            }

            return null;
        }

        /// <summary>
        /// Cancels the current display lifetime and invalidates all late image completions.
        /// </summary>
        private void CancelPendingImageWork()
        {
            Interlocked.Increment(ref _imageLoadGeneration);

            if (_imageLoadCancellation == null)
            {
                return;
            }

            _imageLoadCancellation.Cancel();
            _imageLoadCancellation.Dispose();
            _imageLoadCancellation = null;
        }

        /// <summary>
        /// Clears all strong bitmap references after completion, cancellation, or window closure.
        /// </summary>
        private void ReleaseImageResources()
        {
            CancelPendingImageWork();

            lock (_imageCacheSyncRoot)
            {
                _imageCache.Clear();
                _imageCacheOrder.Clear();
            }

            CurrentImage = null;
            IsImageLoading = false;
        }

        /// <summary>
        /// Determines whether a completed image task still belongs to the active question.
        /// </summary>
        /// <param name="generation">The image task generation.</param>
        /// <param name="image">The image requested by the task.</param>
        /// <param name="cancellationToken">The task cancellation token.</param>
        /// <returns>True when the image task may update the view model.</returns>
        private bool IsCurrentImageLoad(
            int generation,
            QuizImage image,
            CancellationToken cancellationToken)
        {
            return !cancellationToken.IsCancellationRequested &&
                   generation == _imageLoadGeneration &&
                   Interlocked.CompareExchange(
                       ref _isDisposed,
                       0,
                       0) == 0 &&
                   !_isFinished &&
                   _quizEngine != null &&
                   ReferenceEquals(_quizEngine.CurrentImage, image);
        }

        /// <summary>
        /// Determines whether a completed preload still belongs to the current image and its next question.
        /// </summary>
        /// <param name="generation">The image task generation.</param>
        /// <param name="image">The image requested for preload.</param>
        /// <param name="currentImagePath">The active image path captured when the preload was queued.</param>
        /// <param name="cancellationToken">The task cancellation token.</param>
        /// <returns>True when the preload may add its bitmap to the cache.</returns>
        private bool IsCurrentImagePreload(
            int generation,
            QuizImage image,
            string currentImagePath,
            CancellationToken cancellationToken)
        {
            QuizImage currentImage = _quizEngine == null
                ? null
                : _quizEngine.CurrentImage;

            return !cancellationToken.IsCancellationRequested &&
                   generation == Interlocked.CompareExchange(
                       ref _imageLoadGeneration,
                       0,
                       0) &&
                   Interlocked.CompareExchange(
                       ref _isDisposed,
                       0,
                       0) == 0 &&
                   !_isFinished &&
                   _quizEngine != null &&
                   string.Equals(
                       currentImage == null
                           ? null
                           : currentImage.FilePath,
                       currentImagePath,
                       StringComparison.OrdinalIgnoreCase) &&
                   ReferenceEquals(GetUpcomingImage(), image);
        }

        /// <summary>
        /// Observes a task even when its implementation already contains expected failure handling.
        /// </summary>
        /// <param name="task">The image task to observe.</param>
        private static void ObserveBackgroundTask(Task task)
        {
            if (task == null)
            {
                return;
            }

            task.ContinueWith(
                delegate(Task completedTask)
                {
                    AggregateException ignored = completedTask.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Returns whether the active question has a visible, ready bitmap that can accept one answer.
        /// </summary>
        /// <returns>True when answer commands may execute.</returns>
        private bool CanSubmitAnswer()
        {
            return !_isFinished &&
                   Interlocked.CompareExchange(
                       ref _isDisposed,
                       0,
                       0) == 0 &&
                   !IsImageLoading &&
                   CurrentImage != null &&
                   _quizEngine != null &&
                   _quizEngine.CanSubmitAnswer;
        }

        /// <summary>
        /// Submits the current image as GOOD.
        /// </summary>
        private void OnGood()
        {
            SubmitAnswer(QuizAnswerType.Good);
        }

        /// <summary>
        /// Submits the current image as NG.
        /// </summary>
        private void OnNg()
        {
            SubmitAnswer(QuizAnswerType.Ng);
        }

        #endregion

        #region Answer Handling

        /// <summary>
        /// Submits one answer through the existing duplicate-protected quiz engine.
        /// </summary>
        /// <param name="answer">The selected GOOD or NG answer.</param>
        private void SubmitAnswer(QuizAnswerType answer)
        {
            if (!CanSubmitAnswer())
            {
                return;
            }

            bool accepted = _quizEngine.TrySubmitAnswer(answer);

            if (!accepted)
            {
                UpdateProgress();
                RefreshCommands();

                return;
            }

            UpdateProgress();

            if (_quizEngine.IsCompleted())
            {
                FinishQuiz();

                return;
            }

            BeginDisplayCurrentImage();
        }

        /// <summary>
        /// Handles a missing, deleted, unreadable, or corrupt active image without persisting an incomplete session.
        /// </summary>
        private void HandleCurrentImageFailure()
        {
            if (_isFinished)
            {
                return;
            }

            _isFinished = true;
            ReleaseImageResources();
            ImageStatus = "The inspection image is unavailable.";
            UpdateProgress();
            RefreshCommands();

            MessageBox.Show(
                "An inspection image could not be loaded. Training was stopped and the incomplete session was not saved.",
                "Quiz Image Unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            CloseQuizWindow();
        }

        #endregion

        #region Progress

        /// <summary>
        /// Synchronizes all bindable progress values from accepted answers only.
        /// </summary>
        private void UpdateProgress()
        {
            TrainingSession session = _quizEngine == null
                ? null
                : _quizEngine.Session;
            int totalQuestions = session == null
                ? 0
                : session.TotalQuestions;
            int answeredQuestions = session == null
                ? 0
                : session.AnsweredQuestions;
            int remainingQuestions = Math.Max(
                0,
                totalQuestions - answeredQuestions);
            int currentQuestion = 0;

            if (totalQuestions > 0)
            {
                currentQuestion = _quizEngine != null &&
                                  _quizEngine.IsCompleted()
                    ? totalQuestions
                    : Math.Min(
                        Math.Max(
                            1,
                            _quizEngine == null
                                ? 1
                                : _quizEngine.CurrentQuestion),
                        totalQuestions);
            }

            CurrentQuestion = currentQuestion;
            TotalQuestions = totalQuestions;
            AnsweredQuestions = answeredQuestions;
            RemainingQuestions = remainingQuestions;
            CompletionPercentage = totalQuestions == 0
                ? 0
                : Math.Round(
                    (double)answeredQuestions /
                    totalQuestions * 100,
                    2);
            Progress = totalQuestions == 0
                ? "0 / 0"
                : currentQuestion + " / " + totalQuestions;
            QuestionProgressText = totalQuestions == 0
                ? "Question 0 of 0"
                : "Question " + currentQuestion + " of " + totalQuestions;
            AnsweredRemainingText = "Answered " + answeredQuestions +
                                    " | Remaining " + remainingQuestions;
        }

        #endregion

        #region Finish And Cleanup

        /// <summary>
        /// Handles normal completion once the engine has accepted every answer.
        /// </summary>
        private void FinishQuiz()
        {
            if (_isFinished ||
                _quizEngine == null ||
                !_quizEngine.IsCompleted())
            {
                return;
            }

            _isFinished = true;
            UpdateProgress();
            ReleaseImageResources();
            ImageStatus = "Training completed.";
            RefreshCommands();

            SaveCompletedSession();
            ShowResultWindow();
            CloseQuizWindow();
        }

        /// <summary>
        /// Shows the empty-image message without treating an empty quiz as a completed persisted session.
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
        /// Opens the result window only once for a fully completed quiz.
        /// </summary>
        private void ShowResultWindow()
        {
            if (_resultWindowShown ||
                _quizEngine == null ||
                !_quizEngine.IsCompleted())
            {
                return;
            }

            _resultWindowShown = true;

            ResultWindow resultWindow =
                new ResultWindow(
                    new List<QuizAnswer>(_quizEngine.Session.Answers));

            resultWindow.Show();
        }

        /// <summary>
        /// Saves only a fully completed, unsaved session with an answer for every question.
        /// </summary>
        private void SaveCompletedSession()
        {
            if (_quizEngine == null ||
                !_quizEngine.IsCompleted())
            {
                return;
            }

            TrainingSession session = _quizEngine.Session;

            if (session == null ||
                session.SessionID > 0 ||
                !session.IsCompleted() ||
                session.TotalQuestions == 0 ||
                session.Answers.Count != session.TotalQuestions)
            {
                return;
            }

            try
            {
                _sessionRepository.Save(session);
            }
            catch (Exception ex)
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "Quiz Session Persistence",
                    ex,
                    false);

                MessageBox.Show(
                    "Training completed, but the result could not be saved. Please contact support if the problem continues.",
                    "Save Result Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Closes the active quiz window after normal completion or an image failure.
        /// </summary>
        private static void CloseQuizWindow()
        {
            if (Application.Current == null)
            {
                return;
            }

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
        /// Cancels an incomplete quiz when its owner confirms exit.
        /// </summary>
        public void CancelQuiz()
        {
            Dispose();
        }

        /// <summary>
        /// Releases cached bitmaps and cancels all pending image work exactly once.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }

            _isFinished = true;
            ReleaseImageResources();
            RefreshCommands();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Validates and returns a supported size before other constructor arguments are evaluated.
        /// </summary>
        private static int ValidateAndReturnQuizSize(int requestedQuizSize)
        {
            ValidateRequestedQuizSize(requestedQuizSize);

            return requestedQuizSize;
        }

        /// <summary>
        /// Rejects unsupported quiz sizes before normal initialization begins.
        /// </summary>
        private static void ValidateRequestedQuizSize(int requestedQuizSize)
        {
            if (!ImageService.IsSupportedQuizSize(requestedQuizSize))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedQuizSize),
                    requestedQuizSize,
                    "Quiz size must be 10 or 20.");
            }
        }

        /// <summary>
        /// Builds the persistent safe notice for the requested and actual sample sizes.
        /// </summary>
        private string BuildQuizNotice(int actualQuizSize)
        {
            if (actualQuizSize <= 0)
                return "No inspection images are available.";

            if (actualQuizSize < _requestedQuizSize)
            {
                return "Only " +
                       actualQuizSize +
                       " unique inspection images are available. " +
                       "This training will use all available images.";
            }

            return "This training uses " +
                   actualQuizSize +
                   " unique inspection images.";
        }

        /// <summary>
        /// Refreshes answer-command enabled states after image, lifecycle, or engine changes.
        /// </summary>
        private void RefreshCommands()
        {
            _goodCommand.RaiseCanExecuteChanged();
            _ngCommand.RaiseCanExecuteChanged();
        }

        #endregion
    }
}
