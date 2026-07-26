#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Provides bounded, current-user-only training history with asynchronous filtering and paging.
    /// </summary>
    public sealed class TrainingHistoryViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        private const int PageSize = 50;

        private const string LoadErrorMessage =
            "Training history could not be loaded. Please try again. " +
            "Contact support if the problem continues.";

        private const string InvalidDateRangeMessage =
            "Select a valid date range. The start date must not be later than the end date.";

        #endregion

        #region Fields

        private readonly ITrainingHistoryService _historyService;
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _clearFiltersCommand;
        private readonly RelayCommand _loadMoreCommand;
        private readonly RelayCommand _openResultCommand;
        private readonly ReadOnlyCollection<string> _reviewStatusOptions;

        private string _searchText;
        private DateTime? _startDate;
        private DateTime? _endDate;
        private string _selectedReviewStatus;
        private string _statusMessage;
        private bool _isLoading;
        private bool _hasMore;
        private bool _isDisposed;
        private int _operationVersion;
        private CancellationTokenSource _operationCancellation;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the view should open an authorized read-only session result.
        /// </summary>
        public event Action<TrainingHistorySessionSummary> OpenSessionRequested;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes trainee history with the production current-user service.
        /// </summary>
        public TrainingHistoryViewModel()
            : this(new TrainingHistoryService(), true)
        {
        }

        /// <summary>
        /// Initializes trainee history with an explicit service.
        /// </summary>
        /// <param name="historyService">Current-user-only history service.</param>
        /// <param name="loadImmediately">Whether to begin the first page load.</param>
        public TrainingHistoryViewModel(
            ITrainingHistoryService historyService,
            bool loadImmediately)
        {
            if (historyService == null)
                throw new ArgumentNullException(nameof(historyService));

            _historyService = historyService;
            _reviewStatusOptions = new ReadOnlyCollection<string>(
                new[]
                {
                    "All",
                    "Pending Review",
                    "Partially Reviewed",
                    "Reviewed"
                });
            _selectedReviewStatus = "All";
            _statusMessage = "Loading your training history...";

            Sessions = new ObservableCollection<TrainingHistorySessionSummary>();

            _refreshCommand = new RelayCommand(BeginRefresh, CanRefresh);
            _clearFiltersCommand = new RelayCommand(ClearFilters, CanRefresh);
            _loadMoreCommand = new RelayCommand(BeginLoadMore, CanLoadMore);
            _openResultCommand = new RelayCommand(OpenResult, CanOpenResult);

            RefreshCommand = _refreshCommand;
            ClearFiltersCommand = _clearFiltersCommand;
            LoadMoreCommand = _loadMoreCommand;
            OpenResultCommand = _openResultCommand;

            if (loadImmediately)
                BeginRefresh();
        }

        #endregion

        #region Collections

        /// <summary>
        /// Gets newest-first completed sessions for the active trainee.
        /// </summary>
        public ObservableCollection<TrainingHistorySessionSummary> Sessions { get; private set; }

        /// <summary>
        /// Gets supported review-status labels.
        /// </summary>
        public ReadOnlyCollection<string> ReviewStatusOptions
        {
            get { return _reviewStatusOptions; }
        }

        #endregion

        #region Filter Properties

        /// <summary>
        /// Gets or sets optional session or image filename search text.
        /// </summary>
        public string SearchText
        {
            get { return _searchText; }
            set { SetProperty(ref _searchText, value); }
        }

        /// <summary>
        /// Gets or sets the inclusive local completion date.
        /// </summary>
        public DateTime? StartDate
        {
            get { return _startDate; }
            set { SetProperty(ref _startDate, value); }
        }

        /// <summary>
        /// Gets or sets the inclusive local completion end date.
        /// </summary>
        public DateTime? EndDate
        {
            get { return _endDate; }
            set { SetProperty(ref _endDate, value); }
        }

        /// <summary>
        /// Gets or sets the selected review-status label.
        /// </summary>
        public string SelectedReviewStatus
        {
            get { return _selectedReviewStatus; }
            set { SetProperty(ref _selectedReviewStatus, value ?? "All"); }
        }

        #endregion

        #region State Properties

        /// <summary>
        /// Gets a fixed, non-sensitive operation message.
        /// </summary>
        public string StatusMessage
        {
            get { return _statusMessage; }
            private set { SetProperty(ref _statusMessage, value); }
        }

        /// <summary>
        /// Gets whether a database page is being loaded.
        /// </summary>
        public bool IsLoading
        {
            get { return _isLoading; }
            private set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(EmptyStateText));
                    RefreshCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets whether another bounded page is available.
        /// </summary>
        public bool HasMore
        {
            get { return _hasMore; }
            private set
            {
                if (SetProperty(ref _hasMore, value))
                    RefreshCommandStates();
            }
        }

        /// <summary>
        /// Gets whether the current filter has no completed sessions.
        /// </summary>
        public bool IsEmpty
        {
            get { return !IsLoading && Sessions.Count == 0; }
        }

        /// <summary>
        /// Gets the interactive empty-state message.
        /// </summary>
        public string EmptyStateText
        {
            get
            {
                return IsEmpty
                    ? "No completed training sessions match these filters."
                    : string.Empty;
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the command that reloads the first filtered page.
        /// </summary>
        public ICommand RefreshCommand { get; private set; }

        /// <summary>
        /// Gets the command that clears all history filters.
        /// </summary>
        public ICommand ClearFiltersCommand { get; private set; }

        /// <summary>
        /// Gets the command that appends the next bounded page.
        /// </summary>
        public ICommand LoadMoreCommand { get; private set; }

        /// <summary>
        /// Gets the command that requests one authorized session detail window.
        /// </summary>
        public ICommand OpenResultCommand { get; private set; }

        #endregion

        #region Command Methods

        /// <summary>
        /// Starts a replacement first-page load.
        /// </summary>
        private void BeginRefresh()
        {
            BeginLoad(false);
        }

        /// <summary>
        /// Starts a bounded subsequent-page load.
        /// </summary>
        private void BeginLoadMore()
        {
            BeginLoad(true);
        }

        /// <summary>
        /// Clears filters and immediately reloads current-user history.
        /// </summary>
        private void ClearFilters()
        {
            SearchText = string.Empty;
            StartDate = null;
            EndDate = null;
            SelectedReviewStatus = "All";
            BeginRefresh();
        }

        /// <summary>
        /// Requests read-only detail for a selected session row.
        /// </summary>
        /// <param name="parameter">Selected session summary.</param>
        private void OpenResult(object parameter)
        {
            TrainingHistorySessionSummary session =
                parameter as TrainingHistorySessionSummary;

            if (session == null || _isDisposed || IsLoading)
                return;

            OpenSessionRequested?.Invoke(session);
        }

        #endregion

        #region Loading

        /// <summary>
        /// Starts one observed asynchronous database read.
        /// </summary>
        /// <param name="append">Whether to append rather than replace rows.</param>
        private async void BeginLoad(bool append)
        {
            if (_isDisposed || IsLoading)
                return;

            TrainingHistoryQuery query;

            try
            {
                query = BuildQuery(append ? Sessions.Count : 0);
            }
            catch (ArgumentException)
            {
                StatusMessage = InvalidDateRangeMessage;
                return;
            }

            int operationVersion = ++_operationVersion;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            CancellationTokenSource previous = _operationCancellation;
            _operationCancellation = cancellation;

            if (previous != null)
            {
                previous.Cancel();
                previous.Dispose();
            }

            IsLoading = true;
            StatusMessage = append
                ? "Loading more completed sessions..."
                : "Loading your training history...";

            Task<TrainingHistoryPage> worker = Task.Run(
                () => _historyService.GetHistoryPage(query),
                CancellationToken.None);

            try
            {
                TrainingHistoryPage page = await AwaitWithCancellation(
                    worker,
                    cancellation.Token);

                if (!CanPublish(operationVersion, cancellation.Token))
                    return;

                if (!append)
                    Sessions.Clear();

                HashSet<int> existingIds = new HashSet<int>(
                    Sessions.Select(session => session.SessionID));

                foreach (TrainingHistorySessionSummary session in page.Sessions)
                {
                    if (existingIds.Add(session.SessionID))
                        Sessions.Add(session);
                }

                HasMore = page.HasMore;
                StatusMessage = Sessions.Count == 0
                    ? "No completed training sessions matched these filters."
                    : "Showing " + Sessions.Count +
                      (HasMore ? " completed sessions. More are available." : " completed sessions.");
                NotifyCollectionStateChanged();
            }
            catch (OperationCanceledException)
            {
                // Window close and replacement operations are expected cancellation paths.
            }
            catch (Exception ex)
            {
                if (CanPublish(operationVersion, cancellation.Token))
                {
                    ApplicationErrorLogger.LogUnhandledException(
                        "Training History Load",
                        ex,
                        false);
                    HasMore = false;
                    StatusMessage = LoadErrorMessage;
                }
            }
            finally
            {
                if (operationVersion == _operationVersion && !_isDisposed)
                {
                    IsLoading = false;
                    NotifyCollectionStateChanged();
                }

                if (ReferenceEquals(_operationCancellation, cancellation))
                    _operationCancellation = null;

                cancellation.Dispose();
            }
        }

        /// <summary>
        /// Creates an identity-free bounded query from visible filter state.
        /// </summary>
        /// <param name="offset">Zero-based paging offset.</param>
        /// <returns>Validated history query.</returns>
        private TrainingHistoryQuery BuildQuery(int offset)
        {
            DateTime? startInclusive = StartDate.HasValue
                ? StartDate.Value.Date
                : (DateTime?)null;
            DateTime? endExclusive = EndDate.HasValue
                ? EndDate.Value.Date.AddDays(1)
                : (DateTime?)null;

            if (startInclusive.HasValue &&
                endExclusive.HasValue &&
                startInclusive.Value >= endExclusive.Value)
            {
                throw new ArgumentException(InvalidDateRangeMessage);
            }

            return new TrainingHistoryQuery
            {
                SearchText = SearchText,
                StartInclusive = startInclusive,
                EndExclusive = endExclusive,
                ReviewFilter = ParseReviewFilter(SelectedReviewStatus),
                Offset = offset,
                Limit = PageSize
            };
        }

        /// <summary>
        /// Maps the fixed UI label to its repository filter.
        /// </summary>
        private static TrainingHistoryReviewFilter ParseReviewFilter(string value)
        {
            if (string.Equals(value, "Pending Review", StringComparison.Ordinal))
                return TrainingHistoryReviewFilter.PendingReview;

            if (string.Equals(value, "Partially Reviewed", StringComparison.Ordinal))
                return TrainingHistoryReviewFilter.PartiallyReviewed;

            if (string.Equals(value, "Reviewed", StringComparison.Ordinal))
                return TrainingHistoryReviewFilter.Reviewed;

            return TrainingHistoryReviewFilter.All;
        }

        #endregion

        #region Cancellation Helpers

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
                Task completed = await Task.WhenAny(
                    task,
                    cancellationSignal.Task);

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
        /// Returns whether one operation may still publish to live WPF state.
        /// </summary>
        private bool CanPublish(
            int operationVersion,
            CancellationToken cancellationToken)
        {
            return !_isDisposed &&
                   !cancellationToken.IsCancellationRequested &&
                   operationVersion == _operationVersion;
        }

        #endregion

        #region Command State

        /// <summary>
        /// Returns whether a first-page operation may begin.
        /// </summary>
        private bool CanRefresh()
        {
            return !_isDisposed && !IsLoading;
        }

        /// <summary>
        /// Returns whether a subsequent bounded page may load.
        /// </summary>
        private bool CanLoadMore()
        {
            return CanRefresh() && HasMore;
        }

        /// <summary>
        /// Returns whether an authorized session detail may open.
        /// </summary>
        private bool CanOpenResult(object parameter)
        {
            return CanRefresh() && parameter is TrainingHistorySessionSummary;
        }

        /// <summary>
        /// Refreshes all command enabled states.
        /// </summary>
        private void RefreshCommandStates()
        {
            _refreshCommand.RaiseCanExecuteChanged();
            _clearFiltersCommand.RaiseCanExecuteChanged();
            _loadMoreCommand.RaiseCanExecuteChanged();
            _openResultCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Raises collection-dependent properties.
        /// </summary>
        private void NotifyCollectionStateChanged()
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyStateText));
            RefreshCommandStates();
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Cancels active work and prevents late results from updating a closed window.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _operationVersion++;

            CancellationTokenSource cancellation = _operationCancellation;
            _operationCancellation = null;

            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }

            OpenSessionRequested = null;
        }

        #endregion
    }
}
