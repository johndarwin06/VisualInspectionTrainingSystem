#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Provides daily analytics and recent-session state for the administrator dashboard.
    /// </summary>
    public class DashboardViewModel : BaseViewModel
    {
        #region Constants

        private const int RecentSessionLimit = 12;

        private const string DashboardErrorMessage =
            "Dashboard data could not be loaded. Please try again. " +
            "Contact support if the problem continues.";

        #endregion

        #region Fields

        private readonly DashboardRepository _dashboardRepository;

        private readonly RelayCommand _refreshCommand;

        private DashboardMetrics _metrics;

        private string _statusMessage;

        private bool _isBusy;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a dashboard ViewModel with the default repository.
        /// </summary>
        public DashboardViewModel()
            : this(new DashboardRepository())
        {
        }

        /// <summary>
        /// Initializes a dashboard ViewModel with an explicit repository.
        /// </summary>
        /// <param name="dashboardRepository">Repository used to load dashboard data.</param>
        public DashboardViewModel(DashboardRepository dashboardRepository)
        {
            if (dashboardRepository == null)
                throw new ArgumentNullException(nameof(dashboardRepository));

            _dashboardRepository = dashboardRepository;

            RecentSessions = new ObservableCollection<DashboardSessionSummary>();

            _metrics = new DashboardMetrics();

            _statusMessage = "Loading today's dashboard...";

            _refreshCommand = new RelayCommand(
                BeginRefresh,
                CanRefresh);

            RefreshCommand = _refreshCommand;

            BeginRefresh();
        }

        #endregion

        #region Collections

        /// <summary>
        /// Gets the current deterministic recent-session rows.
        /// </summary>
        public ObservableCollection<DashboardSessionSummary> RecentSessions
        {
            get;
        }

        #endregion

        #region Metric Properties

        /// <summary>
        /// Gets the current dashboard metric snapshot.
        /// </summary>
        public DashboardMetrics Metrics
        {
            get
            {
                return _metrics;
            }
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
            get
            {
                return Metrics.TodaysTraining.ToString();
            }
        }

        /// <summary>
        /// Gets today's reviewed-only accuracy or N/A when no reviewed answers exist.
        /// </summary>
        public string AverageReviewedAccuracyText
        {
            get
            {
                if (!Metrics.AverageReviewedAccuracy.HasValue)
                    return "N/A";

                return Metrics.AverageReviewedAccuracy.Value.ToString("0.00") + "%";
            }
        }

        /// <summary>
        /// Gets today's valid completed-session time in hours, minutes, and seconds.
        /// </summary>
        public string TimeSpentText
        {
            get
            {
                long totalSeconds = Metrics.TimeSpentSeconds;

                if (totalSeconds < 0)
                    totalSeconds = 0;

                long hours = totalSeconds / 3600;
                long minutes = (totalSeconds % 3600) / 60;
                long seconds = totalSeconds % 60;

                return string.Format(
                    "{0}h {1:00}m {2:00}s",
                    hours,
                    minutes,
                    seconds);
            }
        }

        /// <summary>
        /// Gets today's trainee GOOD selection count.
        /// </summary>
        public string GoodCountText
        {
            get
            {
                return Metrics.GoodCount.ToString();
            }
        }

        /// <summary>
        /// Gets today's trainee NG selection count.
        /// </summary>
        public string NgCountText
        {
            get
            {
                return Metrics.NgCount.ToString();
            }
        }

        /// <summary>
        /// Gets reviewed, correct, wrong, and pending detail for today's answers.
        /// </summary>
        public string ReviewedAccuracyDetailText
        {
            get
            {
                return string.Format(
                    "Reviewed {0} · Correct {1} · Wrong {2} · Pending {3}",
                    Metrics.ReviewedAnswers,
                    Metrics.CorrectReviewedAnswers,
                    Metrics.WrongReviewedAnswers,
                    Metrics.PendingAnswers);
            }
        }

        /// <summary>
        /// Gets the local date label used by all five metrics.
        /// </summary>
        public string TodayScopeText
        {
            get
            {
                return "Today · " + DateTime.Today.ToString("yyyy-MM-dd");
            }
        }

        #endregion

        #region Compatibility Display Properties

        /// <summary>
        /// Gets the existing session text binding using today's completed count.
        /// </summary>
        public string TotalSessionsText
        {
            get
            {
                return Metrics.TotalSessions.ToString();
            }
        }

        /// <summary>
        /// Gets the existing answer text binding using today's GOOD and NG answers.
        /// </summary>
        public string TotalAnswersText
        {
            get
            {
                return Metrics.TotalAnswers.ToString();
            }
        }

        /// <summary>
        /// Gets today's pending-answer count for existing consumers.
        /// </summary>
        public string PendingAnswersText
        {
            get
            {
                return Metrics.PendingAnswers.ToString();
            }
        }

        /// <summary>
        /// Gets today's reviewed-answer count for existing consumers.
        /// </summary>
        public string ReviewedAnswersText
        {
            get
            {
                return Metrics.ReviewedAnswers.ToString();
            }
        }

        /// <summary>
        /// Gets today's completed-session trainee count for existing consumers.
        /// </summary>
        public string ActiveTraineesText
        {
            get
            {
                return Metrics.ActiveTrainees.ToString();
            }
        }

        /// <summary>
        /// Gets today's reviewed-only accuracy for the existing accuracy binding.
        /// </summary>
        public string AverageAccuracyText
        {
            get
            {
                return AverageReviewedAccuracyText;
            }
        }

        /// <summary>
        /// Gets today's latest completed-session time for existing consumers.
        /// </summary>
        public string LatestSessionText
        {
            get
            {
                if (!Metrics.LatestSessionTime.HasValue)
                    return "-";

                return Metrics.LatestSessionTime.Value.ToString("yyyy-MM-dd HH:mm");
            }
        }

        #endregion

        #region State Properties

        /// <summary>
        /// Gets or sets the non-sensitive dashboard status message.
        /// </summary>
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

        /// <summary>
        /// Gets or sets whether dashboard data is currently loading.
        /// </summary>
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
                    _refreshCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets the command that refreshes metrics and recent sessions once.
        /// </summary>
        public ICommand RefreshCommand
        {
            get;
        }

        #endregion

        #region Loading

        /// <summary>
        /// Refreshes metrics and recent sessions without blocking the WPF dispatcher.
        /// </summary>
        /// <returns>A task that completes when the refresh settles.</returns>
        public async Task RefreshAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            StatusMessage = "Loading today's dashboard...";

            try
            {
                DashboardLoadResult result = await Task.Run(
                    () => new DashboardLoadResult(
                        _dashboardRepository.GetMetrics(),
                        _dashboardRepository.GetRecentSessions(
                            RecentSessionLimit)));

                Metrics = result.Metrics;

                ReplaceRecentSessions(result.RecentSessions);

                StatusMessage =
                    "Today's dashboard refreshed at " +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    ".";
            }
            catch (Exception ex)
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "Dashboard Refresh",
                    ex,
                    false);

                Metrics = new DashboardMetrics();
                RecentSessions.Clear();
                StatusMessage = DashboardErrorMessage;
            }
            finally
            {
                IsBusy = false;
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
            return !IsBusy;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Replaces recent-session rows so refreshes cannot append duplicates.
        /// </summary>
        /// <param name="sessions">The latest deterministic repository rows.</param>
        private void ReplaceRecentSessions(
            IList<DashboardSessionSummary> sessions)
        {
            RecentSessions.Clear();

            if (sessions == null)
                return;

            foreach (DashboardSessionSummary session in sessions)
            {
                if (session != null)
                {
                    RecentSessions.Add(session);
                }
            }
        }

        /// <summary>
        /// Raises property changes for all metric display values.
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

        #region Nested Types

        /// <summary>
        /// Carries one atomically loaded dashboard refresh result.
        /// </summary>
        private sealed class DashboardLoadResult
        {
            /// <summary>
            /// Initializes one dashboard load result.
            /// </summary>
            /// <param name="metrics">Daily metric snapshot.</param>
            /// <param name="recentSessions">Recent deterministic session rows.</param>
            public DashboardLoadResult(
                DashboardMetrics metrics,
                List<DashboardSessionSummary> recentSessions)
            {
                Metrics = metrics ?? new DashboardMetrics();

                RecentSessions = recentSessions ??
                    new List<DashboardSessionSummary>();
            }

            /// <summary>
            /// Gets the daily metric snapshot.
            /// </summary>
            public DashboardMetrics Metrics
            {
                get;
            }

            /// <summary>
            /// Gets the recent deterministic session rows.
            /// </summary>
            public List<DashboardSessionSummary> RecentSessions
            {
                get;
            }
        }

        #endregion
    }
}
