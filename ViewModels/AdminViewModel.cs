#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using VisualInspectionTrainingSystem.Views.Reports;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Coordinates asynchronous administrator review searching, filtering, preview, and transactional review commands.
    /// </summary>
    public class AdminViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        private const string FilterAll = "All";
        private const string FilterPending = "Pending";
        private const string FilterReviewed = "Reviewed";
        private const string FilterAutoReviewed = "Auto Reviewed";
        private const string FilterManuallyReviewed = "Manually Reviewed";
        private const string FilterUserGood = "User GOOD";
        private const string FilterUserNg = "User NG";
        private const string FilterCorrect = "Correct";
        private const string FilterWrong = "Wrong";
        private const string FilterReusableTruth = "Has Reusable Truth";
        private const string FilterMissingIdentity = "Missing Stable Identity";

        private const string LoadErrorMessage =
            "The review queue could not be loaded. Please try again or contact support if the problem continues.";

        private const string SaveErrorMessage =
            "The review could not be saved. No partial review changes were kept.";

        #endregion

        #region Fields

        private readonly AnswerRepository _answerRepository;
        private readonly ImageService _imageService;
        private readonly List<QuizAnswer> _allAnswers;
        private readonly Dictionary<int, QuizImage> _legacyPreviewByAnswerId;
        private readonly CancellationTokenSource _lifetimeCancellation;

        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _searchCommand;
        private readonly RelayCommand _clearSearchCommand;
        private readonly RelayCommand _showAllCommand;
        private readonly RelayCommand _showPendingCommand;
        private readonly RelayCommand _showReviewedCommand;
        private readonly RelayCommand _selectVisibleCommand;
        private readonly RelayCommand _clearSelectionCommand;
        private readonly RelayCommand _bulkGoodCommand;
        private readonly RelayCommand _bulkNgCommand;
        private readonly RelayCommand _openDashboardCommand;
        private readonly RelayCommand _openReportsCommand;
        private readonly RelayCommand _logoutCommand;
        private readonly RelayCommand _markGoodCommand;
        private readonly RelayCommand _markNgCommand;

        private CancellationTokenSource _previewCancellation;
        private QuizAnswer _selectedAnswer;
        private BitmapImage _selectedImage;
        private string _selectedImageCaption;
        private string _selectedImageStatus;
        private string _selectedFilter;
        private string _searchText;
        private string _appliedSearchText;
        private string _statusMessage;
        private string _imageCatalogWarning;
        private bool _isBusy;
        private int _loadGeneration;
        private int _previewGeneration;
        private int _isDisposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates the administrator review workflow and begins its first non-blocking load.
        /// </summary>
        public AdminViewModel()
        {
            _answerRepository = new AnswerRepository();
            _imageService = new ImageService();
            _allAnswers = new List<QuizAnswer>();
            _legacyPreviewByAnswerId = new Dictionary<int, QuizImage>();
            _lifetimeCancellation = new CancellationTokenSource();

            Answers = new ObservableCollection<QuizAnswer>();
            FilterOptions = new ObservableCollection<string>(
                new[]
                {
                    FilterAll,
                    FilterPending,
                    FilterReviewed,
                    FilterAutoReviewed,
                    FilterManuallyReviewed,
                    FilterUserGood,
                    FilterUserNg,
                    FilterCorrect,
                    FilterWrong,
                    FilterReusableTruth,
                    FilterMissingIdentity
                });

            _selectedFilter = FilterPending;
            _appliedSearchText = string.Empty;

            _refreshCommand = new RelayCommand(BeginLoadAnswers, CanRunCommand);
            _searchCommand = new RelayCommand(ApplySearch, CanRunCommand);
            _clearSearchCommand = new RelayCommand(ClearSearch, CanRunCommand);
            _showAllCommand = new RelayCommand(ShowAll, CanRunCommand);
            _showPendingCommand = new RelayCommand(ShowPending, CanRunCommand);
            _showReviewedCommand = new RelayCommand(ShowReviewed, CanRunCommand);
            _selectVisibleCommand = new RelayCommand(SelectVisible, CanSelectVisible);
            _clearSelectionCommand = new RelayCommand(ClearSelection, CanClearSelection);
            _bulkGoodCommand = new RelayCommand(BeginBulkGood, CanBulkReview);
            _bulkNgCommand = new RelayCommand(BeginBulkNg, CanBulkReview);
            _openDashboardCommand = new RelayCommand(OpenDashboard, CanRunCommand);
            _openReportsCommand = new RelayCommand(OpenReports, CanRunCommand);
            _logoutCommand = new RelayCommand(Logout, CanRunCommand);
            _markGoodCommand = new RelayCommand(BeginMarkSelectedGood, CanReviewSelectedAnswer);
            _markNgCommand = new RelayCommand(BeginMarkSelectedNg, CanReviewSelectedAnswer);

            RefreshCommand = _refreshCommand;
            SearchCommand = _searchCommand;
            ClearSearchCommand = _clearSearchCommand;
            ShowAllCommand = _showAllCommand;
            ShowPendingCommand = _showPendingCommand;
            ShowReviewedCommand = _showReviewedCommand;
            SelectVisibleCommand = _selectVisibleCommand;
            ClearSelectionCommand = _clearSelectionCommand;
            BulkGoodCommand = _bulkGoodCommand;
            BulkNgCommand = _bulkNgCommand;
            OpenDashboardCommand = _openDashboardCommand;
            OpenReportsCommand = _openReportsCommand;
            LogoutCommand = _logoutCommand;
            MarkGoodCommand = _markGoodCommand;
            MarkNgCommand = _markNgCommand;

            SelectedImageCaption = "No answer selected";
            SelectedImageStatus = "Select an answer to preview its inspection image.";
            StatusMessage = "Loading the review queue...";

            BeginLoadAnswers();
        }

        #endregion

        #region Collections

        /// <summary>
        /// Gets the visible, filtered review queue.
        /// </summary>
        public ObservableCollection<QuizAnswer> Answers { get; private set; }

        /// <summary>
        /// Gets deterministic administrator filter choices.
        /// </summary>
        public ObservableCollection<string> FilterOptions { get; private set; }

        #endregion

        #region Search and Filter Properties

        /// <summary>
        /// Gets or sets search input without querying or filtering on each keystroke.
        /// </summary>
        public string SearchText
        {
            get { return _searchText; }
            set { SetProperty(ref _searchText, value); }
        }

        /// <summary>
        /// Gets or sets the active deterministic review filter.
        /// </summary>
        public string SelectedFilter
        {
            get { return _selectedFilter; }
            set
            {
                string normalized = FilterOptions.Contains(value)
                    ? value
                    : FilterAll;

                if (SetProperty(ref _selectedFilter, normalized))
                {
                    ApplyFilter(null);
                    StatusMessage = BuildLoadedStatus();
                    OnPropertyChanged(nameof(ActiveFilter));
                }
            }
        }

        /// <summary>
        /// Gets the compatibility name for the active filter.
        /// </summary>
        public string ActiveFilter
        {
            get { return SelectedFilter; }
        }

        /// <summary>
        /// Gets a concise visible filter/search summary.
        /// </summary>
        public string FilterSummary
        {
            get
            {
                string search = string.IsNullOrWhiteSpace(_appliedSearchText)
                    ? string.Empty
                    : " matching ‘" + _appliedSearchText + "’";

                return SelectedFilter + search + " - " + VisibleAnswers + " shown";
            }
        }

        #endregion

        #region Selected Answer Properties

        /// <summary>
        /// Gets or sets the row shown in the review details panel.
        /// </summary>
        public QuizAnswer SelectedAnswer
        {
            get { return _selectedAnswer; }
            set
            {
                if (SetProperty(ref _selectedAnswer, value))
                {
                    BeginRefreshSelectedImage();
                    NotifySelectedAnswerChanged();
                    RefreshCommands();
                }
            }
        }

        /// <summary>
        /// Gets or sets the detached selected preview image.
        /// </summary>
        public BitmapImage SelectedImage
        {
            get { return _selectedImage; }
            set { SetProperty(ref _selectedImage, value); }
        }

        /// <summary>
        /// Gets or sets the selected preview caption.
        /// </summary>
        public string SelectedImageCaption
        {
            get { return _selectedImageCaption; }
            set { SetProperty(ref _selectedImageCaption, value); }
        }

        /// <summary>
        /// Gets or sets a non-sensitive preview status.
        /// </summary>
        public string SelectedImageStatus
        {
            get { return _selectedImageStatus; }
            set { SetProperty(ref _selectedImageStatus, value); }
        }

        public string SelectedUserAnswerText
        {
            get { return SelectedAnswer == null ? "-" : SelectedAnswer.UserAnswerText; }
        }

        public string SelectedCorrectAnswerText
        {
            get { return SelectedAnswer == null ? "-" : SelectedAnswer.CorrectAnswerText; }
        }

        public string SelectedIsCorrectText
        {
            get { return SelectedAnswer == null ? "-" : SelectedAnswer.ResultText; }
        }

        public string SelectedAnswerTimeText
        {
            get
            {
                return SelectedAnswer == null
                    ? "-"
                    : SelectedAnswer.AnswerTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        public string SelectedReviewStatusText
        {
            get { return SelectedAnswer == null ? "-" : SelectedAnswer.ReviewStatusText; }
        }

        #endregion

        #region State and Summary

        /// <summary>
        /// Gets or sets the current safe status message.
        /// </summary>
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        /// <summary>
        /// Gets or sets whether one load or review operation is active.
        /// </summary>
        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (SetProperty(ref _isBusy, value))
                    RefreshCommands();
            }
        }

        public int TotalAnswers { get { return _allAnswers.Count; } }

        public int ReviewedAnswers
        {
            get { return _allAnswers.Count(answer => answer != null && answer.IsReviewed); }
        }

        public int PendingAnswers { get { return TotalAnswers - ReviewedAnswers; } }

        public int VisibleAnswers { get { return Answers.Count; } }

        public int SelectedCount
        {
            get { return _allAnswers.Count(answer => answer != null && answer.IsSelected); }
        }

        public int SelectedUniqueImageCount
        {
            get
            {
                return _allAnswers
                    .Where(answer => answer != null && answer.IsSelected && answer.HasStableIdentity)
                    .Select(answer => answer.ImageHash)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
            }
        }

        public string SelectionSummary
        {
            get
            {
                return SelectedCount + " selected, " +
                       SelectedUniqueImageCount + " unique stable image(s)";
            }
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; private set; }
        public ICommand SearchCommand { get; private set; }
        public ICommand ClearSearchCommand { get; private set; }
        public ICommand ShowAllCommand { get; private set; }
        public ICommand ShowPendingCommand { get; private set; }
        public ICommand ShowReviewedCommand { get; private set; }
        public ICommand SelectVisibleCommand { get; private set; }
        public ICommand ClearSelectionCommand { get; private set; }
        public ICommand BulkGoodCommand { get; private set; }
        public ICommand BulkNgCommand { get; private set; }
        public ICommand OpenDashboardCommand { get; private set; }
        public ICommand OpenReportsCommand { get; private set; }
        public ICommand LogoutCommand { get; private set; }
        public ICommand MarkGoodCommand { get; private set; }
        public ICommand MarkNgCommand { get; private set; }

        #endregion

        #region Loading

        /// <summary>
        /// Starts one observed non-blocking queue load.
        /// </summary>
        private async void BeginLoadAnswers()
        {
            await LoadAnswersAsync(null);
        }

        /// <summary>
        /// Loads database rows and stable local catalog identities without blocking the dispatcher.
        /// </summary>
        private async Task LoadAnswersAsync(int? preferredAnswerId)
        {
            if (IsBusy || IsDisposed)
                return;

            int generation = Interlocked.Increment(ref _loadGeneration);
            CancellationToken token = _lifetimeCancellation.Token;
            Task<List<QuizImage>> catalogTask = null;
            bool catalogObserved = false;

            try
            {
                IsBusy = true;
                StatusMessage = "Loading the review queue...";
                _imageCatalogWarning = string.Empty;

                Task<List<QuizAnswer>> answerTask = Task.Run(
                    () => _answerRepository.GetForReview(),
                    token);
                catalogTask = _imageService.LoadImagesWithHashesAsync(
                    AppConstants.QuizImageFolder,
                    false,
                    token);

                List<QuizAnswer> answers = await AwaitOrCancelAsync(answerTask, token);
                List<QuizImage> images;

                try
                {
                    catalogObserved = true;
                    images = await AwaitOrCancelAsync(catalogTask, token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    ApplicationErrorLogger.LogUnhandledException(
                        "Admin Image Catalog",
                        ex,
                        false);
                    images = new List<QuizImage>();
                    _imageCatalogWarning = "Image previews are currently unavailable.";
                }

                if (!CanApplyAsyncResult(generation, token))
                    return;

                ReplaceAnswers(
                    answers,
                    images,
                    preferredAnswerId);
                StatusMessage = BuildLoadedStatus();
            }
            catch (OperationCanceledException)
            {
                if (!catalogObserved && catalogTask != null)
                    ObserveAbandonedTask(catalogTask);

                // Window closing and lifecycle cancellation are normal outcomes.
            }
            catch (Exception ex)
            {
                if (!catalogObserved && catalogTask != null)
                    ObserveAbandonedTask(catalogTask);

                if (!CanApplyAsyncResult(generation, token))
                    return;

                ApplicationErrorLogger.LogUnhandledException(
                    "Admin Review Load",
                    ex,
                    false);
                ClearLoadedAnswers();
                StatusMessage = LoadErrorMessage;
                MessageBox.Show(
                    LoadErrorMessage,
                    "Review Queue",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (generation == _loadGeneration && !IsDisposed)
                    IsBusy = false;
            }
        }

        /// <summary>
        /// Replaces loaded rows and maps local previews only by stable hash, except labeled legacy candidates.
        /// </summary>
        private void ReplaceAnswers(
            IList<QuizAnswer> answers,
            IList<QuizImage> images,
            int? preferredAnswerId)
        {
            ClearLoadedAnswers();

            Dictionary<string, QuizImage> imagesByHash = images
                .Where(image => image != null && image.HasStableIdentity)
                .GroupBy(image => image.ImageHash, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);
            Dictionary<int, QuizImage> imagesByTransientId = images
                .Where(image => image != null)
                .GroupBy(image => image.ImageID)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (QuizAnswer answer in answers ?? new List<QuizAnswer>())
            {
                if (answer == null)
                    continue;

                QuizImage image;

                if (answer.HasStableIdentity &&
                    imagesByHash.TryGetValue(answer.ImageHash, out image))
                {
                    answer.FilePath = image.FilePath;

                    if (string.IsNullOrWhiteSpace(answer.FileName))
                        answer.FileName = image.FileName;
                }
                else if (!answer.HasStableIdentity &&
                         imagesByTransientId.TryGetValue(answer.ImageID, out image))
                {
                    answer.FilePath = image.FilePath;

                    if (string.IsNullOrWhiteSpace(answer.FileName))
                        answer.FileName = image.FileName;

                    _legacyPreviewByAnswerId[answer.AnswerID] = image;
                }

                answer.PropertyChanged += OnAnswerPropertyChanged;
                _allAnswers.Add(answer);
            }

            ApplyFilter(preferredAnswerId);
            NotifySummaryChanged();
        }

        /// <summary>
        /// Clears all loaded rows and event handlers.
        /// </summary>
        private void ClearLoadedAnswers()
        {
            foreach (QuizAnswer answer in _allAnswers)
            {
                if (answer != null)
                    answer.PropertyChanged -= OnAnswerPropertyChanged;
            }

            _allAnswers.Clear();
            _legacyPreviewByAnswerId.Clear();
            Answers.Clear();
            SelectedAnswer = null;
            NotifySummaryChanged();
        }

        #endregion

        #region Search and Filtering

        private void ApplySearch()
        {
            _appliedSearchText = (SearchText ?? string.Empty).Trim();
            ApplyFilter(null);
            StatusMessage = BuildLoadedStatus();
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
            _appliedSearchText = string.Empty;
            ApplyFilter(null);
            StatusMessage = BuildLoadedStatus();
        }

        private void ShowAll() { SelectedFilter = FilterAll; }

        private void ShowPending() { SelectedFilter = FilterPending; }

        private void ShowReviewed() { SelectedFilter = FilterReviewed; }

        /// <summary>
        /// Replaces the visible collection so refresh/filter never duplicates rows.
        /// </summary>
        private void ApplyFilter(int? preferredAnswerId)
        {
            Answers.Clear();

            foreach (QuizAnswer answer in _allAnswers)
            {
                if (ShouldShowAnswer(answer) && MatchesSearch(answer))
                    Answers.Add(answer);
            }

            SelectedAnswer = FindPreferredAnswer(preferredAnswerId);
            NotifySummaryChanged();
        }

        private bool ShouldShowAnswer(QuizAnswer answer)
        {
            if (answer == null)
                return false;

            switch (SelectedFilter)
            {
                case FilterPending:
                    return !answer.IsReviewed;
                case FilterReviewed:
                    return answer.IsReviewed;
                case FilterAutoReviewed:
                    return answer.IsAutoReviewed;
                case FilterManuallyReviewed:
                    return answer.IsManuallyReviewed;
                case FilterUserGood:
                    return answer.UserAnswer == QuizAnswerType.Good;
                case FilterUserNg:
                    return answer.UserAnswer == QuizAnswerType.Ng;
                case FilterCorrect:
                    return answer.IsReviewed && answer.IsCorrect;
                case FilterWrong:
                    return answer.IsReviewed && !answer.IsCorrect;
                case FilterReusableTruth:
                    return answer.HasReusableTruth;
                case FilterMissingIdentity:
                    return !answer.HasStableIdentity;
                default:
                    return true;
            }
        }

        private bool MatchesSearch(QuizAnswer answer)
        {
            if (string.IsNullOrWhiteSpace(_appliedSearchText))
                return true;

            string search = _appliedSearchText;

            return Contains(answer.EmployeeNo, search) ||
                   Contains(answer.AnswerID.ToString(), search) ||
                   Contains(answer.SessionID.ToString(), search) ||
                   Contains(answer.FileName, search) ||
                   Contains(answer.ImageHash, search) ||
                   Contains(answer.ShortImageHash, search) ||
                   Contains(answer.UserAnswerText, search) ||
                   Contains(answer.CorrectAnswerText, search) ||
                   Contains(answer.ReviewStatusText, search);
        }

        private QuizAnswer FindPreferredAnswer(int? preferredAnswerId)
        {
            if (preferredAnswerId.HasValue)
            {
                QuizAnswer preferred = Answers.FirstOrDefault(
                    answer => answer.AnswerID == preferredAnswerId.Value);

                if (preferred != null)
                    return preferred;
            }

            return Answers.Count > 0 ? Answers[0] : null;
        }

        #endregion

        #region Selection and Bulk Review

        private void SelectVisible()
        {
            foreach (QuizAnswer answer in Answers)
                answer.IsSelected = true;
        }

        private void ClearSelection()
        {
            foreach (QuizAnswer answer in _allAnswers)
                answer.IsSelected = false;
        }

        private async void BeginBulkGood()
        {
            await BulkReviewAsync(QuizAnswerType.Good);
        }

        private async void BeginBulkNg()
        {
            await BulkReviewAsync(QuizAnswerType.Ng);
        }

        /// <summary>
        /// Confirms and executes one atomic bulk decision grouped by stable image identity.
        /// </summary>
        private async Task BulkReviewAsync(QuizAnswerType correctAnswer)
        {
            List<QuizAnswer> selected = _allAnswers
                .Where(answer => answer.IsSelected)
                .ToList();

            if (selected.Count == 0 || IsBusy || IsDisposed)
                return;

            int uniqueImages = selected
                .Where(answer => answer.HasStableIdentity)
                .Select(answer => answer.ImageHash)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            int missingIdentities = selected.Count(answer => !answer.HasStableIdentity);
            int estimatedAnswers = CountMatchingAnswers(selected);
            int estimatedSessions = CountMatchingSessions(selected);
            int corrections = CountCorrections(selected, correctAnswer);

            string message =
                "Apply administrator truth " + FormatAnswer(correctAnswer) + "?\n\n" +
                "Selected rows: " + selected.Count + "\n" +
                "Unique stable images: " + uniqueImages + "\n" +
                "Matching answers: " + estimatedAnswers + "\n" +
                "Affected sessions: " + estimatedSessions + "\n" +
                "Truth corrections: " + corrections + "\n" +
                "Rows without stable identity (not processed): " + missingIdentities;

            if (MessageBox.Show(
                    message,
                    "Confirm Bulk Review",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            int generation = _loadGeneration;
            CancellationToken token = _lifetimeCancellation.Token;

            try
            {
                IsBusy = true;
                StatusMessage = "Applying the bulk administrator review...";

                string reviewer = GetReviewerEmployeeNo();
                Task<AnswerRepository.ReviewOperationResult> operationTask = Task.Run(
                    () => _answerRepository.ReviewAnswers(selected, correctAnswer, reviewer),
                    token);
                AnswerRepository.ReviewOperationResult result =
                    await AwaitOrCancelAsync(operationTask, token);

                if (IsDisposed || token.IsCancellationRequested)
                    return;

                LogCorrectionIfNeeded(result, correctAnswer);
                StatusMessage = BuildOperationStatus(result, correctAnswer);
                IsBusy = false;
                await LoadAnswersAsync(null);
            }
            catch (OperationCanceledException)
            {
                // Closing the window abandons only UI waiting; the database transaction remains atomic.
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                    return;

                ApplicationErrorLogger.LogUnhandledException(
                    "Admin Bulk Review",
                    ex,
                    false);
                StatusMessage = SaveErrorMessage;
                MessageBox.Show(
                    IsStaleReview(ex) ? ex.Message : SaveErrorMessage,
                    "Bulk Review",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (!IsDisposed && generation <= _loadGeneration)
                    IsBusy = false;
            }
        }

        #endregion

        #region Individual Review

        private async void BeginMarkSelectedGood()
        {
            await ReviewSelectedAnswerAsync(QuizAnswerType.Good);
        }

        private async void BeginMarkSelectedNg()
        {
            await ReviewSelectedAnswerAsync(QuizAnswerType.Ng);
        }

        /// <summary>
        /// Confirms legacy identity or truth correction and executes one atomic review.
        /// </summary>
        private async Task ReviewSelectedAnswerAsync(QuizAnswerType correctAnswer)
        {
            QuizAnswer selected = SelectedAnswer;

            if (selected == null || IsBusy || IsDisposed)
                return;

            string confirmedHash = null;
            string confirmedFileName = null;
            QuizImage legacyPreview;

            if (!selected.HasStableIdentity &&
                _legacyPreviewByAnswerId.TryGetValue(selected.AnswerID, out legacyPreview) &&
                legacyPreview != null &&
                legacyPreview.HasStableIdentity)
            {
                MessageBoxResult legacyConfirmation = MessageBox.Show(
                    "This historical answer has no stable image identity. Confirm that the displayed preview is the exact image originally answered. If confirmed, its SHA-256 identity will be attached to this row and reusable administrator truth may be propagated.",
                    "Confirm Legacy Image Identity",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (legacyConfirmation == MessageBoxResult.Cancel)
                    return;

                if (legacyConfirmation == MessageBoxResult.Yes)
                {
                    confirmedHash = legacyPreview.ImageHash;
                    confirmedFileName = legacyPreview.FileName;
                }
            }

            string effectiveHash = selected.HasStableIdentity
                ? selected.ImageHash
                : confirmedHash;

            if (!string.IsNullOrWhiteSpace(effectiveHash))
            {
                int answerCount = _allAnswers.Count(
                    answer => HashEquals(answer.ImageHash, effectiveHash));
                int sessionCount = _allAnswers
                    .Where(answer => HashEquals(answer.ImageHash, effectiveHash))
                    .Select(answer => answer.SessionID)
                    .Distinct()
                    .Count();
                QuizAnswerType? currentTruth = FindLoadedTruth(effectiveHash);

                if (currentTruth.HasValue && currentTruth.Value != correctAnswer)
                {
                    if (MessageBox.Show(
                            "Change reusable administrator truth from " +
                            FormatAnswer(currentTruth.Value) + " to " +
                            FormatAnswer(correctAnswer) + "?\n\n" +
                            "Answer rows affected: " + answerCount + "\n" +
                            "Sessions recalculated: " + sessionCount +
                            "\n\nHistorical statistics will change.",
                            "Confirm Truth Correction",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }
            else if (MessageBox.Show(
                         "Stable image identity is unavailable. This answer can be reviewed individually, but the decision will not propagate to other rows. Continue?",
                         "Legacy Review",
                         MessageBoxButton.YesNo,
                         MessageBoxImage.Information) != MessageBoxResult.Yes)
            {
                return;
            }

            int answerId = selected.AnswerID;
            CancellationToken token = _lifetimeCancellation.Token;

            try
            {
                IsBusy = true;
                StatusMessage = "Saving the administrator review...";

                string reviewer = GetReviewerEmployeeNo();
                Task<AnswerRepository.ReviewOperationResult> operationTask = Task.Run(
                    () => _answerRepository.ReviewAnswer(
                        answerId,
                        correctAnswer,
                        reviewer,
                        selected.CorrectAnswer,
                        confirmedHash,
                        confirmedFileName),
                    token);
                AnswerRepository.ReviewOperationResult result =
                    await AwaitOrCancelAsync(operationTask, token);

                if (IsDisposed || token.IsCancellationRequested)
                    return;

                LogCorrectionIfNeeded(result, correctAnswer);
                StatusMessage = BuildOperationStatus(result, correctAnswer);
                IsBusy = false;
                await LoadAnswersAsync(answerId);
            }
            catch (OperationCanceledException)
            {
                // Lifecycle cancellation is not an application failure.
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                    return;

                ApplicationErrorLogger.LogUnhandledException(
                    "Admin Individual Review",
                    ex,
                    false);
                StatusMessage = SaveErrorMessage;
                MessageBox.Show(
                    IsStaleReview(ex) ? ex.Message : SaveErrorMessage,
                    "Save Review",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (!IsDisposed)
                    IsBusy = false;
            }
        }

        #endregion

        #region Image Preview

        /// <summary>
        /// Starts a detached preview and prevents late results from updating a changed selection.
        /// </summary>
        private async void BeginRefreshSelectedImage()
        {
            CancelPreview();
            SelectedImage = null;

            QuizAnswer answer = SelectedAnswer;
            int generation = Interlocked.Increment(ref _previewGeneration);

            if (answer == null)
            {
                SelectedImageCaption = "No answer selected";
                SelectedImageStatus = "Select an answer to preview its inspection image.";
                return;
            }

            SelectedImageCaption = string.IsNullOrWhiteSpace(answer.FileName)
                ? "Image " + answer.ImageID
                : answer.FileName;

            if (string.IsNullOrWhiteSpace(answer.FilePath))
            {
                SelectedImageStatus = answer.HasStableIdentity
                    ? "No local file matches this stable image identity."
                    : "Stable image identity and a confirmed local preview are unavailable.";
                return;
            }

            _previewCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            CancellationToken token = _previewCancellation.Token;
            SelectedImageStatus = "Loading preview...";

            try
            {
                BitmapImage bitmap = await _imageService.LoadBitmapAsync(
                    answer.FilePath,
                    token);

                if (token.IsCancellationRequested ||
                    generation != _previewGeneration ||
                    IsDisposed ||
                    SelectedAnswer != answer)
                {
                    return;
                }

                SelectedImage = bitmap;
                SelectedImageStatus = answer.HasStableIdentity
                    ? answer.ReviewStatusText
                    : "Legacy preview candidate; confirm the exact image before attaching identity.";
            }
            catch (OperationCanceledException)
            {
                // Selection change or window closing is expected.
            }
            catch (Exception ex)
            {
                if (generation != _previewGeneration || IsDisposed)
                    return;

                ApplicationErrorLogger.LogUnhandledException(
                    "Admin Image Preview",
                    ex,
                    false);
                SelectedImage = null;
                SelectedImageStatus = "The selected preview could not be opened.";
            }
        }

        private void CancelPreview()
        {
            CancellationTokenSource cancellation = _previewCancellation;
            _previewCancellation = null;

            if (cancellation == null)
                return;

            cancellation.Cancel();
            cancellation.Dispose();
        }

        #endregion

        #region Navigation

        private void OpenDashboard()
        {
            new DashboardWindow().Show();
        }

        private void OpenReports()
        {
            new ReportsWindow().Show();
        }

        private void Logout()
        {
            SessionService.Logout();
            new LoginWindow().Show();
            CloseWindow<DashboardWindow>();
            CloseWindow<ReportsWindow>();
            CloseWindow<AdminWindow>();
        }

        #endregion

        #region Command State

        private bool CanRunCommand() { return !IsBusy && !IsDisposed; }

        private bool CanReviewSelectedAnswer()
        {
            return CanRunCommand() && SelectedAnswer != null;
        }

        private bool CanSelectVisible()
        {
            return CanRunCommand() && Answers.Count > 0;
        }

        private bool CanClearSelection()
        {
            return CanRunCommand() && SelectedCount > 0;
        }

        private bool CanBulkReview()
        {
            return CanRunCommand() && SelectedCount > 0;
        }

        private void RefreshCommands()
        {
            _refreshCommand.RaiseCanExecuteChanged();
            _searchCommand.RaiseCanExecuteChanged();
            _clearSearchCommand.RaiseCanExecuteChanged();
            _showAllCommand.RaiseCanExecuteChanged();
            _showPendingCommand.RaiseCanExecuteChanged();
            _showReviewedCommand.RaiseCanExecuteChanged();
            _selectVisibleCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
            _bulkGoodCommand.RaiseCanExecuteChanged();
            _bulkNgCommand.RaiseCanExecuteChanged();
            _openDashboardCommand.RaiseCanExecuteChanged();
            _openReportsCommand.RaiseCanExecuteChanged();
            _logoutCommand.RaiseCanExecuteChanged();
            _markGoodCommand.RaiseCanExecuteChanged();
            _markNgCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Cancels UI waiting and preview work; abandoned database tasks remain observed and atomic.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            Interlocked.Increment(ref _loadGeneration);
            Interlocked.Increment(ref _previewGeneration);
            _lifetimeCancellation.Cancel();
            CancelPreview();
            ClearLoadedAnswers();
            _lifetimeCancellation.Dispose();
        }

        private bool IsDisposed
        {
            get { return Interlocked.CompareExchange(ref _isDisposed, 0, 0) != 0; }
        }

        #endregion

        #region Async Helpers

        /// <summary>
        /// Allows window cancellation to return promptly while observing abandoned work.
        /// </summary>
        private static async Task<T> AwaitOrCancelAsync<T>(
            Task<T> workTask,
            CancellationToken cancellationToken)
        {
            Task cancellationTask = Task.Delay(
                Timeout.Infinite,
                cancellationToken);
            Task completedTask = await Task.WhenAny(workTask, cancellationTask);

            if (completedTask != workTask)
            {
                ObserveAbandonedTask(workTask);
                throw new OperationCanceledException(cancellationToken);
            }

            return await workTask;
        }

        /// <summary>
        /// Observes any later exception from work no longer awaited by a closed screen.
        /// </summary>
        private static async void ObserveAbandonedTask(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // The initiating screen no longer exists; observation prevents an unobserved exception.
            }
        }

        private bool CanApplyAsyncResult(
            int generation,
            CancellationToken token)
        {
            return !IsDisposed &&
                   !token.IsCancellationRequested &&
                   generation == _loadGeneration;
        }

        #endregion

        #region Notification Helpers

        private void OnAnswerPropertyChanged(
            object sender,
            PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs == null || eventArgs.PropertyName != nameof(QuizAnswer.IsSelected))
                return;

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedUniqueImageCount));
            OnPropertyChanged(nameof(SelectionSummary));
            RefreshCommands();
        }

        private void NotifySummaryChanged()
        {
            OnPropertyChanged(nameof(TotalAnswers));
            OnPropertyChanged(nameof(ReviewedAnswers));
            OnPropertyChanged(nameof(PendingAnswers));
            OnPropertyChanged(nameof(VisibleAnswers));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedUniqueImageCount));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(FilterSummary));
            RefreshCommands();
        }

        private void NotifySelectedAnswerChanged()
        {
            OnPropertyChanged(nameof(SelectedUserAnswerText));
            OnPropertyChanged(nameof(SelectedCorrectAnswerText));
            OnPropertyChanged(nameof(SelectedIsCorrectText));
            OnPropertyChanged(nameof(SelectedAnswerTimeText));
            OnPropertyChanged(nameof(SelectedReviewStatusText));
        }

        #endregion

        #region General Helpers

        private string BuildLoadedStatus()
        {
            string status = "Loaded " + TotalAnswers + " answer(s). " + FilterSummary + ".";

            if (!string.IsNullOrWhiteSpace(_imageCatalogWarning))
                status += " " + _imageCatalogWarning;

            return status;
        }

        private static string BuildOperationStatus(
            AnswerRepository.ReviewOperationResult result,
            QuizAnswerType correctAnswer)
        {
            return "Applied " + FormatAnswer(correctAnswer) + " to " +
                   result.UniqueImageCount + " unique image(s); updated " +
                   result.UpdatedAnswerCount + " answer(s) across " +
                   result.AffectedSessionCount + " session(s). " +
                   result.MissingIdentityCount + " selected row(s) lacked stable identity.";
        }

        private static void LogCorrectionIfNeeded(
            AnswerRepository.ReviewOperationResult result,
            QuizAnswerType correctAnswer)
        {
            if (!result.WasCorrection)
                return;

            ApplicationErrorLogger.LogUnhandledException(
                "Administrator Image Truth Correction",
                new InvalidOperationException(
                    "Reusable administrator truth was corrected to " +
                    FormatAnswer(correctAnswer) + ". " +
                    result.UpdatedAnswerCount + " answer rows and " +
                    result.AffectedSessionCount + " sessions were recalculated."),
                false);
        }

        private int CountMatchingAnswers(IList<QuizAnswer> selected)
        {
            HashSet<string> hashes = new HashSet<string>(
                selected.Where(answer => answer.HasStableIdentity)
                    .Select(answer => answer.ImageHash),
                StringComparer.OrdinalIgnoreCase);

            return _allAnswers.Count(
                answer => answer.HasStableIdentity && hashes.Contains(answer.ImageHash));
        }

        private int CountMatchingSessions(IList<QuizAnswer> selected)
        {
            HashSet<string> hashes = new HashSet<string>(
                selected.Where(answer => answer.HasStableIdentity)
                    .Select(answer => answer.ImageHash),
                StringComparer.OrdinalIgnoreCase);

            return _allAnswers
                .Where(answer => answer.HasStableIdentity && hashes.Contains(answer.ImageHash))
                .Select(answer => answer.SessionID)
                .Distinct()
                .Count();
        }

        private int CountCorrections(
            IList<QuizAnswer> selected,
            QuizAnswerType correctAnswer)
        {
            return selected
                .Where(answer => answer.HasStableIdentity &&
                                 answer.CorrectAnswer.HasValue &&
                                 answer.CorrectAnswer.Value != correctAnswer)
                .Select(answer => answer.ImageHash)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private QuizAnswerType? FindLoadedTruth(string imageHash)
        {
            QuizAnswer answer = _allAnswers.FirstOrDefault(
                candidate => HashEquals(candidate.ImageHash, imageHash) &&
                             candidate.CorrectAnswer.HasValue);

            return answer == null ? (QuizAnswerType?)null : answer.CorrectAnswer;
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HashEquals(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) &&
                   !string.IsNullOrWhiteSpace(second) &&
                   string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatAnswer(QuizAnswerType answer)
        {
            return answer.ToString().ToUpperInvariant();
        }

        private static string GetReviewerEmployeeNo()
        {
            return SessionService.CurrentUser == null
                ? null
                : SessionService.CurrentUser.EmployeeNo;
        }

        private static bool IsStaleReview(Exception exception)
        {
            return exception is InvalidOperationException &&
                   exception.Message.IndexOf(
                       "Refresh",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CloseWindow<T>() where T : Window
        {
            if (Application.Current == null)
                return;

            for (int index = Application.Current.Windows.Count - 1; index >= 0; index--)
            {
                Window window = Application.Current.Windows[index];

                if (window is T)
                    window.Close();
            }
        }

        #endregion
    }
}
