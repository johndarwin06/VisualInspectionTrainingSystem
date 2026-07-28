#region Namespaces

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Maps chart-neutral analytics data to replaceable LiveCharts presentation objects.
    /// </summary>
    public class AnalyticsChartViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        private const string DefaultUnavailableMessage = "Chart data is unavailable.";
        private const string DefaultEmptyMessage = "No activity is available for this period.";
        private const int MaximumStatusMessageLength = 160;

        #endregion

        #region Fields

        private readonly ApplicationThemeService _applicationThemeService;
        private readonly ChartThemeService _themeService;
        private List<ChartPoint> _sourcePoints;
        private AnalyticsChartData _sourceData;
        private ChartThemePalette _palette;
        private bool _isDisposed;
        private bool _isDarkTheme;
        private bool _isAvailable;
        private bool _isEmpty;
        private bool _hasCompletedSessionsData;
        private bool _hasSelectionData;
        private bool _hasReviewCoverageData;
        private bool _hasReviewedAccuracyData;
        private bool _hasDurationData;
        private string _emptyStateMessage;
        private string _rangeText;
        private ISeries[] _completedSessionsSeries;
        private ISeries[] _goodVsNgSeries;
        private ISeries[] _reviewCoverageSeries;
        private ISeries[] _reviewedAccuracyTrendSeries;
        private ISeries[] _durationTrendSeries;
        private Axis[] _completedSessionsXAxes;
        private Axis[] _completedSessionsYAxes;
        private Axis[] _selectionXAxes;
        private Axis[] _selectionYAxes;
        private Axis[] _reviewedAccuracyXAxes;
        private Axis[] _reviewedAccuracyYAxes;
        private Axis[] _durationXAxes;
        private Axis[] _durationYAxes;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes an empty chart presentation using the current application theme.
        /// </summary>
        public AnalyticsChartViewModel()
            : this(
                new AnalyticsChartData(),
                ApplicationThemeService.Current.IsDarkTheme)
        {
        }

        /// <summary>
        /// Initializes a chart presentation for the supplied data using the current application theme.
        /// </summary>
        /// <param name="data">Chart-neutral analytics data.</param>
        public AnalyticsChartViewModel(AnalyticsChartData data)
            : this(
                data,
                ApplicationThemeService.Current.IsDarkTheme)
        {
        }

        /// <summary>
        /// Initializes a chart presentation for the supplied data and theme.
        /// </summary>
        /// <param name="data">Chart-neutral analytics data.</param>
        /// <param name="isDarkTheme">True to use the dark chart palette.</param>
        public AnalyticsChartViewModel(
            AnalyticsChartData data,
            bool isDarkTheme)
        {
            _applicationThemeService = ApplicationThemeService.Current;
            _themeService = new ChartThemeService();
            _sourcePoints = new List<ChartPoint>();
            _sourceData = new AnalyticsChartData();
            _palette = _themeService.UseTheme(isDarkTheme);
            _isDarkTheme = isDarkTheme;
            _emptyStateMessage = DefaultEmptyMessage;
            _rangeText = string.Empty;
            _completedSessionsSeries = new ISeries[0];
            _goodVsNgSeries = new ISeries[0];
            _reviewCoverageSeries = new ISeries[0];
            _reviewedAccuracyTrendSeries = new ISeries[0];
            _durationTrendSeries = new ISeries[0];
            _completedSessionsXAxes = new Axis[0];
            _completedSessionsYAxes = new Axis[0];
            _selectionXAxes = new Axis[0];
            _selectionYAxes = new Axis[0];
            _reviewedAccuracyXAxes = new Axis[0];
            _reviewedAccuracyYAxes = new Axis[0];
            _durationXAxes = new Axis[0];
            _durationYAxes = new Axis[0];

            _applicationThemeService.ThemeChanged +=
                OnApplicationThemeChanged;
            Update(data);
        }

        #endregion

        #region Availability Properties

        /// <summary>
        /// Gets whether the repository supplied chart data for the selected range.
        /// </summary>
        public bool IsAvailable
        {
            get { return _isAvailable; }
            private set { SetProperty(ref _isAvailable, value); }
        }

        /// <summary>
        /// Gets whether every chart is empty for the selected range.
        /// </summary>
        public bool IsEmpty
        {
            get { return _isEmpty; }
            private set { SetProperty(ref _isEmpty, value); }
        }

        /// <summary>
        /// Gets a fixed or bounded non-sensitive unavailable/empty message.
        /// </summary>
        public string EmptyStateMessage
        {
            get { return _emptyStateMessage; }
            private set { SetProperty(ref _emptyStateMessage, value); }
        }

        /// <summary>
        /// Gets a local-calendar description of the selected chart range.
        /// </summary>
        public string RangeText
        {
            get { return _rangeText; }
            private set { SetProperty(ref _rangeText, value); }
        }

        /// <summary>
        /// Gets whether at least one completed session exists in the range.
        /// </summary>
        public bool HasCompletedSessionsData
        {
            get { return _hasCompletedSessionsData; }
            private set { SetProperty(ref _hasCompletedSessionsData, value); }
        }

        /// <summary>
        /// Gets whether at least one supported GOOD or NG trainee selection exists.
        /// </summary>
        public bool HasSelectionData
        {
            get { return _hasSelectionData; }
            private set { SetProperty(ref _hasSelectionData, value); }
        }

        /// <summary>
        /// Gets whether at least one reviewed or pending answer exists.
        /// </summary>
        public bool HasReviewCoverageData
        {
            get { return _hasReviewCoverageData; }
            private set { SetProperty(ref _hasReviewCoverageData, value); }
        }

        /// <summary>
        /// Gets whether any daily point has a reviewed-only accuracy denominator.
        /// </summary>
        public bool HasReviewedAccuracyData
        {
            get { return _hasReviewedAccuracyData; }
            private set { SetProperty(ref _hasReviewedAccuracyData, value); }
        }

        /// <summary>
        /// Gets whether at least one valid completed-session duration is greater than zero.
        /// </summary>
        public bool HasDurationData
        {
            get { return _hasDurationData; }
            private set { SetProperty(ref _hasDurationData, value); }
        }

        /// <summary>
        /// Gets whether the dark chart palette is active.
        /// </summary>
        public bool IsDarkTheme
        {
            get { return _isDarkTheme; }
            private set { SetProperty(ref _isDarkTheme, value); }
        }

        /// <summary>
        /// Gets whether this presentation has released its application-theme subscription.
        /// </summary>
        public bool IsDisposed
        {
            get { return _isDisposed; }
        }

        #endregion

        #region Series Properties

        /// <summary>
        /// Gets the completed-session column series.
        /// </summary>
        public ISeries[] CompletedSessionsSeries
        {
            get { return _completedSessionsSeries; }
            private set { SetProperty(ref _completedSessionsSeries, value); }
        }

        /// <summary>
        /// Gets the grouped GOOD and NG selection series.
        /// </summary>
        public ISeries[] GoodVsNgSeries
        {
            get { return _goodVsNgSeries; }
            private set { SetProperty(ref _goodVsNgSeries, value); }
        }

        /// <summary>
        /// Gets the reviewed-versus-pending donut series.
        /// </summary>
        public ISeries[] ReviewCoverageSeries
        {
            get { return _reviewCoverageSeries; }
            private set { SetProperty(ref _reviewCoverageSeries, value); }
        }

        /// <summary>
        /// Gets the reviewed-only accuracy trend series.
        /// </summary>
        public ISeries[] ReviewedAccuracyTrendSeries
        {
            get { return _reviewedAccuracyTrendSeries; }
            private set { SetProperty(ref _reviewedAccuracyTrendSeries, value); }
        }

        /// <summary>
        /// Gets the valid completed-session duration trend series.
        /// </summary>
        public ISeries[] DurationTrendSeries
        {
            get { return _durationTrendSeries; }
            private set { SetProperty(ref _durationTrendSeries, value); }
        }

        #endregion

        #region Axis Properties

        /// <summary>
        /// Gets the completed-session horizontal axes.
        /// </summary>
        public Axis[] CompletedSessionsXAxes
        {
            get { return _completedSessionsXAxes; }
            private set { SetProperty(ref _completedSessionsXAxes, value); }
        }

        /// <summary>
        /// Gets the completed-session vertical axes.
        /// </summary>
        public Axis[] CompletedSessionsYAxes
        {
            get { return _completedSessionsYAxes; }
            private set { SetProperty(ref _completedSessionsYAxes, value); }
        }

        /// <summary>
        /// Gets the GOOD/NG horizontal axes.
        /// </summary>
        public Axis[] SelectionXAxes
        {
            get { return _selectionXAxes; }
            private set { SetProperty(ref _selectionXAxes, value); }
        }

        /// <summary>
        /// Gets the GOOD/NG vertical axes.
        /// </summary>
        public Axis[] SelectionYAxes
        {
            get { return _selectionYAxes; }
            private set { SetProperty(ref _selectionYAxes, value); }
        }

        /// <summary>
        /// Gets the reviewed-accuracy horizontal axes.
        /// </summary>
        public Axis[] ReviewedAccuracyXAxes
        {
            get { return _reviewedAccuracyXAxes; }
            private set { SetProperty(ref _reviewedAccuracyXAxes, value); }
        }

        /// <summary>
        /// Gets the reviewed-accuracy vertical axes.
        /// </summary>
        public Axis[] ReviewedAccuracyYAxes
        {
            get { return _reviewedAccuracyYAxes; }
            private set { SetProperty(ref _reviewedAccuracyYAxes, value); }
        }

        /// <summary>
        /// Gets the duration horizontal axes.
        /// </summary>
        public Axis[] DurationXAxes
        {
            get { return _durationXAxes; }
            private set { SetProperty(ref _durationXAxes, value); }
        }

        /// <summary>
        /// Gets the duration vertical axes.
        /// </summary>
        public Axis[] DurationYAxes
        {
            get { return _durationYAxes; }
            private set { SetProperty(ref _durationYAxes, value); }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Replaces every chart series and axis from one chart-neutral data object.
        /// </summary>
        /// <param name="data">The latest analytics data; null is treated as unavailable.</param>
        public void Update(AnalyticsChartData data)
        {
            if (_isDisposed)
            {
                return;
            }

            _sourceData = CloneData(data);
            _sourcePoints = _sourceData.DailyPoints;

            IsAvailable = _sourceData.IsAvailable;
            RangeText = FormatRange(_sourceData);
            UpdateAvailability(_sourceData);
            ReplaceAxes();
            ReplaceSeries();
        }

        /// <summary>
        /// Recreates chart paints for the requested application theme without appending series.
        /// </summary>
        /// <param name="isDarkTheme">True to use colors designed for dark surfaces.</param>
        public void ApplyTheme(bool isDarkTheme)
        {
            if (_isDisposed)
            {
                return;
            }

            _palette = _themeService.UseTheme(isDarkTheme);
            IsDarkTheme = isDarkTheme;
            ReplaceAxes();
            ReplaceSeries();
        }

        /// <summary>
        /// Formats a bounded duration as hours, minutes, and seconds.
        /// </summary>
        /// <param name="totalSeconds">Duration in seconds.</param>
        /// <returns>A compact non-negative duration such as 1h 02m 03s.</returns>
        public static string FormatDuration(long totalSeconds)
        {
            long safeSeconds = Math.Max(0L, totalSeconds);
            long hours = safeSeconds / 3600L;
            long minutes = safeSeconds % 3600L / 60L;
            long seconds = safeSeconds % 60L;

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}h {1:00}m {2:00}s",
                hours,
                minutes,
                seconds);
        }

        /// <summary>
        /// Gets a reviewed-accuracy tooltip from a safe source-point lookup.
        /// </summary>
        /// <param name="index">The LiveCharts point index.</param>
        /// <returns>A tooltip including the correct and reviewed counts.</returns>
        public string GetAccuracyTooltip(int index)
        {
            ChartPoint point = GetSourcePoint(index);

            if (point == null || !point.ReviewedAccuracyPercent.HasValue)
            {
                return "Reviewed accuracy: N/A";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1:0.##}% ({2} correct / {3} reviewed)",
                GetPointLabel(point),
                point.ReviewedAccuracyPercent.Value,
                Math.Max(0, point.CorrectReviewedAnswers),
                Math.Max(0, point.ReviewedAnswers));
        }

        /// <summary>
        /// Gets a duration tooltip from a safe source-point lookup.
        /// </summary>
        /// <param name="index">The LiveCharts point index.</param>
        /// <returns>A tooltip formatted in hours, minutes, and seconds.</returns>
        public string GetDurationTooltip(int index)
        {
            ChartPoint point = GetSourcePoint(index);

            if (point == null)
            {
                return "Time spent: 0h 00m 00s";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1}",
                GetPointLabel(point),
                FormatDuration(point.DurationSeconds));
        }

        #endregion

        #region Application Theme Coordination

        private void OnApplicationThemeChanged(
            object sender,
            EventArgs eventArgs)
        {
            if (_isDisposed)
            {
                return;
            }

            ApplyTheme(_applicationThemeService.IsDarkTheme);
        }

        #endregion

        #region Series Construction

        private void ReplaceSeries()
        {
            int[] completedValues = _sourcePoints
                .Select(point => Math.Max(0, point.CompletedSessions))
                .ToArray();
            int[] goodValues = _sourcePoints
                .Select(point => Math.Max(0, point.GoodSelections))
                .ToArray();
            int[] ngValues = _sourcePoints
                .Select(point => Math.Max(0, point.NgSelections))
                .ToArray();
            double?[] accuracyValues = _sourcePoints
                .Select(point => point.ReviewedAccuracyPercent.HasValue
                    ? (double?)point.ReviewedAccuracyPercent.Value
                    : null)
                .ToArray();
            double[] durationValues = _sourcePoints
                .Select(point => (double)Math.Max(0L, point.DurationSeconds))
                .ToArray();

            CompletedSessionsSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "Completed sessions",
                    Values = completedValues,
                    Fill = _palette.CreatePaint(_palette.Activity),
                    Stroke = null,
                    MaxBarWidth = 30,
                    Rx = 6,
                    Ry = 6,
                    YToolTipLabelFormatter = point => string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: {1} completed",
                        GetPointLabel(GetSourcePoint(point.Index)),
                        GetCompletedSessions(point.Index))
                }
            };

            GoodVsNgSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "GOOD",
                    Values = goodValues,
                    Fill = _palette.CreatePaint(_palette.Good),
                    Stroke = null,
                    MaxBarWidth = 24,
                    Rx = 5,
                    Ry = 5,
                    YToolTipLabelFormatter = point => string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: {1} GOOD",
                        GetPointLabel(GetSourcePoint(point.Index)),
                        GetGoodSelections(point.Index))
                },
                new ColumnSeries<int>
                {
                    Name = "NG",
                    Values = ngValues,
                    Fill = _palette.CreatePaint(_palette.Ng),
                    Stroke = null,
                    MaxBarWidth = 24,
                    Rx = 5,
                    Ry = 5,
                    YToolTipLabelFormatter = point => string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: {1} NG",
                        GetPointLabel(GetSourcePoint(point.Index)),
                        GetNgSelections(point.Index))
                }
            };

            int reviewed = Math.Max(0, _sourceData.ReviewedAnswers);
            int pending = Math.Max(0, _sourceData.PendingAnswers);
            ReviewCoverageSeries = reviewed + pending == 0
                ? new ISeries[0]
                : new ISeries[]
                {
                    new PieSeries<int>
                    {
                        Name = "Reviewed",
                        Values = new[] { reviewed },
                        Fill = _palette.CreatePaint(_palette.ActivitySecondary),
                        Stroke = null,
                        InnerRadius = 54,
                        ToolTipLabelFormatter = point => string.Format(
                            CultureInfo.InvariantCulture,
                            "Reviewed: {0}",
                            reviewed)
                    },
                    new PieSeries<int>
                    {
                        Name = "Pending",
                        Values = new[] { pending },
                        Fill = _palette.CreatePaint(_palette.Pending),
                        Stroke = null,
                        InnerRadius = 54,
                        ToolTipLabelFormatter = point => string.Format(
                            CultureInfo.InvariantCulture,
                            "Pending: {0}",
                            pending)
                    }
                };

            ReviewedAccuracyTrendSeries = new ISeries[]
            {
                new LineSeries<double?>
                {
                    Name = "Reviewed accuracy",
                    Values = accuracyValues,
                    Fill = _palette.CreatePaint(_palette.ActivitySecondary, 0.14d),
                    Stroke = _palette.CreatePaint(_palette.ActivitySecondary, 1d, 3f),
                    GeometryFill = _palette.CreatePaint(_palette.ActivitySecondary),
                    GeometryStroke = _palette.CreatePaint(_palette.Surface, 1d, 2f),
                    GeometrySize = 9,
                    LineSmoothness = 0.35d,
                    YToolTipLabelFormatter = point => GetAccuracyTooltip(point.Index)
                }
            };

            DurationTrendSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "Time spent",
                    Values = durationValues,
                    Fill = _palette.CreatePaint(_palette.Activity, 0.12d),
                    Stroke = _palette.CreatePaint(_palette.Activity, 1d, 3f),
                    GeometryFill = _palette.CreatePaint(_palette.Activity),
                    GeometryStroke = _palette.CreatePaint(_palette.Surface, 1d, 2f),
                    GeometrySize = 8,
                    LineSmoothness = 0.35d,
                    YToolTipLabelFormatter = point => GetDurationTooltip(point.Index)
                }
            };
        }

        #endregion

        #region Axis Construction

        private void ReplaceAxes()
        {
            string[] labels = _sourcePoints
                .Select(GetPointLabel)
                .ToArray();

            CompletedSessionsXAxes = CreateCategoryAxes(labels);
            CompletedSessionsYAxes = CreateCountAxes("Sessions");
            SelectionXAxes = CreateCategoryAxes(labels);
            SelectionYAxes = CreateCountAxes("Answers");
            ReviewedAccuracyXAxes = CreateCategoryAxes(labels);
            ReviewedAccuracyYAxes = new[]
            {
                CreateValueAxis(
                    "Reviewed accuracy",
                    value => string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:0}%",
                        value),
                    0d,
                    100d,
                    20d)
            };
            DurationXAxes = CreateCategoryAxes(labels);
            DurationYAxes = new[]
            {
                CreateValueAxis(
                    "Time spent",
                    value => FormatDuration((long)Math.Max(0d, value)),
                    0d,
                    null,
                    null)
            };
        }

        private Axis[] CreateCategoryAxes(string[] labels)
        {
            return new[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsPaint = _palette.CreatePaint(_palette.MutedText),
                    SeparatorsPaint = _palette.CreatePaint(_palette.Grid, 0.42d, 1f),
                    TextSize = 12,
                    MinStep = 1d,
                    ForceStepToMin = true,
                    LabelsRotation = labels.Length > 14 ? -25d : 0d
                }
            };
        }

        private Axis[] CreateCountAxes(string name)
        {
            return new[]
            {
                CreateValueAxis(
                    name,
                    value => Math.Max(0d, Math.Round(value))
                        .ToString("0", CultureInfo.InvariantCulture),
                    0d,
                    null,
                    1d)
            };
        }

        private Axis CreateValueAxis(
            string name,
            Func<double, string> labeler,
            double? minimum,
            double? maximum,
            double? minimumStep)
        {
            return new Axis
            {
                Name = name,
                NamePaint = _palette.CreatePaint(_palette.Text),
                LabelsPaint = _palette.CreatePaint(_palette.MutedText),
                SeparatorsPaint = _palette.CreatePaint(_palette.Grid, 0.42d, 1f),
                Labeler = labeler,
                MinLimit = minimum,
                MaxLimit = maximum,
                MinStep = minimumStep ?? 0d,
                ForceStepToMin = minimumStep.HasValue,
                TextSize = 12
            };
        }

        #endregion

        #region Availability Helpers

        private void UpdateAvailability(AnalyticsChartData data)
        {
            HasCompletedSessionsData = data.IsAvailable &&
                _sourcePoints.Any(point => point.CompletedSessions > 0);
            HasSelectionData = data.IsAvailable &&
                _sourcePoints.Any(point =>
                    point.GoodSelections > 0 || point.NgSelections > 0);
            HasReviewCoverageData = data.IsAvailable &&
                Math.Max(0, data.ReviewedAnswers) + Math.Max(0, data.PendingAnswers) > 0;
            HasReviewedAccuracyData = data.IsAvailable &&
                _sourcePoints.Any(point => point.ReviewedAccuracyPercent.HasValue);
            HasDurationData = data.IsAvailable &&
                _sourcePoints.Any(point => point.DurationSeconds > 0L);
            IsEmpty = !HasCompletedSessionsData &&
                !HasSelectionData &&
                !HasReviewCoverageData &&
                !HasReviewedAccuracyData &&
                !HasDurationData;

            if (!data.IsAvailable)
            {
                EmptyStateMessage = BoundStatusMessage(
                    data.UnavailableReason,
                    DefaultUnavailableMessage);
            }
            else
            {
                EmptyStateMessage = DefaultEmptyMessage;
            }
        }

        private static string BoundStatusMessage(
            string message,
            string fallback)
        {
            string value = string.IsNullOrWhiteSpace(message)
                ? fallback
                : message.Trim();

            if (value.Length > MaximumStatusMessageLength)
            {
                value = value.Substring(0, MaximumStatusMessageLength);
            }

            return value;
        }

        #endregion

        #region Source Helpers

        private static AnalyticsChartData CloneData(AnalyticsChartData data)
        {
            if (data == null)
            {
                return new AnalyticsChartData
                {
                    IsAvailable = false,
                    UnavailableReason = DefaultUnavailableMessage,
                    DailyPoints = new List<ChartPoint>()
                };
            }

            List<ChartPoint> points = (data.DailyPoints ?? new List<ChartPoint>())
                .Where(point => point != null)
                .Select(ClonePoint)
                .ToList();

            return new AnalyticsChartData
            {
                RangeStartInclusive = data.RangeStartInclusive,
                RangeEndExclusive = data.RangeEndExclusive,
                DailyPoints = points,
                IsAvailable = data.IsAvailable,
                UnavailableReason = data.UnavailableReason,
                ReviewedAnswers = Math.Max(0, data.ReviewedAnswers),
                CorrectReviewedAnswers = Math.Max(0, data.CorrectReviewedAnswers),
                PendingAnswers = Math.Max(0, data.PendingAnswers)
            };
        }

        private static ChartPoint ClonePoint(ChartPoint source)
        {
            return new ChartPoint
            {
                PeriodStartLocal = source.PeriodStartLocal,
                Label = source.Label,
                CompletedSessions = Math.Max(0, source.CompletedSessions),
                DurationSeconds = Math.Max(0L, source.DurationSeconds),
                GoodSelections = Math.Max(0, source.GoodSelections),
                NgSelections = Math.Max(0, source.NgSelections),
                ReviewedAnswers = Math.Max(0, source.ReviewedAnswers),
                CorrectReviewedAnswers = Math.Max(0, source.CorrectReviewedAnswers),
                PendingAnswers = Math.Max(0, source.PendingAnswers),
                ReviewedAccuracyPercent = source.ReviewedAccuracyPercent
            };
        }

        private ChartPoint GetSourcePoint(int index)
        {
            if (index < 0 || index >= _sourcePoints.Count)
            {
                return null;
            }

            return _sourcePoints[index];
        }

        private int GetCompletedSessions(int index)
        {
            ChartPoint point = GetSourcePoint(index);
            return point == null ? 0 : Math.Max(0, point.CompletedSessions);
        }

        private int GetGoodSelections(int index)
        {
            ChartPoint point = GetSourcePoint(index);
            return point == null ? 0 : Math.Max(0, point.GoodSelections);
        }

        private int GetNgSelections(int index)
        {
            ChartPoint point = GetSourcePoint(index);
            return point == null ? 0 : Math.Max(0, point.NgSelections);
        }

        private static string GetPointLabel(ChartPoint point)
        {
            if (point == null)
            {
                return "Period";
            }

            if (!string.IsNullOrWhiteSpace(point.Label))
            {
                return point.Label.Trim();
            }

            return point.PeriodStartLocal == DateTime.MinValue
                ? "Period"
                : point.PeriodStartLocal.ToString("MMM d", CultureInfo.CurrentCulture);
        }

        private static string FormatRange(AnalyticsChartData data)
        {
            if (data == null ||
                data.RangeStartInclusive == DateTime.MinValue ||
                data.RangeEndExclusive <= data.RangeStartInclusive)
            {
                return string.Empty;
            }

            DateTime lastIncludedDay = data.RangeEndExclusive.AddDays(-1d);

            if (data.RangeStartInclusive.Date == lastIncludedDay.Date)
            {
                return data.RangeStartInclusive.ToString(
                    "MMMM d, yyyy",
                    CultureInfo.CurrentCulture);
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "{0:MMM d, yyyy} \u2013 {1:MMM d, yyyy}",
                data.RangeStartInclusive,
                lastIncludedDay);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases the process-wide application-theme event subscription.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _applicationThemeService.ThemeChanged -=
                OnApplicationThemeChanged;
            OnPropertyChanged(nameof(IsDisposed));
        }

        #endregion
    }
}
