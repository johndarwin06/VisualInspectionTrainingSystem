#region Namespaces

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// ViewModel for the reports window.
    /// </summary>
    public class ReportsViewModel : BaseViewModel
    {
        #region Fields

        private readonly ReportRepository _reportRepository;

        private readonly RelayCommand _refreshCommand;

        private readonly RelayCommand _exportCsvCommand;

        private readonly RelayCommand _todayCommand;

        private readonly RelayCommand _lastSevenDaysCommand;

        private readonly RelayCommand _thisMonthCommand;

        private readonly RelayCommand _allDatesCommand;

        private ReportSummary _summary;

        private DateTime? _startDate;

        private DateTime? _endDate;

        private bool _includeAllDates;

        private string _statusMessage;

        private bool _isBusy;

        #endregion

        #region Constructor

        public ReportsViewModel()
        {
            _reportRepository = new ReportRepository();

            Sessions = new ObservableCollection<ReportSessionRow>();

            _summary = new ReportSummary();

            _startDate = DateTime.Today.AddDays(-6);

            _endDate = DateTime.Today;

            _refreshCommand = new RelayCommand(
                LoadReports,
                CanRunCommand);

            _exportCsvCommand = new RelayCommand(
                ExportCsv,
                CanExportCsv);

            _todayCommand = new RelayCommand(
                ShowToday,
                CanRunCommand);

            _lastSevenDaysCommand = new RelayCommand(
                ShowLastSevenDays,
                CanRunCommand);

            _thisMonthCommand = new RelayCommand(
                ShowThisMonth,
                CanRunCommand);

            _allDatesCommand = new RelayCommand(
                ShowAllDates,
                CanRunCommand);

            RefreshCommand = _refreshCommand;

            ExportCsvCommand = _exportCsvCommand;

            TodayCommand = _todayCommand;

            LastSevenDaysCommand = _lastSevenDaysCommand;

            ThisMonthCommand = _thisMonthCommand;

            AllDatesCommand = _allDatesCommand;

            LoadReports();
        }

        #endregion

        #region Properties

        public ObservableCollection<ReportSessionRow> Sessions
        {
            get;
        }

        public ReportSummary Summary
        {
            get
            {
                return _summary;
            }
            private set
            {
                if (SetProperty(ref _summary, value))
                {
                    NotifySummaryTextChanged();
                }
            }
        }

        public DateTime? StartDate
        {
            get
            {
                return _startDate;
            }
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    IncludeAllDates = false;

                    OnPropertyChanged(nameof(DateRangeText));
                }
            }
        }

        public DateTime? EndDate
        {
            get
            {
                return _endDate;
            }
            set
            {
                if (SetProperty(ref _endDate, value))
                {
                    IncludeAllDates = false;

                    OnPropertyChanged(nameof(DateRangeText));
                }
            }
        }

        public bool IncludeAllDates
        {
            get
            {
                return _includeAllDates;
            }
            private set
            {
                if (SetProperty(ref _includeAllDates, value))
                {
                    OnPropertyChanged(nameof(DateRangeText));
                }
            }
        }

        public string DateRangeText
        {
            get
            {
                if (IncludeAllDates)
                    return "All dates";

                return $"{FormatDate(StartDate)} to {FormatDate(EndDate)}";
            }
        }

        public string SessionCountText
        {
            get
            {
                return Summary.SessionCount.ToString();
            }
        }

        public string TotalQuestionsText
        {
            get
            {
                return Summary.TotalQuestions.ToString();
            }
        }

        public string AverageAccuracyText
        {
            get
            {
                return Summary.AverageAccuracy.ToString("0.00") + "%";
            }
        }

        public string PendingAnswersText
        {
            get
            {
                return Summary.PendingAnswers.ToString();
            }
        }

        public string ReviewedAnswersText
        {
            get
            {
                return Summary.ReviewedAnswers.ToString();
            }
        }

        public string TraineeCountText
        {
            get
            {
                return Summary.TraineeCount.ToString();
            }
        }

        public string CorrectWrongText
        {
            get
            {
                return $"{Summary.CorrectAnswers} / {Summary.WrongAnswers}";
            }
        }

        public string FirstLastSessionText
        {
            get
            {
                return $"{FormatDateTime(Summary.FirstSessionTime)} - {FormatDateTime(Summary.LastSessionTime)}";
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

        #region Commands

        public ICommand RefreshCommand
        {
            get;
        }

        public ICommand ExportCsvCommand
        {
            get;
        }

        public ICommand TodayCommand
        {
            get;
        }

        public ICommand LastSevenDaysCommand
        {
            get;
        }

        public ICommand ThisMonthCommand
        {
            get;
        }

        public ICommand AllDatesCommand
        {
            get;
        }

        #endregion

        #region Loading

        private void LoadReports()
        {
            DateTime? startDate;

            DateTime? endDateExclusive;

            if (!TryBuildDateRange(
                out startDate,
                out endDateExclusive))
            {
                return;
            }

            try
            {
                IsBusy = true;

                Summary = _reportRepository.GetSummary(
                    startDate,
                    endDateExclusive);

                Sessions.Clear();

                foreach (ReportSessionRow session in _reportRepository.GetSessions(
                    startDate,
                    endDateExclusive))
                {
                    Sessions.Add(session);
                }

                StatusMessage =
                    $"Loaded {Sessions.Count} session report row(s) for {DateRangeText}.";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;

                MessageBox.Show(
                    ex.Message,
                    "Reports Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool TryBuildDateRange(
            out DateTime? startDate,
            out DateTime? endDateExclusive)
        {
            startDate = null;

            endDateExclusive = null;

            if (IncludeAllDates)
                return true;

            if (!StartDate.HasValue ||
                !EndDate.HasValue)
            {
                StatusMessage = "Please choose both start and end dates.";

                return false;
            }

            DateTime start = StartDate.Value.Date;

            DateTime end = EndDate.Value.Date;

            if (start > end)
            {
                StatusMessage = "Start date cannot be after end date.";

                return false;
            }

            startDate = start;

            endDateExclusive = end.AddDays(1);

            return true;
        }

        #endregion

        #region Filters

        private void ShowToday()
        {
            StartDate = DateTime.Today;

            EndDate = DateTime.Today;

            IncludeAllDates = false;

            LoadReports();
        }

        private void ShowLastSevenDays()
        {
            StartDate = DateTime.Today.AddDays(-6);

            EndDate = DateTime.Today;

            IncludeAllDates = false;

            LoadReports();
        }

        private void ShowThisMonth()
        {
            DateTime today = DateTime.Today;

            StartDate = new DateTime(
                today.Year,
                today.Month,
                1);

            EndDate = today;

            IncludeAllDates = false;

            LoadReports();
        }

        private void ShowAllDates()
        {
            IncludeAllDates = true;

            LoadReports();
        }

        #endregion

        #region Export

        private bool CanExportCsv()
        {
            return !IsBusy &&
                   Sessions.Count > 0;
        }

        private void ExportCsv()
        {
            if (Sessions.Count == 0)
            {
                StatusMessage = "There are no report rows to export.";

                MessageBox.Show(
                    StatusMessage,
                    "Export Reports",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = ".csv",
                FileName = $"TrainingReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                OverwritePrompt = true,
                Title = "Export Training Report"
            };

            bool? result = dialog.ShowDialog();

            if (result != true)
                return;

            try
            {
                string csv = BuildCsv();

                File.WriteAllText(
                    dialog.FileName,
                    csv,
                    Encoding.UTF8);

                StatusMessage =
                    $"Exported {Sessions.Count} report row(s) to {dialog.FileName}.";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;

                MessageBox.Show(
                    ex.Message,
                    "Export Reports Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string BuildCsv()
        {
            StringBuilder builder = new StringBuilder();

            AppendCsvRow(
                builder,
                "SessionID",
                "EmployeeNo",
                "FullName",
                "Department",
                "StartTime",
                "EndTime",
                "TotalQuestions",
                "CorrectAnswers",
                "WrongAnswers",
                "PendingAnswers",
                "ReviewedAnswers",
                "Accuracy",
                "Status");

            foreach (ReportSessionRow session in Sessions)
            {
                AppendCsvRow(
                    builder,
                    session.SessionID.ToString(CultureInfo.InvariantCulture),
                    session.EmployeeNo,
                    session.FullName,
                    session.Department,
                    FormatDateTime(session.StartTime),
                    FormatDateTime(session.EndTime),
                    session.TotalQuestions.ToString(CultureInfo.InvariantCulture),
                    session.CorrectAnswers.ToString(CultureInfo.InvariantCulture),
                    session.WrongAnswers.ToString(CultureInfo.InvariantCulture),
                    session.PendingAnswers.ToString(CultureInfo.InvariantCulture),
                    session.ReviewedAnswers.ToString(CultureInfo.InvariantCulture),
                    session.Accuracy.ToString("0.00", CultureInfo.InvariantCulture),
                    session.Status);
            }

            return builder.ToString();
        }

        private static void AppendCsvRow(
            StringBuilder builder,
            params string[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(EscapeCsv(values[index]));
            }

            builder.AppendLine();
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
                return string.Empty;

            bool mustQuote =
                value.Contains(",") ||
                value.Contains("\"") ||
                value.Contains("\r") ||
                value.Contains("\n");

            if (!mustQuote)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        #endregion

        #region Command State

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private void RefreshCommands()
        {
            _refreshCommand.RaiseCanExecuteChanged();

            _exportCsvCommand.RaiseCanExecuteChanged();

            _todayCommand.RaiseCanExecuteChanged();

            _lastSevenDaysCommand.RaiseCanExecuteChanged();

            _thisMonthCommand.RaiseCanExecuteChanged();

            _allDatesCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region Helpers

        private void NotifySummaryTextChanged()
        {
            OnPropertyChanged(nameof(SessionCountText));

            OnPropertyChanged(nameof(TotalQuestionsText));

            OnPropertyChanged(nameof(AverageAccuracyText));

            OnPropertyChanged(nameof(PendingAnswersText));

            OnPropertyChanged(nameof(ReviewedAnswersText));

            OnPropertyChanged(nameof(TraineeCountText));

            OnPropertyChanged(nameof(CorrectWrongText));

            OnPropertyChanged(nameof(FirstLastSessionText));
        }

        private static string FormatDate(DateTime? value)
        {
            if (!value.HasValue)
                return "-";

            return value.Value.ToString("yyyy-MM-dd");
        }

        private static string FormatDateTime(DateTime? value)
        {
            if (!value.HasValue)
                return "-";

            return value.Value.ToString("yyyy-MM-dd HH:mm");
        }

        private static string FormatDateTime(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        #endregion
    }
}
