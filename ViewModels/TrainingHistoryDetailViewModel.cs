#region Namespaces

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Provides one authorized, read-only trainee result with lazy image preview loading.
    /// </summary>
    public sealed class TrainingHistoryDetailViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        private const string LoadErrorMessage =
            "This training result could not be loaded. Please try again. " +
            "Contact support if the problem continues.";

        private const string MissingResultMessage =
            "This completed training result is not available for the current account.";

        #endregion

        #region Fields

        private readonly int _sessionId;
        private readonly ITrainingHistoryService _historyService;
        private readonly ImageService _imageService;
        private readonly RelayCommand _refreshCommand;

        private TrainingHistorySessionSummary _summary;
        private TrainingHistoryAnswerDetail _selectedAnswer;
        private ImageSource _selectedImagePreview;
        private string _statusMessage;
        private string _previewStatus;
        private bool _isLoading;
        private bool _isPreviewLoading;
        private bool _isDisposed;
        private int _loadVersion;
        private long _previewVersion;
        private CancellationTokenSource _loadCancellation;
        private CancellationTokenSource _previewCancellation;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes one current-user session result with production services.
        /// </summary>
        /// <param name="sessionId">Authorized session identity to request.</param>
        public TrainingHistoryDetailViewModel(int sessionId)
            : this(
                sessionId,
                new TrainingHistoryService(),
                new ImageService(),
                true)
        {
        }

        /// <summary>
        /// Initializes one result with explicit services for verification.
        /// </summary>
        public TrainingHistoryDetailViewModel(
            int sessionId,
            ITrainingHistoryService historyService,
            ImageService imageService,
            bool loadImmediately)
        {
            if (sessionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sessionId));

            if (historyService == null)
                throw new ArgumentNullException(nameof(historyService));

            if (imageService == null)
                throw new ArgumentNullException(nameof(imageService));

            _sessionId = sessionId;
            _historyService = historyService;
            _imageService = imageService;
            _statusMessage = "Loading your training result...";
            _previewStatus = "Select an answer to preview its image.";

            Answers = new ObservableCollection<TrainingHistoryAnswerDetail>();
            _refreshCommand = new RelayCommand(BeginLoad, CanRefresh);
            RefreshCommand = _refreshCommand;

            if (loadImmediately)
                BeginLoad();
        }

        #endregion

        #region Collections

        /// <summary>
        /// Gets read-only answers in deterministic question order.
        /// </summary>
        public ObservableCollection<TrainingHistoryAnswerDetail> Answers { get; private set; }

        #endregion

        #region Result Properties

        /// <summary>
        /// Gets the authorized completed-session summary.
        /// </summary>
        public TrainingHistorySessionSummary Summary
        {
            get { return _summary; }
            private set
            {
                if (SetProperty(ref _summary, value))
                {
                    OnPropertyChanged(nameof(SessionTitle));
                    OnPropertyChanged(nameof(HasResult));
                }
            }
        }

        /// <summary>
        /// Gets the safe session title.
        /// </summary>
        public string SessionTitle
        {
            get
            {
                return Summary == null
                    ? "Training Result"
                    : "Training Result #" + Summary.SessionID;
            }
        }

        /// <summary>
        /// Gets whether an authorized result was loaded.
        /// </summary>
        public bool HasResult
        {
            get { return Summary != null; }
        }

        /// <summary>
        /// Gets or sets the answer selected for lazy preview.
        /// </summary>
        public TrainingHistoryAnswerDetail SelectedAnswer
        {
            get { return _selectedAnswer; }
            set
            {
                if (SetProperty(ref _selectedAnswer, value))
                    BeginPreviewLoad(value);
            }
        }

        /// <summary>
        /// Gets the detached selected-answer preview.
        /// </summary>
        public ImageSource SelectedImagePreview
        {
            get { return _selectedImagePreview; }
            private set
            {
                if (SetProperty(ref _selectedImagePreview, value))
                    OnPropertyChanged(nameof(HasImagePreview));
            }
        }

        /// <summary>
        /// Gets whether an image preview is available.
        /// </summary>
        public bool HasImagePreview
        {
            get { return SelectedImagePreview != null; }
        }

        #endregion

        #region State Properties

        /// <summary>
        /// Gets a fixed, non-sensitive detail status.
        /// </summary>
        public string StatusMessage
        {
            get { return _statusMessage; }
            private set { SetProperty(ref _statusMessage, value); }
        }

        /// <summary>
        /// Gets a fixed, non-sensitive preview status.
        /// </summary>
        public string PreviewStatus
        {
            get { return _previewStatus; }
            private set { SetProperty(ref _previewStatus, value); }
        }

        /// <summary>
        /// Gets whether session data is loading.
        /// </summary>
        public bool IsLoading
        {
            get { return _isLoading; }
            private set
            {
                if (SetProperty(ref _isLoading, value))
                    _refreshCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Gets whether the selected image is loading.
        /// </summary>
        public bool IsPreviewLoading
        {
            get { return _isPreviewLoading; }
            private set { SetProperty(ref _isPreviewLoading, value); }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the command that reloads this authorized session result.
        /// </summary>
        public ICommand RefreshCommand { get; private set; }

        #endregion

        #region Detail Loading

        /// <summary>
        /// Begins one observed current-user detail load.
        /// </summary>
        private async void BeginLoad()
        {
            if (_isDisposed || IsLoading)
                return;

            int version = ++_loadVersion;
            CancellationTokenSource cancellation = ReplaceCancellation(
                ref _loadCancellation);

            IsLoading = true;
            StatusMessage = "Loading your training result...";

            Task<TrainingHistorySessionDetail> worker = Task.Run(
                () => _historyService.GetSessionDetail(_sessionId),
                CancellationToken.None);

            try
            {
                TrainingHistorySessionDetail detail = await AwaitWithCancellation(
                    worker,
                    cancellation.Token);

                if (!CanPublishLoad(version, cancellation.Token))
                    return;

                Answers.Clear();
                Summary = detail == null ? null : detail.Summary;

                if (detail == null)
                {
                    StatusMessage = MissingResultMessage;
                    SelectedAnswer = null;
                    return;
                }

                foreach (TrainingHistoryAnswerDetail answer in detail.Answers)
                    Answers.Add(answer);

                StatusMessage = "Training result loaded with " +
                                Answers.Count + " answers.";
                SelectedAnswer = Answers.Count > 0 ? Answers[0] : null;
            }
            catch (OperationCanceledException)
            {
                // Window close and replacement operations are expected cancellation paths.
            }
            catch (Exception ex)
            {
                if (CanPublishLoad(version, cancellation.Token))
                {
                    ApplicationErrorLogger.LogUnhandledException(
                        "Training History Detail Load",
                        ex,
                        false);
                    Summary = null;
                    Answers.Clear();
                    SelectedAnswer = null;
                    StatusMessage = LoadErrorMessage;
                }
            }
            finally
            {
                if (version == _loadVersion && !_isDisposed)
                    IsLoading = false;

                ReleaseCancellation(ref _loadCancellation, cancellation);
            }
        }

        #endregion

        #region Preview Loading

        /// <summary>
        /// Cancels the previous preview and lazily loads the selected answer image.
        /// </summary>
        private void BeginPreviewLoad(TrainingHistoryAnswerDetail answer)
        {
            long version = Interlocked.Increment(ref _previewVersion);
            CancelAndClear(ref _previewCancellation);

            SelectedImagePreview = null;
            IsPreviewLoading = false;

            if (_isDisposed)
                return;

            if (answer == null)
            {
                PreviewStatus = "Select an answer to preview its image.";
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            _previewCancellation = cancellation;
            IsPreviewLoading = true;
            PreviewStatus = "Loading image preview...";

            Task previewTask = LoadPreviewAsync(
                answer,
                version,
                cancellation);
            ObserveAbandonedTask(previewTask);
        }

        /// <summary>
        /// Resolves and decodes one safe in-folder image without blocking the dispatcher.
        /// </summary>
        private async Task LoadPreviewAsync(
            TrainingHistoryAnswerDetail answer,
            long version,
            CancellationTokenSource cancellation)
        {
            try
            {
                Task<string> pathWorker = Task.Run(
                    () => ResolvePreviewPath(answer.ImageFileName),
                    CancellationToken.None);
                string path = await AwaitWithCancellation(
                    pathWorker,
                    cancellation.Token);

                if (string.IsNullOrWhiteSpace(path))
                {
                    if (CanPublishPreview(answer, version, cancellation.Token))
                        PreviewStatus = "Image preview is unavailable.";

                    return;
                }

                Task<BitmapImage> imageWorker = _imageService.LoadBitmapAsync(
                    path,
                    cancellation.Token);
                BitmapImage bitmap = await AwaitWithCancellation(
                    imageWorker,
                    cancellation.Token);

                if (!CanPublishPreview(answer, version, cancellation.Token))
                    return;

                SelectedImagePreview = bitmap;
                PreviewStatus = "Image preview ready.";
            }
            catch (OperationCanceledException)
            {
                // Selection changes and window close are expected cancellation paths.
            }
            catch (Exception ex)
            {
                if (CanPublishPreview(answer, version, cancellation.Token))
                {
                    ApplicationErrorLogger.LogUnhandledException(
                        "Training History Preview",
                        ex,
                        false);
                    SelectedImagePreview = null;
                    PreviewStatus = "Image preview is unavailable.";
                }
            }
            finally
            {
                if (version == Interlocked.Read(ref _previewVersion) && !_isDisposed)
                    IsPreviewLoading = false;

                ReleaseCancellation(ref _previewCancellation, cancellation);
            }
        }

        /// <summary>
        /// Resolves a persisted filename only inside the configured quiz image folder.
        /// </summary>
        private static string ResolvePreviewPath(string imageFileName)
        {
            if (string.IsNullOrWhiteSpace(imageFileName))
                return null;

            string safeFileName = Path.GetFileName(imageFileName.Trim());

            if (string.IsNullOrWhiteSpace(safeFileName))
                return null;

            string root = Path.GetFullPath(AppConstants.QuizImageFolder);
            string candidate = Path.GetFullPath(Path.Combine(root, safeFileName));
            string rootPrefix = root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            return File.Exists(candidate) ? candidate : null;
        }

        /// <summary>
        /// Returns whether the preview still belongs to a live selected row.
        /// </summary>
        private bool CanPublishPreview(
            TrainingHistoryAnswerDetail answer,
            long version,
            CancellationToken cancellationToken)
        {
            return !_isDisposed &&
                   !cancellationToken.IsCancellationRequested &&
                   version == Interlocked.Read(ref _previewVersion) &&
                   ReferenceEquals(answer, SelectedAnswer);
        }

        #endregion

        #region Cancellation Helpers

        /// <summary>
        /// Replaces one active operation token source.
        /// </summary>
        private static CancellationTokenSource ReplaceCancellation(
            ref CancellationTokenSource target)
        {
            CancellationTokenSource previous = target;
            CancellationTokenSource next = new CancellationTokenSource();
            target = next;

            if (previous != null)
            {
                previous.Cancel();
                previous.Dispose();
            }

            return next;
        }

        /// <summary>
        /// Completes promptly on cancellation while observing abandoned worker failures.
        /// </summary>
        private static async Task<T> AwaitWithCancellation<T>(
            Task<T> task,
            CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> cancellationSignal =
                new TaskCompletionSource<bool>();

            using (cancellationToken.Register(
                () => cancellationSignal.TrySetResult(true)))
            {
                Task completed = await Task.WhenAny(task, cancellationSignal.Task);

                if (!ReferenceEquals(completed, task))
                {
                    ObserveAbandonedTask(task);
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task;
        }

        /// <summary>
        /// Observes a task that can outlive a canceled view operation.
        /// </summary>
        private static void ObserveAbandonedTask(Task task)
        {
            task.ContinueWith(
                abandoned =>
                {
                    Exception ignored = abandoned.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Releases a token source only when it remains the active instance.
        /// </summary>
        private static void ReleaseCancellation(
            ref CancellationTokenSource target,
            CancellationTokenSource completed)
        {
            if (ReferenceEquals(target, completed))
                target = null;

            completed.Dispose();
        }

        /// <summary>
        /// Cancels and clears one active token source.
        /// </summary>
        private static void CancelAndClear(ref CancellationTokenSource target)
        {
            CancellationTokenSource cancellation = target;
            target = null;

            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }
        }

        /// <summary>
        /// Returns whether one detail operation may publish to live WPF state.
        /// </summary>
        private bool CanPublishLoad(
            int version,
            CancellationToken cancellationToken)
        {
            return !_isDisposed &&
                   !cancellationToken.IsCancellationRequested &&
                   version == _loadVersion;
        }

        #endregion

        #region Command State

        /// <summary>
        /// Returns whether the result can be refreshed.
        /// </summary>
        private bool CanRefresh()
        {
            return !_isDisposed && !IsLoading;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Cancels database and image work and prevents late WPF updates.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _loadVersion++;
            Interlocked.Increment(ref _previewVersion);
            CancelAndClear(ref _loadCancellation);
            CancelAndClear(ref _previewCancellation);
        }

        #endregion
    }
}
