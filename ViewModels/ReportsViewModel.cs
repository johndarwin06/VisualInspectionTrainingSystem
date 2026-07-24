#region Namespaces

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
    /// Provides asynchronous report filtering, display, and document-export state.
    /// </summary>
    public class ReportsViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        private const string LoadErrorMessage =
            "Reports could not be loaded. Please try again. " +
            "Contact support if the problem continues.";

        private const string ExportErrorMessage =
            "The report could not be exported. Please choose another destination " +
            "or contact support if the problem continues.";

        private const string InvalidRangeMessage =
            "Select a valid start date and end date. The start date must not be later " +
            "than the end date.";

        private const string EmptyExportMessage =
            "There are no matching report sessions to export.";

        private const string ExportLimitMessage =
            "This selection exceeds the 10,000-session export safeguard. " +
            "Choose a smaller date range and try again.";

        #endregion

        #region Fields

        private readonly IReportRepository _reportRepository;
        private readonly IReportExportService _reportExportService;

        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _exportCsvCommand;
        private readonly RelayCommand _exportExcelCommand;
        private readonly RelayCommand _exportPdfCommand;
        private readonly RelayCommand _todayCommand;
        private readonly RelayCommand _thisWeekCommand;
        private readonly RelayCommand _lastSevenDaysCommand;
        private readonly RelayCommand _thisMonthCommand;
        private readonly RelayCommand _allDatesCommand;

        private ReportSummary _summary;
        private ReportPeriod _activePeriod;
        private DateTime? _startDate;
        private DateTime? _endDate;
        private string _statusMessage;
        private bool _includeAllDates;
        private bool _isLoading;
        private bool _isExporting;
        private bool _isDisplayLimited;
        private bool _isDisposed;
        private int _operationVersion;
        private CancellationTokenSource _operationCancellation;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the Reports ViewModel with production services.
        /// </summary>
        public ReportsViewModel()
            : this(
                new ReportRepository(),
                new ReportExportService())
        {
        }

        /// <summary>
        /// Initializes the Reports ViewModel with explicit report services.
        /// </summary>
        /// <param name="reportRepository">Repository used to load display and export snapshots.</param>
        /// <param name="reportExportService">Service used to generate document files.</param>
        public ReportsViewModel(
            IReportRepository reportRepository,
            IReportExportService reportExportService)
        {
            if (reportRepository == null)
            {
                throw new ArgumentNullException(nameof(reportRepository));
            }

            if (reportExportService == null)
            {
                throw new ArgumentNullException(nameof(reportExportService));
            }

            _reportRepository = reportRepository;
            _reportExportService = reportExportService;
            _summary = new ReportSummary();
            _activePeriod = ReportPeriod.CreateLastSevenDays(DateTime.Today);
            _statusMessage = "Loading the Last 7 Days report...";

            Sessions = new ObservableCollection<ReportSessionRow>();

            _refreshCommand = new RelayCommand(BeginRefresh, CanRunCommand);
            _exportCsvCommand = new RelayCommand(BeginCsvExport, CanExport);
            _exportExcelCommand = new RelayCommand(BeginExcelExport, CanExport);
            _exportPdfCommand = new RelayCommand(BeginPdfExport, CanExport);
            _todayCommand = new RelayCommand(SelectToday, CanRunCommand);
            _thisWeekCommand = new RelayCommand(SelectThisWeek, CanRunCommand);
            _lastSevenDaysCommand = new RelayCommand(
                SelectLastSevenDays,
                CanRunCommand);
            _thisMonthCommand = new RelayCommand(SelectThisMonth, CanRunCommand);
            _allDatesCommand = new RelayCommand(SelectAllDates, CanRunCommand);

            RefreshCommand = _refreshCommand;
            ExportCsvCommand = _exportCsvCommand;
            ExportExcelCommand = _exportExcelCommand;
            ExportPdfCommand = _exportPdfCommand;
            TodayCommand = _todayCommand;
            ThisWeekCommand = _thisWeekCommand;
            LastSevenDaysCommand = _lastSevenDaysCommand;
            ThisMonthCommand = _thisMonthCommand;
            AllDatesCommand = _allDatesCommand;

            ApplyPeriodToDatePickers(_activePeriod);
            BeginLoadPeriod(_activePeriod);
        }

        #endregion

        #region Collections

        /// <summary>
        /// Gets the current bounded interactive session rows.
        /// </summary>
        public ObservableCollection<ReportSessionRow> Sessions
        {
            get;
        }

        #endregion

        #region Summary Properties

        /// <summary>
        /// Gets the current aggregate report snapshot.
        /// </summary>
        public ReportSummary Summary
        {
            get
            {
                return _summary;
            }
            private set
            {
                if (SetProperty(
                        ref _summary,
                        value ?? new ReportSummary()))
                {
                    NotifySummaryChanged();
                }
            }
        }

        /// <summary>
        /// Gets the total matching-session count.
        /// </summary>
        public string SessionCountText
        {
            get
            {
                return Summary.SessionCount.ToString();
            }
        }

        /// <summary>
        /// Gets completed and open session counts.
        /// </summary>
        public string CompletedOpenSessionsText
        {
            get
            {
                return "Completed " + Summary.CompletedSessionCount +
                       " / Open " + Summary.OpenSessionCount;
            }
        }

        /// <summary>
        /// Gets the total configured question count.
        /// </summary>
        public string TotalQuestionsText
        {
            get
            {
                return Summary.TotalQuestions.ToString();
            }
        }

        /// <summary>
        /// Gets reviewed-only average accuracy or N/A.
        /// </summary>
        public string AverageAccuracyText
        {
            get
            {
                return Summary.AverageReviewedAccuracy.HasValue
                    ? Summary.AverageReviewedAccuracy.Value.ToString("0.00") + "%"
                    : "N/A";
            }
        }

        /// <summary>
        /// Gets the number of answers pending supported GOOD/NG truth.
        /// </summary>
        public string PendingAnswersText
        {
            get
            {
                return Summary.PendingAnswers.ToString();
            }
        }

        /// <summary>
        /// Gets the number of answers with supported GOOD/NG truth.
        /// </summary>
        public string ReviewedAnswersText
        {
            get
            {
                return Summary.ReviewedAnswers.ToString();
            }
        }

        /// <summary>
        /// Gets the distinct matching-trainee count.
        /// </summary>
        public string TraineeCountText
        {
            get
            {
                return Summary.TraineeCount.ToString();
            }
        }

        /// <summary>
        /// Gets correct and wrong reviewed-answer counts.
        /// </summary>
        public string CorrectWrongText
        {
            get
            {
                return "Correct " + Summary.CorrectAnswers +
                       " / Wrong " + Summary.WrongAnswers;
            }
        }

        /// <summary>
        /// Gets first and last matching session start times.
        /// </summary>
        public string FirstLastSessionText
        {
            get
            {
                return FormatOptionalTime(Summary.FirstSessionTime) +
                       " / " +
                       FormatOptionalTime(Summary.LastSessionTime);
            }
        }

        #endregion

        #region Period Properties

        /// <summary>
        /// Gets or sets the inclusive custom start date.
        /// </summary>
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
                }
            }
        }

        /// <summary>
        /// Gets or sets the inclusive custom end date.
        /// </summary>
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
                }
            }
        }

        /// <summary>
        /// Gets whether the selected report is unbounded by date.
        /// </summary>
        public bool IncludeAllDates
        {
            get
            {
                return _includeAllDates;
            }
            private set
            {
                SetProperty(ref _includeAllDates, value);
            }
        }

        /// <summary>
        /// Gets the current period's selected-range label.
        /// </summary>
        public string DateRangeText
        {
            get
            {
                return _activePeriod == null
                    ? "No period selected"
                    : _activePeriod.DateRangeText;
            }
        }

        /// <summary>
        /// Gets a clear active report-period label.
        /// </summary>
        public string ActivePeriodText
        {
            get
            {
                return _activePeriod == null
                    ? "Active period: none"
                    : "Active period: " + _activePeriod.ReportTypeText;
            }
        }

        #endregion

        #region State Properties

        /// <summary>
        /// Gets or sets the current non-sensitive status message.
        /// </summary>
        public string StatusMessage
        {
            get
            {
                return _statusMessage;
            }
            private set
            {
                SetProperty(ref _statusMessage, value);
            }
        }

        /// <summary>
        /// Gets whether report data is currently loading.
        /// </summary>
        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }
            private set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(IsBusy));
                    OnPropertyChanged(nameof(BusyStatusText));
                    RefreshCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets whether a complete report is currently being exported.
        /// </summary>
        public bool IsExporting
        {
            get
            {
                return _isExporting;
            }
            private set
            {
                if (SetProperty(ref _isExporting, value))
                {
                    OnPropertyChanged(nameof(IsBusy));
                    OnPropertyChanged(nameof(BusyStatusText));
                    RefreshCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets whether any Reports operation is active.
        /// </summary>
        public bool IsBusy
        {
            get
            {
                return IsLoading || IsExporting;
            }
        }

        /// <summary>
        /// Gets a clear loading or export state label.
        /// </summary>
        public string BusyStatusText
        {
            get
            {
                if (IsExporting)
                {
                    return "Preparing complete export...";
                }

                if (IsLoading)
                {
                    return "Loading report...";
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Gets whether additional matching rows are omitted from the interactive table.
        /// </summary>
        public bool IsDisplayLimited
        {
            get
            {
                return _isDisplayLimited;
            }
            private set
            {
                if (SetProperty(ref _isDisplayLimited, value))
                {
                    OnPropertyChanged(nameof(DisplayLimitText));
                }
            }
        }

        /// <summary>
        /// Gets the disclosed interactive row-limit state.
        /// </summary>
        public string DisplayLimitText
        {
            get
            {
                if (IsDisplayLimited)
                {
                    return "Showing the newest " +
                           ReportRepository.InteractiveDisplayLimit +
                           " of " + Summary.SessionCount +
                           " sessions. Exports include every matching row.";
                }

                return "Showing all " + Summary.SessionCount + " matching sessions.";
            }
        }

        /// <summary>
        /// Gets whether the current selection has no matching sessions.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return Summary.SessionCount == 0 && !IsLoading;
            }
        }

        /// <summary>
        /// Gets the interactive empty-state message.
        /// </summary>
        public string EmptyStateText
        {
            get
            {
                return IsEmpty
                    ? "No training sessions match the selected report period."
                    : string.Empty;
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the custom-range refresh command.
        /// </summary>
        public ICommand RefreshCommand { get; private set; }

        /// <summary>
        /// Gets the complete CSV export command.
        /// </summary>
        public ICommand ExportCsvCommand { get; private set; }

        /// <summary>
        /// Gets the complete Excel export command.
        /// </summary>
        public ICommand ExportExcelCommand { get; private set; }

        /// <summary>
        /// Gets the complete PDF export command.
        /// </summary>
        public ICommand ExportPdfCommand { get; private set; }

        /// <summary>
        /// Gets the local-calendar Today command.
        /// </summary>
        public ICommand TodayCommand { get; private set; }

        /// <summary>
        /// Gets the Monday-through-Sunday current-week command.
        /// </summary>
        public ICommand ThisWeekCommand { get; private set; }

        /// <summary>
        /// Gets the rolling Last 7 Days command.
        /// </summary>
        public ICommand LastSevenDaysCommand { get; private set; }

        /// <summary>
        /// Gets the current-calendar-month command.
        /// </summary>
        public ICommand ThisMonthCommand { get; private set; }

        /// <summary>
        /// Gets the unbounded All Dates command.
        /// </summary>
        public ICommand AllDatesCommand { get; private set; }

        #endregion

        #region Loading

        /// <summary>
        /// Refreshes the custom picker range, or the current all-dates period.
        /// </summary>
        /// <returns>A task that completes when the refresh settles.</returns>
        public Task RefreshAsync()
        {
            if (IsBusy || _isDisposed)
            {
                return Task.FromResult(0);
            }

            ReportPeriod period;

            if (IncludeAllDates)
            {
                period = ReportPeriod.CreateAllDates();
            }
            else if (!TryCreateCustomPeriod(out period))
            {
                StatusMessage = InvalidRangeMessage;
                return Task.FromResult(0);
            }

            return LoadPeriodAsync(period);
        }

        /// <summary>
        /// Loads one report period without blocking the WPF dispatcher.
        /// </summary>
        private async Task LoadPeriodAsync(ReportPeriod period)
        {
            if (period == null || IsBusy || _isDisposed)
            {
                return;
            }

            int version = BeginOperation();
            CancellationToken token = _operationCancellation.Token;
            IsLoading = true;
            StatusMessage = "Loading the " + period.ReportTypeText + " report...";

            try
            {
                Task<ReportSnapshot> worker = Task.Run(
                    () => _reportRepository.GetDisplaySnapshot(period),
                    token);
                ReportSnapshot snapshot = await AwaitWithCancellation(
                    worker,
                    token);

                if (!CanPublish(version))
                {
                    return;
                }

                _activePeriod = period;
                ApplyPeriodToDatePickers(period);
                PublishSnapshot(snapshot);
                StatusMessage = BuildLoadedStatus(snapshot);
            }
            catch (OperationCanceledException)
            {
                if (CanPublish(version))
                {
                    StatusMessage = "Report loading was cancelled.";
                }
            }
            catch (Exception ex)
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "Reports Load",
                    ex,
                    false);

                if (CanPublish(version))
                {
                    ClearReportData();
                    StatusMessage = LoadErrorMessage;
                }
            }
            finally
            {
                CompleteOperation(version, true);
            }
        }

        /// <summary>
        /// Starts the refresh command while observing all asynchronous failures.
        /// </summary>
        private async void BeginRefresh()
        {
            await RefreshAsync();
        }

        /// <summary>
        /// Starts a preset load while observing all asynchronous failures.
        /// </summary>
        private async void BeginLoadPeriod(ReportPeriod period)
        {
            await LoadPeriodAsync(period);
        }

        #endregion

        #region Period Commands

        /// <summary>
        /// Selects today's local calendar day.
        /// </summary>
        private void SelectToday()
        {
            BeginLoadPeriod(ReportPeriod.CreateDaily(DateTime.Today));
        }

        /// <summary>
        /// Selects the current Monday-through-following-Monday calendar week.
        /// </summary>
        private void SelectThisWeek()
        {
            BeginLoadPeriod(ReportPeriod.CreateWeekly(DateTime.Today));
        }

        /// <summary>
        /// Selects the rolling seven local calendar days ending today.
        /// </summary>
        private void SelectLastSevenDays()
        {
            BeginLoadPeriod(ReportPeriod.CreateLastSevenDays(DateTime.Today));
        }

        /// <summary>
        /// Selects the first day through next first day of the current month.
        /// </summary>
        private void SelectThisMonth()
        {
            BeginLoadPeriod(ReportPeriod.CreateMonthly(DateTime.Today));
        }

        /// <summary>
        /// Selects all stored dates.
        /// </summary>
        private void SelectAllDates()
        {
            BeginLoadPeriod(ReportPeriod.CreateAllDates());
        }

        /// <summary>
        /// Builds a custom period from inclusive date-picker values.
        /// </summary>
        private bool TryCreateCustomPeriod(out ReportPeriod period)
        {
            period = null;

            if (!StartDate.HasValue || !EndDate.HasValue)
            {
                return false;
            }

            try
            {
                period = ReportPeriod.CreateCustomInclusive(
                    StartDate.Value,
                    EndDate.Value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        #endregion

        #region Exporting

        /// <summary>
        /// Exports CSV to an explicit destination without displaying a save dialog.
        /// </summary>
        public Task ExportCsvToFileAsync(string filePath)
        {
            return ExportToFileAsync(ReportExportKind.Csv, filePath);
        }

        /// <summary>
        /// Exports Excel to an explicit destination without displaying a save dialog.
        /// </summary>
        public Task ExportExcelToFileAsync(string filePath)
        {
            return ExportToFileAsync(ReportExportKind.Excel, filePath);
        }

        /// <summary>
        /// Exports PDF to an explicit destination without displaying a save dialog.
        /// </summary>
        public Task ExportPdfToFileAsync(string filePath)
        {
            return ExportToFileAsync(ReportExportKind.Pdf, filePath);
        }

        /// <summary>
        /// Loads a complete snapshot and generates one file off the dispatcher.
        /// </summary>
        private async Task ExportToFileAsync(
            ReportExportKind exportKind,
            string filePath)
        {
            if (IsBusy || _isDisposed)
            {
                return;
            }

            if (Summary.SessionCount == 0)
            {
                StatusMessage = EmptyExportMessage;
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                StatusMessage = "Export was cancelled.";
                return;
            }

            int version = BeginOperation();
            CancellationToken token = _operationCancellation.Token;
            ReportPeriod period = _activePeriod;
            IsExporting = true;
            StatusMessage = "Preparing the complete " +
                            GetExportLabel(exportKind) +
                            " report...";

            try
            {
                Task<ReportSnapshot> snapshotWorker = Task.Run(
                    () => _reportRepository.GetExportSnapshot(period),
                    token);
                ReportSnapshot snapshot = await AwaitWithCancellation(
                    snapshotWorker,
                    token);

                if (!CanPublish(version))
                {
                    return;
                }

                if (snapshot.IsExportLimitExceeded)
                {
                    StatusMessage = ExportLimitMessage;
                    return;
                }

                if (snapshot.Sessions.Count == 0)
                {
                    StatusMessage = EmptyExportMessage;
                    return;
                }

                Task exportWorker = Task.Run(
                    () => GenerateExport(
                        exportKind,
                        snapshot,
                        filePath,
                        token),
                    token);
                await AwaitWithCancellation(exportWorker, token);

                if (CanPublish(version))
                {
                    StatusMessage = GetExportLabel(exportKind) +
                                    " report exported successfully.";
                }
            }
            catch (OperationCanceledException)
            {
                if (CanPublish(version))
                {
                    StatusMessage = "Report export was cancelled.";
                }
            }
            catch (Exception ex)
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "Reports " + GetExportLabel(exportKind) + " Export",
                    ex,
                    false);

                if (CanPublish(version))
                {
                    StatusMessage = ExportErrorMessage;
                }
            }
            finally
            {
                CompleteOperation(version, false);
            }
        }

        /// <summary>
        /// Generates the selected export format from one complete snapshot.
        /// </summary>
        private void GenerateExport(
            ReportExportKind exportKind,
            ReportSnapshot snapshot,
            string filePath,
            CancellationToken cancellationToken)
        {
            switch (exportKind)
            {
                case ReportExportKind.Csv:
                    _reportExportService.ExportCsv(
                        snapshot,
                        filePath,
                        cancellationToken);
                    break;
                case ReportExportKind.Excel:
                    _reportExportService.ExportExcel(
                        snapshot,
                        filePath,
                        cancellationToken);
                    break;
                default:
                    _reportExportService.ExportPdf(
                        snapshot,
                        filePath,
                        cancellationToken);
                    break;
            }
        }

        /// <summary>
        /// Opens the CSV save dialog and begins export when accepted.
        /// </summary>
        private void BeginCsvExport()
        {
            BeginExportWithDialog(ReportExportKind.Csv);
        }

        /// <summary>
        /// Opens the Excel save dialog and begins export when accepted.
        /// </summary>
        private void BeginExcelExport()
        {
            BeginExportWithDialog(ReportExportKind.Excel);
        }

        /// <summary>
        /// Opens the PDF save dialog and begins export when accepted.
        /// </summary>
        private void BeginPdfExport()
        {
            BeginExportWithDialog(ReportExportKind.Pdf);
        }

        /// <summary>
        /// Collects a destination on the UI thread, then starts asynchronous generation.
        /// </summary>
        private async void BeginExportWithDialog(ReportExportKind exportKind)
        {
            string filePath = ShowSaveDialog(exportKind);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                StatusMessage = "Export was cancelled.";
                return;
            }

            await ExportToFileAsync(exportKind, filePath);
        }

        /// <summary>
        /// Shows a format-specific native save dialog.
        /// </summary>
        private string ShowSaveDialog(ReportExportKind exportKind)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                AddExtension = true,
                CheckPathExists = true,
                DefaultExt = GetExportExtension(exportKind),
                Filter = GetExportFilter(exportKind),
                FileName = BuildSuggestedFileName(exportKind),
                OverwritePrompt = true,
                Title = "Export " + GetExportLabel(exportKind) + " Report"
            };
            string exportFolder = TryGetConfiguredExportFolder();

            if (!string.IsNullOrWhiteSpace(exportFolder))
            {
                dialog.InitialDirectory = exportFolder;
            }

            return dialog.ShowDialog() == true
                ? dialog.FileName
                : null;
        }

        #endregion

        #region Async Coordination

        /// <summary>
        /// Starts one cancellable operation generation.
        /// </summary>
        private int BeginOperation()
        {
            CancelActiveOperation();
            _operationCancellation = new CancellationTokenSource();
            _operationVersion++;

            return _operationVersion;
        }

        /// <summary>
        /// Completes an operation without allowing an older operation to alter command state.
        /// </summary>
        private void CompleteOperation(
            int version,
            bool wasLoading)
        {
            if (!CanPublish(version))
            {
                return;
            }

            if (wasLoading)
            {
                IsLoading = false;
            }
            else
            {
                IsExporting = false;
            }

            _operationCancellation.Dispose();
            _operationCancellation = null;
        }

        /// <summary>
        /// Returns whether one operation may still publish WPF state.
        /// </summary>
        private bool CanPublish(int version)
        {
            return !_isDisposed && version == _operationVersion;
        }

        /// <summary>
        /// Cancels and disposes the current coordination token source.
        /// </summary>
        private void CancelActiveOperation()
        {
            CancellationTokenSource cancellation = _operationCancellation;
            _operationCancellation = null;

            if (cancellation == null)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        /// <summary>
        /// Awaits a worker task while allowing window cancellation to return promptly.
        /// </summary>
        private static async Task<T> AwaitWithCancellation<T>(
            Task<T> task,
            CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> cancellationCompletion =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            using (cancellationToken.Register(
                () => cancellationCompletion.TrySetResult(true)))
            {
                Task completedTask = await Task.WhenAny(
                    task,
                    cancellationCompletion.Task);

                if (completedTask != task)
                {
                    ObserveAbandonedTask(task);
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task;
        }

        /// <summary>
        /// Awaits a non-generic worker task with prompt cancellation.
        /// </summary>
        private static async Task AwaitWithCancellation(
            Task task,
            CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> cancellationCompletion =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            using (cancellationToken.Register(
                () => cancellationCompletion.TrySetResult(true)))
            {
                Task completedTask = await Task.WhenAny(
                    task,
                    cancellationCompletion.Task);

                if (completedTask != task)
                {
                    ObserveAbandonedTask(task);
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            await task;
        }

        /// <summary>
        /// Observes any later exception from work abandoned after cancellation.
        /// </summary>
        private static void ObserveAbandonedTask(Task task)
        {
            task.ContinueWith(
                completedTask =>
                {
                    Exception ignored = completedTask.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        #endregion

        #region State Helpers

        /// <summary>
        /// Replaces the current display snapshot without appending duplicate rows.
        /// </summary>
        private void PublishSnapshot(ReportSnapshot snapshot)
        {
            Summary = snapshot == null
                ? new ReportSummary()
                : snapshot.Summary;
            Sessions.Clear();

            if (snapshot != null && snapshot.Sessions != null)
            {
                foreach (ReportSessionRow session in snapshot.Sessions)
                {
                    if (session != null)
                    {
                        Sessions.Add(session);
                    }
                }
            }

            IsDisplayLimited = snapshot != null && snapshot.IsDisplayLimited;
            NotifyCollectionStateChanged();
        }

        /// <summary>
        /// Clears stale report values after a technical load failure.
        /// </summary>
        private void ClearReportData()
        {
            Summary = new ReportSummary();
            Sessions.Clear();
            IsDisplayLimited = false;
            NotifyCollectionStateChanged();
        }

        /// <summary>
        /// Applies one report period to date-picker and label state.
        /// </summary>
        private void ApplyPeriodToDatePickers(ReportPeriod period)
        {
            _startDate = period.StartInclusive;
            _endDate = period.EndInclusive;
            IncludeAllDates = period.PeriodType == ReportPeriodType.AllDates;
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));
            OnPropertyChanged(nameof(DateRangeText));
            OnPropertyChanged(nameof(ActivePeriodText));
        }

        /// <summary>
        /// Builds the successful load status including row-limit disclosure.
        /// </summary>
        private static string BuildLoadedStatus(ReportSnapshot snapshot)
        {
            if (snapshot.Summary.SessionCount == 0)
            {
                return "No sessions matched the selected report period.";
            }

            if (snapshot.IsDisplayLimited)
            {
                return "Report loaded. The table shows the newest " +
                       ReportRepository.InteractiveDisplayLimit +
                       " of " + snapshot.Summary.SessionCount +
                       " sessions; exports load the complete matching dataset.";
            }

            return "Report loaded with " +
                   snapshot.Summary.SessionCount +
                   " matching sessions.";
        }

        /// <summary>
        /// Raises every summary-dependent display property.
        /// </summary>
        private void NotifySummaryChanged()
        {
            OnPropertyChanged(nameof(SessionCountText));
            OnPropertyChanged(nameof(CompletedOpenSessionsText));
            OnPropertyChanged(nameof(TotalQuestionsText));
            OnPropertyChanged(nameof(AverageAccuracyText));
            OnPropertyChanged(nameof(PendingAnswersText));
            OnPropertyChanged(nameof(ReviewedAnswersText));
            OnPropertyChanged(nameof(TraineeCountText));
            OnPropertyChanged(nameof(CorrectWrongText));
            OnPropertyChanged(nameof(FirstLastSessionText));
            NotifyCollectionStateChanged();
            RefreshCommandStates();
        }

        /// <summary>
        /// Raises every collection and empty-state display property.
        /// </summary>
        private void NotifyCollectionStateChanged()
        {
            OnPropertyChanged(nameof(DisplayLimitText));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyStateText));
        }

        /// <summary>
        /// Refreshes all command enabled states.
        /// </summary>
        private void RefreshCommandStates()
        {
            _refreshCommand.RaiseCanExecuteChanged();
            _exportCsvCommand.RaiseCanExecuteChanged();
            _exportExcelCommand.RaiseCanExecuteChanged();
            _exportPdfCommand.RaiseCanExecuteChanged();
            _todayCommand.RaiseCanExecuteChanged();
            _thisWeekCommand.RaiseCanExecuteChanged();
            _lastSevenDaysCommand.RaiseCanExecuteChanged();
            _thisMonthCommand.RaiseCanExecuteChanged();
            _allDatesCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Returns whether a non-export command may execute.
        /// </summary>
        private bool CanRunCommand()
        {
            return !_isDisposed && !IsBusy;
        }

        /// <summary>
        /// Returns whether a complete export may begin.
        /// </summary>
        private bool CanExport()
        {
            return CanRunCommand() && Summary.SessionCount > 0;
        }

        /// <summary>
        /// Formats one optional session time for summary cards.
        /// </summary>
        private static string FormatOptionalTime(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("yyyy-MM-dd HH:mm")
                : "N/A";
        }

        /// <summary>
        /// Returns a non-sensitive export label.
        /// </summary>
        private static string GetExportLabel(ReportExportKind exportKind)
        {
            switch (exportKind)
            {
                case ReportExportKind.Csv:
                    return "CSV";
                case ReportExportKind.Excel:
                    return "Excel";
                default:
                    return "PDF";
            }
        }

        /// <summary>
        /// Returns the selected format's file extension.
        /// </summary>
        private static string GetExportExtension(ReportExportKind exportKind)
        {
            switch (exportKind)
            {
                case ReportExportKind.Csv:
                    return ".csv";
                case ReportExportKind.Excel:
                    return ".xlsx";
                default:
                    return ".pdf";
            }
        }

        /// <summary>
        /// Returns the selected format's save-dialog filter.
        /// </summary>
        private static string GetExportFilter(ReportExportKind exportKind)
        {
            switch (exportKind)
            {
                case ReportExportKind.Csv:
                    return "CSV Files (*.csv)|*.csv";
                case ReportExportKind.Excel:
                    return "Excel Workbooks (*.xlsx)|*.xlsx";
                default:
                    return "PDF Documents (*.pdf)|*.pdf";
            }
        }

        /// <summary>
        /// Builds a filesystem-safe suggested export name.
        /// </summary>
        private string BuildSuggestedFileName(ReportExportKind exportKind)
        {
            string periodName = _activePeriod.ReportTypeText
                .ToLowerInvariant()
                .Replace(" ", "-");

            return "visual-inspection-" +
                   periodName + "-" +
                   _activePeriod.FileNameToken + "-" +
                   DateTime.Now.ToString("yyyyMMdd-HHmmss") +
                   GetExportExtension(exportKind);
        }

        /// <summary>
        /// Returns the already-loaded configured export directory when safely available.
        /// </summary>
        private static string TryGetConfiguredExportFolder()
        {
            try
            {
                string folder = ConfigurationService
                    .GetApplicationSettings()
                    .Paths
                    .ExportFolder;

                return !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)
                    ? folder
                    : null;
            }
            catch (Exception ex)
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "Reports Export Folder",
                    ex,
                    false);
                return null;
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Cancels active work and prevents any late result from updating WPF state.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _operationVersion++;
            CancelActiveOperation();
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Identifies a supported document export format.
        /// </summary>
        private enum ReportExportKind
        {
            Csv,
            Excel,
            Pdf
        }

        #endregion
    }
}
