#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Provides coherent daily metrics, recent sessions, and cancellable administrator charts.
    /// </summary>
    public class DashboardViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        private const int RecentSessionLimit = 12;
        private const int SevenDayTrend = 7;
        private const int ThirtyDayTrend = 30;

        private const string DashboardErrorMessage =
            "Dashboard data could not be loaded. Please try again. " +
            "Contact support if the problem continues.";

        #endregion

        #region Fields

        private readonly DashboardRepository _dashboardRepository;
        private readonly RelayCommand _refreshCommand;
        private readonly ReadOnlyCollection<int> _trendDayOptions;

        private CancellationTokenSource _operationCancellation;
        private DashboardMetrics _metrics;
        private string _statusMessage;
        private bool _isBusy;
        private int _operationVersion;
        private int _selectedTrendDays;
        private bool _isDisposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a dashboard ViewModel and starts its first production refresh.
        /// </summary>
        public DashboardViewModel()
            : this(new DashboardRepository(), true)
        {
        }

        /// <summary>
        /// Initializes a dashboard ViewModel with an explicit repository.
        /// </summary>
        /// <param name="dashboardRepository">Repository used for coherent read snapshots.</param>
        public DashboardViewModel(DashboardRepository dashboardRepository)
            : this(dashboardRepository, true)
        {
        }

        /// <summary>
        /// Initializes a dashboard ViewModel with an optional initial load for deterministic tests.
        /// </summary>
        /// <param name="dashboardRepository">Repository used for coherent read snapshots.</param>
        /// <param name="loadImmediately">Whether to start the first asynchronous refresh.</param>
        public DashboardViewModel(
            DashboardRepository dashboardRepository,
            bool loadImmediately)
        {
            if (dashboardRepository == null)
            {
                throw new ArgumentNullException(nameof(dashboardRepository));
            }

            _dashboardRepository = dashboardRepository;
            _trendDayOptions = new ReadOnlyCollection<int>(
                new[] { SevenDayTrend, ThirtyDayTrend });
            _selectedTrendDays = SevenDayTrend;
            _metrics = new DashboardMetrics();
            _statusMessage = "Loading today's dashboard...";

            RecentSessions =
                new ObservableCollection<DashboardSessionSummary>();
            Charts = new DashboardChartViewModel();

            _refreshCommand = new RelayCommand(
                BeginRefresh,
                CanRefresh);
            RefreshCommand = _refreshCommand;

            if (loadImmediately)
            {
                BeginRefresh();
            }
        }

        #endregion

        #region Collections and Charts

        /// <summary>
        /// Gets deterministic recent-session rows from the current snapshot.
        /// </summary>
        public ObservableCollection<DashboardSessionSummary> RecentSessions
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the reusable LiveCharts presentation for the current snapshot.
        /// </summary>
        public DashboardChartViewModel Charts
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the supported bounded trend day counts.
        /// </summary>
        public ReadOnlyCollection<int> TrendDayOptions
        {
            get { return _trendDayOptions; }
        }

        /// <summary>
        /// Gets or sets the selected seven-day or thirty-day trend range.
        /// </summary>
        public int SelectedTrendDays
        {
            get { return _selectedTrendDays; }
            set
            {
                ValidateTrendDays(value);

                if (_isDisposed || IsBusy)
                {
                    OnPropertyChanged(nameof(SelectedTrendDays));
                    return;
                }

                if (SetProperty(ref _selectedTrendDays, value))
                {
                    BeginRefresh();
                }
            }
        }

        #endregion

        #region Metric Properties

        /// <summary>
        /// Gets the current dashboard metric snapshot.
        /// </summary>
        public DashboardMetrics Metrics
        {
            get { return _metrics; }
            private set
            {
                if (SetProperty(
                        ref _metrics,
                        value ?? new DashboardMetrics()))
                {
                    NotifyMetricTextChanged();
                }
            }
        }

        /// <summary>
        /// Gets today's completed training count for display.
        /// </summary>
        public string TodaysTrainingText
        {
            get { return Metrics.TodaysTraining.ToString(); }
        }

        /// <summary>
        /// Gets today's reviewed-only accuracy or N/A when no answer is reviewed.
        /// </summary>
        public string AverageReviewedAccuracyText
        {
            get
            {
                return Metrics.AverageReviewedAccuracy.HasValue
                    ? Metrics.AverageReviewedAccuracy.Value.ToString("0.00") + "%"
                    : "N/A";
            }
        }

        /// <summary>
        /// Gets today's valid completed-session time in hours, minutes, and seconds.
        /// </summary>
        public string TimeSpentText
        {
            get
            {
                return AnalyticsChartViewModel.FormatDuration(
                    Metrics.TimeSpentSeconds);
            }
        }

        /// <summary>
        /// Gets today's normalized trainee GOOD selection count.
        /// </summary>
        public string GoodCountText
        {
            get { return Metrics.GoodCount.ToString(); }
        }

        /// <summary>
        /// Gets today's normalized trainee NG selection count.
        /// </summary>
        public string NgCountText
        {
            get { return Metrics.NgCount.ToString(); }
        }

        /// <summary>
        /// Gets reviewed, correct, wrong, and pending detail for today's answers.
        /// </summary>
        public string ReviewedAccuracyDetailText
        {
            get
            {
                return string.Format(
                    "Reviewed {0} | Correct {1} | Wrong {2} | Pending {3}",
                    Metrics.ReviewedAnswers,
                    Metrics.CorrectReviewedAnswers,
                    Metrics.WrongReviewedAnswers,
                    Metrics.PendingAnswers);
            }
        }

        /// <summary>
        /// Gets the local date label shared by the five headline metrics.
        /// </summary>
        public string TodayScopeText
        {
            get { return "Today | " + DateTime.Today.ToString("yyyy-MM-dd"); }
        }

        #endregion

        #region Compatibility Display Properties

        /// <summary>
        /// Gets the compatibility session-count display value.
        /// </summary>
        public string TotalSessionsText
        {
            get { return Metrics.TotalSessions.ToString(); }
        }

        /// <summary>
        /// Gets the compatibility answer-count display value.
        /// </summary>
        public string TotalAnswersText
        {
            get { return Metrics.TotalAnswers.ToString(); }
        }

        /// <summary>
        /// Gets today's pending-answer count.
        /// </summary>
        public string PendingAnswersText
        {
            get { return Metrics.PendingAnswers.ToString(); }
        }

        /// <summary>
        /// Gets today's reviewed-answer count.
        /// </summary>
        public string ReviewedAnswersText
        {
            get { return Metrics.ReviewedAnswers.ToString(); }
        }

        /// <summary>
        /// Gets today's completed-session trainee count.
        /// </summary>
        public string ActiveTraineesText
        {
            get { return Metrics.ActiveTrainees.ToString(); }
        }

        /// <summary>
        /// Gets today's reviewed-only accuracy for compatibility bindings.
        /// </summary>
        public string AverageAccuracyText
        {
            get { return AverageReviewedAccuracyText; }
        }

        /// <summary>
        /// Gets today's latest completed-session time.
        /// </summary>
        public string LatestSessionText
        {
            get
            {
                return Metrics.LatestSessionTime.HasValue
                    ? Metrics.LatestSessionTime.Value.ToString("yyyy-MM-dd HH:mm")
                    : "-";
            }
        }

        #endregion

        #region State and Commands

        /// <summary>
        /// Gets or sets the fixed, non-sensitive dashboard status message.
        /// </summary>
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        /// <summary>
        /// Gets or sets whether dashboard data is loading.
        /// </summary>
        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    _refreshCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(CanSelectTrendRange));
                }
            }
        }

        /// <summary>
        /// Gets whether the trend-range selector may be changed.
        /// </summary>
        public bool CanSelectTrendRange
        {
            get { return !_isDisposed && !IsBusy; }
        }

        /// <summary>
        /// Gets the command that refreshes the coherent snapshot once.
        /// </summary>
        public ICommand RefreshCommand
        {
            get;
            private set;
        }

        #endregion

        #region Loading

        /// <summary>
        /// Refreshes one coherent snapshot without blocking the WPF dispatcher.
        /// </summary>
        /// <returns>A task that completes when refresh or cancellation settles.</returns>
        public async Task RefreshAsync()
        {
            if (_isDisposed || IsBusy)
            {
                return;
            }

            int version = ++_operationVersion;
            CancellationTokenSource cancellation =
                new CancellationTokenSource();
            _operationCancellation = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            int trendDays = SelectedTrendDays;
            DateTime dayStart = DateTime.Today;
            DateTime dayEnd = dayStart.AddDays(1);
            DateTime trendStart = dayEnd.AddDays(-trendDays);

            IsBusy = true;
            StatusMessage = "Loading today's dashboard and " +
                            trendDays + "-day trends...";

            Task<DashboardSnapshot> worker = Task.Run(
                () => _dashboardRepository.GetSnapshot(
                    dayStart,
                    dayEnd,
                    trendStart,
                    dayEnd,
                    RecentSessionLimit),
                CancellationToken.None);

            try
            {
                DashboardSnapshot snapshot = await AwaitWithCancellation(
                    worker,
                    cancellationToken);

                if (!CanPublish(version, cancellationToken))
                {
                    return;
                }

                PublishSnapshot(snapshot);
                StatusMessage = "Dashboard refreshed at " +
                                snapshot.GeneratedAtLocal.ToString(
                                    "yyyy-MM-dd HH:mm:ss") +
                                ".";
            }
            catch (OperationCanceledException)
            {
                // Window close is an expected cancellation path.
            }
            catch (Exception exception)
            {
                if (CanPublish(version, cancellationToken))
                {
                    ApplicationErrorLogger.LogUnhandledException(
                        "Dashboard Refresh",
                        exception,
                        false);
                    ClearSnapshot();
                    StatusMessage = DashboardErrorMessage;
                }
            }
            finally
            {
                if (CanPublish(version, cancellationToken))
                {
                    IsBusy = false;
                }

                if (ReferenceEquals(_operationCancellation, cancellation))
                {
                    _operationCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        /// <summary>
        /// Starts a refresh for the command while observing all failures internally.
        /// </summary>
        private async void BeginRefresh()
        {
            await RefreshAsync();
        }

        /// <summary>
        /// Returns whether another refresh may begin.
        /// </summary>
        private bool CanRefresh()
        {
            return !_isDisposed && !IsBusy;
        }

        #endregion

        #region Snapshot Publication

        /// <summary>
        /// Replaces all visible collections and chart arrays from one snapshot.
        /// </summary>
        private void PublishSnapshot(DashboardSnapshot snapshot)
        {
            DashboardSnapshot safeSnapshot = snapshot ??
                new DashboardSnapshot();

            Metrics = safeSnapshot.Metrics;
            ReplaceRecentSessions(safeSnapshot.RecentSessions);
            Charts.UpdateSnapshot(safeSnapshot);
        }

        /// <summary>
        /// Clears stale values after a failed refresh.
        /// </summary>
        private void ClearSnapshot()
        {
            Metrics = new DashboardMetrics();
            RecentSessions.Clear();
            Charts.UpdateSnapshot(null);
        }

        /// <summary>
        /// Replaces recent-session rows so refresh cannot append duplicates.
        /// </summary>
        private void ReplaceRecentSessions(
            IList<DashboardSessionSummary> sessions)
        {
            RecentSessions.Clear();

            if (sessions == null)
            {
                return;
            }

            foreach (DashboardSessionSummary session in sessions)
            {
                if (session != null)
                {
                    RecentSessions.Add(session);
                }
            }
        }

        #endregion

        #region Cancellation

        /// <summary>
        /// Awaits synchronous database work while allowing UI cancellation to finish promptly.
        /// </summary>
        private static async Task<T> AwaitWithCancellation<T>(
            Task<T> task,
            CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> cancellationSignal =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

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
        /// Observes a worker that can outlive a canceled view operation.
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
        /// Returns whether one operation may still update live WPF state.
        /// </summary>
        private bool CanPublish(
            int version,
            CancellationToken cancellationToken)
        {
            return !_isDisposed &&
                   !cancellationToken.IsCancellationRequested &&
                   version == _operationVersion;
        }

        #endregion

        #region Validation and Notification

        /// <summary>
        /// Restricts Dashboard trends to the supported bounded ranges.
        /// </summary>
        private static void ValidateTrendDays(int value)
        {
            if (value != SevenDayTrend && value != ThirtyDayTrend)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Dashboard trends support only seven or thirty days.");
            }
        }

        /// <summary>
        /// Raises all metric-dependent display properties.
        /// </summary>
        private void NotifyMetricTextChanged()
        {
            OnPropertyChanged(nameof(TodaysTrainingText));
            OnPropertyChanged(nameof(AverageReviewedAccuracyText));
            OnPropertyChanged(nameof(TimeSpentText));
            OnPropertyChanged(nameof(GoodCountText));
            OnPropertyChanged(nameof(NgCountText));
            OnPropertyChanged(nameof(ReviewedAccuracyDetailText));
            OnPropertyChanged(nameof(TodayScopeText));
            OnPropertyChanged(nameof(TotalSessionsText));
            OnPropertyChanged(nameof(TotalAnswersText));
            OnPropertyChanged(nameof(PendingAnswersText));
            OnPropertyChanged(nameof(ReviewedAnswersText));
            OnPropertyChanged(nameof(ActiveTraineesText));
            OnPropertyChanged(nameof(AverageAccuracyText));
            OnPropertyChanged(nameof(LatestSessionText));
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Cancels active work and prevents all late publication after view closure.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Charts.Dispose();
            _operationVersion++;

            CancellationTokenSource cancellation = _operationCancellation;
            _operationCancellation = null;

            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }

            _refreshCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanSelectTrendRange));
        }

        #endregion
    }
}
