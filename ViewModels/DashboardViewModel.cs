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
    /// ViewModel for the admin dashboard.
    /// </summary>
    public class DashboardViewModel : BaseViewModel
    {
        #region Fields

        private readonly DashboardRepository _dashboardRepository;

        private readonly RelayCommand _refreshCommand;

        private DashboardMetrics _metrics;

        private string _statusMessage;

        private bool _isBusy;

        #endregion

        #region Constructor

        public DashboardViewModel()
        {
            _dashboardRepository = new DashboardRepository();

            RecentSessions = new ObservableCollection<DashboardSessionSummary>();

            _metrics = new DashboardMetrics();

            _refreshCommand = new RelayCommand(
                LoadDashboard,
                CanRefresh);

            RefreshCommand = _refreshCommand;

            LoadDashboard();
        }

        #endregion

        #region Properties

        public ObservableCollection<DashboardSessionSummary> RecentSessions
        {
            get;
        }

        public DashboardMetrics Metrics
        {
            get
            {
                return _metrics;
            }
            private set
            {
                if (SetProperty(ref _metrics, value))
                {
                    NotifyMetricTextChanged();
                }
            }
        }

        public string TotalSessionsText
        {
            get
            {
                return Metrics.TotalSessions.ToString();
            }
        }

        public string TotalAnswersText
        {
            get
            {
                return Metrics.TotalAnswers.ToString();
            }
        }

        public string PendingAnswersText
        {
            get
            {
                return Metrics.PendingAnswers.ToString();
            }
        }

        public string ReviewedAnswersText
        {
            get
            {
                return Metrics.ReviewedAnswers.ToString();
            }
        }

        public string ActiveTraineesText
        {
            get
            {
                return Metrics.ActiveTrainees.ToString();
            }
        }

        public string AverageAccuracyText
        {
            get
            {
                return Metrics.AverageAccuracy.ToString("0.00") + "%";
            }
        }

        public string LatestSessionText
        {
            get
            {
                if (!Metrics.LatestSessionTime.HasValue)
                    return "-";

                return Metrics.LatestSessionTime.Value.ToString("yyyy-MM-dd HH:mm");
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
                    _refreshCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand RefreshCommand
        {
            get;
        }

        #endregion

        #region Loading

        private bool CanRefresh()
        {
            return !IsBusy;
        }

        private void LoadDashboard()
        {
            try
            {
                IsBusy = true;

                Metrics = _dashboardRepository.GetMetrics();

                RecentSessions.Clear();

                foreach (DashboardSessionSummary session in
                    _dashboardRepository.GetRecentSessions(12))
                {
                    RecentSessions.Add(session);
                }

                StatusMessage =
                    $"Dashboard refreshed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}.";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;

                MessageBox.Show(
                    ex.Message,
                    "Dashboard Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Helpers

        private void NotifyMetricTextChanged()
        {
            OnPropertyChanged(nameof(TotalSessionsText));

            OnPropertyChanged(nameof(TotalAnswersText));

            OnPropertyChanged(nameof(PendingAnswersText));

            OnPropertyChanged(nameof(ReviewedAnswersText));

            OnPropertyChanged(nameof(ActiveTraineesText));

            OnPropertyChanged(nameof(AverageAccuracyText));

            OnPropertyChanged(nameof(LatestSessionText));
        }

        #endregion
    }
}
