#region Namespaces

using System;
using System.Globalization;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Identifies the date-period definition used to generate a report.
    /// </summary>
    public enum ReportPeriodType
    {
        /// <summary>
        /// A single local calendar day.
        /// </summary>
        Daily,

        /// <summary>
        /// The local Monday-through-Sunday calendar week.
        /// </summary>
        Weekly,

        /// <summary>
        /// The rolling seven local calendar days ending today.
        /// </summary>
        LastSevenDays,

        /// <summary>
        /// The current local calendar month.
        /// </summary>
        Monthly,

        /// <summary>
        /// An administrator-selected inclusive date range.
        /// </summary>
        Custom,

        /// <summary>
        /// All stored dates.
        /// </summary>
        AllDates
    }

    /// <summary>
    /// Defines one report period using parameter-ready half-open boundaries.
    /// </summary>
    public sealed class ReportPeriod
    {
        #region Constructors

        private ReportPeriod(
            ReportPeriodType periodType,
            DateTime? startInclusive,
            DateTime? endExclusive)
        {
            PeriodType = periodType;
            StartInclusive = startInclusive;
            EndExclusive = endExclusive;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the selected period type.
        /// </summary>
        public ReportPeriodType PeriodType
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the optional inclusive local start boundary.
        /// </summary>
        public DateTime? StartInclusive
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the optional exclusive local end boundary.
        /// </summary>
        public DateTime? EndExclusive
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the inclusive end date used by date pickers and labels.
        /// </summary>
        public DateTime? EndInclusive
        {
            get
            {
                return EndExclusive.HasValue
                    ? EndExclusive.Value.AddDays(-1).Date
                    : (DateTime?)null;
            }
        }

        /// <summary>
        /// Gets a concise report-type label.
        /// </summary>
        public string ReportTypeText
        {
            get
            {
                switch (PeriodType)
                {
                    case ReportPeriodType.Daily:
                        return "Daily";
                    case ReportPeriodType.Weekly:
                        return "Weekly";
                    case ReportPeriodType.LastSevenDays:
                        return "Last 7 Days";
                    case ReportPeriodType.Monthly:
                        return "Monthly";
                    case ReportPeriodType.Custom:
                        return "Custom";
                    default:
                        return "All Dates";
                }
            }
        }

        /// <summary>
        /// Gets the administrator-facing selected-range label.
        /// </summary>
        public string DateRangeText
        {
            get
            {
                if (!StartInclusive.HasValue || !EndInclusive.HasValue)
                {
                    return "All Dates";
                }

                if (StartInclusive.Value.Date == EndInclusive.Value.Date)
                {
                    return StartInclusive.Value.ToString(
                        "MMMM d, yyyy",
                        CultureInfo.InvariantCulture);
                }

                return StartInclusive.Value.ToString(
                           "MMMM d, yyyy",
                           CultureInfo.InvariantCulture) +
                       " - " +
                       EndInclusive.Value.ToString(
                           "MMMM d, yyyy",
                           CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Gets a filesystem-safe period token for suggested export names.
        /// </summary>
        public string FileNameToken
        {
            get
            {
                if (!StartInclusive.HasValue || !EndInclusive.HasValue)
                {
                    return "all-dates";
                }

                return StartInclusive.Value.ToString(
                           "yyyyMMdd",
                           CultureInfo.InvariantCulture) +
                       "-" +
                       EndInclusive.Value.ToString(
                           "yyyyMMdd",
                           CultureInfo.InvariantCulture);
            }
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Creates a daily report for the supplied local date.
        /// </summary>
        /// <param name="localDate">A date in the application's local calendar.</param>
        /// <returns>A one-day half-open report period.</returns>
        public static ReportPeriod CreateDaily(DateTime localDate)
        {
            DateTime start = localDate.Date;

            return new ReportPeriod(
                ReportPeriodType.Daily,
                start,
                start.AddDays(1));
        }

        /// <summary>
        /// Creates the Monday-through-following-Monday week containing the supplied date.
        /// </summary>
        /// <param name="localDate">A date in the application's local calendar.</param>
        /// <returns>A true local calendar-week period.</returns>
        public static ReportPeriod CreateWeekly(DateTime localDate)
        {
            DateTime selectedDate = localDate.Date;
            int daysSinceMonday =
                ((int)selectedDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            DateTime start = selectedDate.AddDays(-daysSinceMonday);

            return new ReportPeriod(
                ReportPeriodType.Weekly,
                start,
                start.AddDays(7));
        }

        /// <summary>
        /// Creates the rolling seven-day period ending on the supplied local date.
        /// </summary>
        /// <param name="localDate">The final included local calendar day.</param>
        /// <returns>A rolling seven-day half-open period.</returns>
        public static ReportPeriod CreateLastSevenDays(DateTime localDate)
        {
            DateTime endExclusive = localDate.Date.AddDays(1);

            return new ReportPeriod(
                ReportPeriodType.LastSevenDays,
                endExclusive.AddDays(-7),
                endExclusive);
        }

        /// <summary>
        /// Creates the calendar month containing the supplied local date.
        /// </summary>
        /// <param name="localDate">A date in the requested local month.</param>
        /// <returns>A first-day-through-next-first-day period.</returns>
        public static ReportPeriod CreateMonthly(DateTime localDate)
        {
            DateTime start = new DateTime(
                localDate.Year,
                localDate.Month,
                1);

            return new ReportPeriod(
                ReportPeriodType.Monthly,
                start,
                start.AddMonths(1));
        }

        /// <summary>
        /// Creates a custom range from inclusive local date-picker values.
        /// </summary>
        /// <param name="startInclusive">The first included local day.</param>
        /// <param name="endInclusive">The final included local day.</param>
        /// <returns>A custom half-open report period.</returns>
        public static ReportPeriod CreateCustomInclusive(
            DateTime startInclusive,
            DateTime endInclusive)
        {
            DateTime start = startInclusive.Date;
            DateTime end = endInclusive.Date;

            if (start > end)
            {
                throw new ArgumentException(
                    "The report start date must not be later than the end date.");
            }

            return new ReportPeriod(
                ReportPeriodType.Custom,
                start,
                end.AddDays(1));
        }

        /// <summary>
        /// Creates an unbounded all-dates period.
        /// </summary>
        /// <returns>An all-dates report period.</returns>
        public static ReportPeriod CreateAllDates()
        {
            return new ReportPeriod(
                ReportPeriodType.AllDates,
                null,
                null);
        }

        #endregion
    }
}
