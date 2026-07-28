#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.Repositories
{
    /// <summary>
    /// Provides internally consistent, read-only administrator dashboard data.
    /// </summary>
    public class DashboardRepository
    {
        #region Constants

        private const int DefaultTrendDayCount = 7;
        private const int MaximumRecentSessionLimit = 500;
        private const int MaximumTrendDayCount = 30;

        private const string DailyMetricsSql = @"
SELECT
    IFNULL(sessionTotals.TodaysTraining, 0) AS TodaysTraining,
    IFNULL(sessionTotals.TimeSpentSeconds, 0) AS TimeSpentSeconds,
    IFNULL(sessionTotals.ActiveTrainees, 0) AS ActiveTrainees,
    sessionTotals.LatestSessionTime AS LatestSessionTime,
    IFNULL(answerTotals.GoodCount, 0) AS GoodCount,
    IFNULL(answerTotals.NgCount, 0) AS NgCount,
    IFNULL(answerTotals.ReviewedAnswers, 0) AS ReviewedAnswers,
    IFNULL(answerTotals.CorrectReviewedAnswers, 0) AS CorrectReviewedAnswers,
    IFNULL(answerTotals.WrongReviewedAnswers, 0) AS WrongReviewedAnswers,
    IFNULL(answerTotals.PendingAnswers, 0) AS PendingAnswers,
    CASE
        WHEN IFNULL(answerTotals.ReviewedAnswers, 0) = 0 THEN NULL
        ELSE ROUND(
            answerTotals.CorrectReviewedAnswers * 100.0 /
            answerTotals.ReviewedAnswers,
            2)
    END AS AverageReviewedAccuracy
FROM
(
    SELECT
        SUM(CASE
            WHEN EndTime IS NOT NULL THEN 1
            ELSE 0
        END) AS TodaysTraining,
        SUM(CASE
            WHEN EndTime IS NOT NULL AND EndTime >= StartTime
                THEN TIMESTAMPDIFF(SECOND, StartTime, EndTime)
            ELSE 0
        END) AS TimeSpentSeconds,
        COUNT(DISTINCT CASE
            WHEN EndTime IS NOT NULL THEN EmployeeNo
            ELSE NULL
        END) AS ActiveTrainees,
        MAX(CASE
            WHEN EndTime IS NOT NULL THEN StartTime
            ELSE NULL
        END) AS LatestSessionTime
    FROM tbl_training_session
    WHERE StartTime >= @DayStart
      AND StartTime < @DayEnd
) sessionTotals
CROSS JOIN
(
    SELECT
        SUM(CASE
            WHEN UPPER(TRIM(a.UserAnswer)) = 'GOOD' THEN 1
            ELSE 0
        END) AS GoodCount,
        SUM(CASE
            WHEN UPPER(TRIM(a.UserAnswer)) = 'NG' THEN 1
            ELSE 0
        END) AS NgCount,
        SUM(CASE
            WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
            ELSE 0
        END) AS ReviewedAnswers,
        SUM(CASE
            WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG')
             AND UPPER(TRIM(a.UserAnswer)) IN ('GOOD', 'NG')
             AND UPPER(TRIM(a.UserAnswer)) = UPPER(TRIM(a.CorrectAnswer)) THEN 1
            ELSE 0
        END) AS CorrectReviewedAnswers,
        SUM(CASE
            WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG')
             AND
             (
                 a.UserAnswer IS NULL OR
                 UPPER(TRIM(a.UserAnswer)) NOT IN ('GOOD', 'NG') OR
                 UPPER(TRIM(a.UserAnswer)) <> UPPER(TRIM(a.CorrectAnswer))
             ) THEN 1
            ELSE 0
        END) AS WrongReviewedAnswers,
        SUM(CASE
            WHEN a.CorrectAnswer IS NULL
              OR UPPER(TRIM(a.CorrectAnswer)) NOT IN ('GOOD', 'NG') THEN 1
            ELSE 0
        END) AS PendingAnswers
    FROM tbl_quiz_answer a
    INNER JOIN tbl_training_session s
        ON s.SessionID = a.SessionID
    WHERE s.StartTime >= @DayStart
      AND s.StartTime < @DayEnd
) answerTotals;";

        private const string RecentSessionsSql = @"
SELECT
    SessionID,
    EmployeeNo,
    StartTime,
    EndTime,
    TotalQuestions,
    CorrectAnswers,
    WrongAnswers,
    Accuracy
FROM tbl_training_session
ORDER BY StartTime DESC, SessionID DESC
LIMIT @Limit;";

        private const string DailySessionTrendSql = @"
SELECT
    DATE(StartTime) AS ActivityDate,
    SUM(CASE
        WHEN EndTime IS NOT NULL THEN 1
        ELSE 0
    END) AS CompletedSessions,
    SUM(CASE
        WHEN EndTime IS NOT NULL AND EndTime >= StartTime
            THEN TIMESTAMPDIFF(SECOND, StartTime, EndTime)
        ELSE 0
    END) AS DurationSeconds
FROM tbl_training_session
WHERE StartTime >= @TrendStart
  AND StartTime < @TrendEnd
GROUP BY DATE(StartTime)
ORDER BY ActivityDate ASC;";

        private const string DailyAnswerTrendSql = @"
SELECT
    DATE(s.StartTime) AS ActivityDate,
    SUM(CASE
        WHEN UPPER(TRIM(a.UserAnswer)) = 'GOOD' THEN 1
        ELSE 0
    END) AS GoodSelections,
    SUM(CASE
        WHEN UPPER(TRIM(a.UserAnswer)) = 'NG' THEN 1
        ELSE 0
    END) AS NgSelections,
    SUM(CASE
        WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
        ELSE 0
    END) AS ReviewedAnswers,
    SUM(CASE
        WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG')
         AND UPPER(TRIM(a.UserAnswer)) IN ('GOOD', 'NG')
         AND UPPER(TRIM(a.UserAnswer)) = UPPER(TRIM(a.CorrectAnswer)) THEN 1
        ELSE 0
    END) AS CorrectReviewedAnswers,
    SUM(CASE
        WHEN a.CorrectAnswer IS NULL
          OR UPPER(TRIM(a.CorrectAnswer)) NOT IN ('GOOD', 'NG') THEN 1
        ELSE 0
    END) AS PendingAnswers
FROM tbl_quiz_answer a
INNER JOIN tbl_training_session s
    ON s.SessionID = a.SessionID
WHERE s.StartTime >= @TrendStart
  AND s.StartTime < @TrendEnd
GROUP BY DATE(s.StartTime)
ORDER BY ActivityDate ASC;";

        #endregion

        #region Fields

        private readonly MySqlService _database;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the dashboard repository.
        /// </summary>
        public DashboardRepository()
            : this(new MySqlService())
        {
        }

        /// <summary>
        /// Initializes the dashboard repository with an existing database service.
        /// </summary>
        /// <param name="database">The database service used for read-only queries.</param>
        internal DashboardRepository(MySqlService database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            _database = database;
        }

        #endregion

        #region Snapshot Queries

        /// <summary>
        /// Loads today's metrics, recent rows, and a seven-day daily trend through one consistent read.
        /// </summary>
        /// <param name="recentSessionLimit">Maximum number of recent sessions to return.</param>
        /// <returns>An internally consistent dashboard snapshot.</returns>
        public virtual DashboardSnapshot GetSnapshot(int recentSessionLimit)
        {
            DateTime dayStart = DateTime.Today;
            DateTime dayEnd = dayStart.AddDays(1);

            return GetSnapshot(
                dayStart,
                dayEnd,
                dayStart.AddDays(-(DefaultTrendDayCount - 1)),
                dayEnd,
                recentSessionLimit);
        }

        /// <summary>
        /// Loads a coherent dashboard snapshot through one repeatable-read transaction.
        /// </summary>
        /// <param name="dayStart">Inclusive local start for headline metrics.</param>
        /// <param name="dayEnd">Exclusive local end for headline metrics.</param>
        /// <param name="trendStart">Inclusive local start for daily chart data.</param>
        /// <param name="trendEnd">Exclusive local end for daily chart data.</param>
        /// <param name="recentSessionLimit">Maximum number of recent sessions to return.</param>
        /// <returns>An internally consistent dashboard snapshot.</returns>
        public virtual DashboardSnapshot GetSnapshot(
            DateTime dayStart,
            DateTime dayEnd,
            DateTime trendStart,
            DateTime trendEnd,
            int recentSessionLimit)
        {
            ValidateDayRange(dayStart, dayEnd);
            ValidateTrendRange(trendStart, trendEnd);
            ValidateLimit(recentSessionLimit);

            MySqlTransaction transaction = null;
            bool transactionCompleted = false;

            try
            {
                _database.OpenConnection();

                MySqlConnection connection = _database.GetConnection();

                transaction = connection.BeginTransaction(
                    IsolationLevel.RepeatableRead);

                DashboardMetrics metrics = LoadMetrics(
                    connection,
                    transaction,
                    dayStart,
                    dayEnd);
                List<DashboardSessionSummary> recentSessions =
                    LoadRecentSessions(
                        connection,
                        transaction,
                        recentSessionLimit);
                AnalyticsChartData chartData = LoadChartData(
                    connection,
                    transaction,
                    trendStart,
                    trendEnd);

                DashboardSnapshot snapshot = new DashboardSnapshot
                {
                    DayStartInclusive = dayStart,
                    DayEndExclusive = dayEnd,
                    Metrics = metrics,
                    RecentSessions = recentSessions,
                    ChartData = chartData,
                    GeneratedAtLocal = DateTime.Now
                };

                transaction.Commit();
                transactionCompleted = true;

                return snapshot;
            }
            catch (Exception exception)
            {
                if (!transactionCompleted)
                {
                    RollbackReadTransaction(transaction, exception);
                }

                throw;
            }
            finally
            {
                try
                {
                    if (transaction != null)
                    {
                        transaction.Dispose();
                    }
                }
                finally
                {
                    _database.CloseConnection();
                }
            }
        }

        #endregion

        #region Compatibility Queries

        /// <summary>
        /// Loads dashboard metrics for the current local calendar day.
        /// </summary>
        /// <returns>Daily dashboard metrics.</returns>
        public virtual DashboardMetrics GetMetrics()
        {
            DateTime dayStart = DateTime.Today;

            return GetMetrics(dayStart, dayStart.AddDays(1));
        }

        /// <summary>
        /// Loads dashboard metrics within a parameterized half-open local time range.
        /// </summary>
        /// <param name="dayStart">Inclusive local start boundary.</param>
        /// <param name="dayEnd">Exclusive local end boundary.</param>
        /// <returns>Dashboard metrics for the requested range.</returns>
        public virtual DashboardMetrics GetMetrics(
            DateTime dayStart,
            DateTime dayEnd)
        {
            ValidateDayRange(dayStart, dayEnd);

            try
            {
                _database.OpenConnection();

                return LoadMetrics(
                    _database.GetConnection(),
                    null,
                    dayStart,
                    dayEnd);
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Loads the most recent training sessions in deterministic order.
        /// </summary>
        /// <param name="limit">Maximum number of sessions to return.</param>
        /// <returns>Recent session summaries.</returns>
        public virtual List<DashboardSessionSummary> GetRecentSessions(int limit)
        {
            ValidateLimit(limit);

            try
            {
                _database.OpenConnection();

                return LoadRecentSessions(
                    _database.GetConnection(),
                    null,
                    limit);
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        #endregion

        #region Query Helpers

        /// <summary>
        /// Loads headline metrics through the caller-owned read scope.
        /// </summary>
        private static DashboardMetrics LoadMetrics(
            MySqlConnection connection,
            MySqlTransaction transaction,
            DateTime dayStart,
            DateTime dayEnd)
        {
            DataTable table = ExecuteDataTable(
                DailyMetricsSql,
                connection,
                transaction,
                CreateDateParameter("@DayStart", dayStart),
                CreateDateParameter("@DayEnd", dayEnd));

            if (table.Rows.Count == 0)
            {
                return new DashboardMetrics();
            }

            return MapMetrics(table.Rows[0]);
        }

        /// <summary>
        /// Loads deterministic recent rows through the caller-owned read scope.
        /// </summary>
        private static List<DashboardSessionSummary> LoadRecentSessions(
            MySqlConnection connection,
            MySqlTransaction transaction,
            int limit)
        {
            DataTable table = ExecuteDataTable(
                RecentSessionsSql,
                connection,
                transaction,
                new MySqlParameter("@Limit", MySqlDbType.Int32)
                {
                    Value = limit
                });

            List<DashboardSessionSummary> sessions =
                new List<DashboardSessionSummary>(table.Rows.Count);

            foreach (DataRow row in table.Rows)
            {
                sessions.Add(MapSession(row));
            }

            return sessions;
        }

        /// <summary>
        /// Loads separated session and answer aggregates and merges zero-filled days.
        /// </summary>
        private static AnalyticsChartData LoadChartData(
            MySqlConnection connection,
            MySqlTransaction transaction,
            DateTime trendStart,
            DateTime trendEnd)
        {
            DataTable sessionTable = ExecuteDataTable(
                DailySessionTrendSql,
                connection,
                transaction,
                CreateDateParameter("@TrendStart", trendStart),
                CreateDateParameter("@TrendEnd", trendEnd));
            DataTable answerTable = ExecuteDataTable(
                DailyAnswerTrendSql,
                connection,
                transaction,
                CreateDateParameter("@TrendStart", trendStart),
                CreateDateParameter("@TrendEnd", trendEnd));

            AnalyticsChartData chartData = CreateEmptyChartData(
                trendStart,
                trendEnd);
            Dictionary<DateTime, ChartPoint> pointsByDate =
                IndexPointsByDate(chartData.DailyPoints);

            ApplySessionTrendRows(sessionTable, pointsByDate);
            ApplyAnswerTrendRows(answerTable, pointsByDate);
            UpdateChartAggregates(chartData);

            return chartData;
        }

        /// <summary>
        /// Executes one read command within a caller-owned connection and transaction.
        /// </summary>
        private static DataTable ExecuteDataTable(
            string sql,
            MySqlConnection connection,
            MySqlTransaction transaction,
            params MySqlParameter[] parameters)
        {
            using (MySqlCommand command = new MySqlCommand(
                sql,
                connection,
                transaction))
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }

                using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
                {
                    DataTable table = new DataTable();

                    adapter.Fill(table);

                    return table;
                }
            }
        }

        /// <summary>
        /// Rolls back an incomplete read transaction and preserves rollback failures.
        /// </summary>
        private static void RollbackReadTransaction(
            MySqlTransaction transaction,
            Exception originalException)
        {
            if (transaction == null)
            {
                return;
            }

            try
            {
                transaction.Rollback();
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Failed to end the dashboard read transaction safely.",
                    new AggregateException(
                        originalException,
                        rollbackException));
            }
        }

        #endregion

        #region Chart Mapping

        /// <summary>
        /// Creates exact zero-filled points for every local day in the range.
        /// </summary>
        private static AnalyticsChartData CreateEmptyChartData(
            DateTime trendStart,
            DateTime trendEnd)
        {
            AnalyticsChartData chartData = new AnalyticsChartData
            {
                RangeStartInclusive = trendStart,
                RangeEndExclusive = trendEnd
            };
            int dayCount = Convert.ToInt32(
                (trendEnd - trendStart).TotalDays);
            string labelFormat = dayCount <= DefaultTrendDayCount
                ? "ddd"
                : "MMM d";

            for (DateTime day = trendStart;
                 day < trendEnd;
                 day = day.AddDays(1))
            {
                chartData.DailyPoints.Add(new ChartPoint
                {
                    PeriodStartLocal = day,
                    Label = day.ToString(
                        labelFormat,
                        CultureInfo.CurrentCulture)
                });
            }

            return chartData;
        }

        /// <summary>
        /// Indexes zero-filled chart points by normalized local date.
        /// </summary>
        private static Dictionary<DateTime, ChartPoint> IndexPointsByDate(
            IEnumerable<ChartPoint> points)
        {
            Dictionary<DateTime, ChartPoint> pointsByDate =
                new Dictionary<DateTime, ChartPoint>();

            foreach (ChartPoint point in points)
            {
                pointsByDate.Add(
                    point.PeriodStartLocal.Date,
                    point);
            }

            return pointsByDate;
        }

        /// <summary>
        /// Applies daily completed-session aggregates without joining answer rows.
        /// </summary>
        private static void ApplySessionTrendRows(
            DataTable table,
            IDictionary<DateTime, ChartPoint> pointsByDate)
        {
            foreach (DataRow row in table.Rows)
            {
                ChartPoint point;
                DateTime activityDate = ToRequiredDate(
                    row["ActivityDate"],
                    "ActivityDate").Date;

                if (!pointsByDate.TryGetValue(activityDate, out point))
                {
                    continue;
                }

                point.CompletedSessions = ToInt(row["CompletedSessions"]);
                point.DurationSeconds = ToLong(row["DurationSeconds"]);
            }
        }

        /// <summary>
        /// Applies daily answer aggregates using supported GOOD and NG semantics.
        /// </summary>
        private static void ApplyAnswerTrendRows(
            DataTable table,
            IDictionary<DateTime, ChartPoint> pointsByDate)
        {
            foreach (DataRow row in table.Rows)
            {
                ChartPoint point;
                DateTime activityDate = ToRequiredDate(
                    row["ActivityDate"],
                    "ActivityDate").Date;

                if (!pointsByDate.TryGetValue(activityDate, out point))
                {
                    continue;
                }

                point.GoodSelections = ToInt(row["GoodSelections"]);
                point.NgSelections = ToInt(row["NgSelections"]);
                point.ReviewedAnswers = ToInt(row["ReviewedAnswers"]);
                point.CorrectReviewedAnswers = ToInt(
                    row["CorrectReviewedAnswers"]);
                point.PendingAnswers = ToInt(row["PendingAnswers"]);
                point.ReviewedAccuracyPercent = CalculateAccuracy(
                    point.CorrectReviewedAnswers,
                    point.ReviewedAnswers);
            }
        }

        /// <summary>
        /// Updates aggregate review values after all daily points are mapped.
        /// </summary>
        private static void UpdateChartAggregates(
            AnalyticsChartData chartData)
        {
            foreach (ChartPoint point in chartData.DailyPoints)
            {
                chartData.ReviewedAnswers += point.ReviewedAnswers;
                chartData.CorrectReviewedAnswers +=
                    point.CorrectReviewedAnswers;
                chartData.PendingAnswers += point.PendingAnswers;
            }
        }

        #endregion

        #region Row Mapping

        /// <summary>
        /// Maps one aggregate dashboard row.
        /// </summary>
        private static DashboardMetrics MapMetrics(DataRow row)
        {
            int todaysTraining = ToInt(row["TodaysTraining"]);
            int goodCount = ToInt(row["GoodCount"]);
            int ngCount = ToInt(row["NgCount"]);
            decimal? averageReviewedAccuracy =
                ToNullableDecimal(row["AverageReviewedAccuracy"]);

            return new DashboardMetrics
            {
                TodaysTraining = todaysTraining,
                AverageReviewedAccuracy = averageReviewedAccuracy,
                TimeSpentSeconds = ToLong(row["TimeSpentSeconds"]),
                GoodCount = goodCount,
                NgCount = ngCount,
                ReviewedAnswers = ToInt(row["ReviewedAnswers"]),
                CorrectReviewedAnswers = ToInt(
                    row["CorrectReviewedAnswers"]),
                WrongReviewedAnswers = ToInt(row["WrongReviewedAnswers"]),
                PendingAnswers = ToInt(row["PendingAnswers"]),
                TotalSessions = todaysTraining,
                TotalAnswers = goodCount + ngCount,
                ActiveTrainees = ToInt(row["ActiveTrainees"]),
                AverageAccuracy = averageReviewedAccuracy ?? 0,
                LatestSessionTime = ToNullableDate(
                    row["LatestSessionTime"])
            };
        }

        /// <summary>
        /// Maps one recent session row.
        /// </summary>
        private static DashboardSessionSummary MapSession(DataRow row)
        {
            return new DashboardSessionSummary
            {
                SessionID = ToRequiredInt(row["SessionID"], "SessionID"),
                EmployeeNo = ToRequiredString(
                    row["EmployeeNo"],
                    "EmployeeNo"),
                StartTime = ToRequiredDate(row["StartTime"], "StartTime"),
                EndTime = ToNullableDate(row["EndTime"]),
                TotalQuestions = ToInt(row["TotalQuestions"]),
                CorrectAnswers = ToInt(row["CorrectAnswers"]),
                WrongAnswers = ToInt(row["WrongAnswers"]),
                Accuracy = ToDecimal(row["Accuracy"])
            };
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates a half-open headline metric range.
        /// </summary>
        private static void ValidateDayRange(
            DateTime dayStart,
            DateTime dayEnd)
        {
            if (dayEnd <= dayStart)
            {
                throw new ArgumentException(
                    "Dashboard day end must be later than day start.",
                    nameof(dayEnd));
            }
        }

        /// <summary>
        /// Validates a bounded, whole-day chart range.
        /// </summary>
        private static void ValidateTrendRange(
            DateTime trendStart,
            DateTime trendEnd)
        {
            if (trendEnd <= trendStart)
            {
                throw new ArgumentException(
                    "Dashboard trend end must be later than trend start.",
                    nameof(trendEnd));
            }

            if (trendStart.TimeOfDay != TimeSpan.Zero ||
                trendEnd.TimeOfDay != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Dashboard trend boundaries must start at local midnight.");
            }

            double dayCount = (trendEnd - trendStart).TotalDays;

            if (dayCount != DefaultTrendDayCount &&
                dayCount != MaximumTrendDayCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(trendEnd),
                    "Dashboard trends must contain exactly 7 or 30 local days.");
            }
        }

        /// <summary>
        /// Validates the requested recent-session limit.
        /// </summary>
        private static void ValidateLimit(int limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(limit),
                    "Recent session limit must be greater than zero.");
            }

            if (limit > MaximumRecentSessionLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(limit),
                    "Recent session limit is too large.");
            }
        }

        #endregion

        #region Value Helpers

        /// <summary>
        /// Creates a strongly typed MySQL local date parameter.
        /// </summary>
        private static MySqlParameter CreateDateParameter(
            string name,
            DateTime value)
        {
            return new MySqlParameter(name, MySqlDbType.DateTime)
            {
                Value = value
            };
        }

        /// <summary>
        /// Calculates nullable reviewed-only accuracy.
        /// </summary>
        private static decimal? CalculateAccuracy(
            int correctReviewedAnswers,
            int reviewedAnswers)
        {
            if (reviewedAnswers <= 0)
            {
                return null;
            }

            return Math.Round(
                correctReviewedAnswers * 100m / reviewedAnswers,
                2,
                MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Converts a nullable numeric value to an integer.
        /// </summary>
        private static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Converts a nullable numeric value to a non-negative long integer.
        /// </summary>
        private static long ToLong(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            long converted = Convert.ToInt64(value);

            return converted < 0 ? 0 : converted;
        }

        /// <summary>
        /// Converts a required numeric value to an integer.
        /// </summary>
        private static int ToRequiredInt(
            object value,
            string columnName)
        {
            if (value == null || value == DBNull.Value)
            {
                throw new InvalidOperationException(
                    columnName + " is required.");
            }

            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Converts a nullable numeric value to a decimal.
        /// </summary>
        private static decimal ToDecimal(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToDecimal(value);
        }

        /// <summary>
        /// Converts a nullable numeric value to a nullable decimal.
        /// </summary>
        private static decimal? ToNullableDecimal(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            return Convert.ToDecimal(value);
        }

        /// <summary>
        /// Converts a required non-empty string value.
        /// </summary>
        private static string ToRequiredString(
            object value,
            string columnName)
        {
            if (value == null ||
                value == DBNull.Value ||
                string.IsNullOrWhiteSpace(value.ToString()))
            {
                throw new InvalidOperationException(
                    columnName + " is required.");
            }

            return value.ToString();
        }

        /// <summary>
        /// Converts a required date value.
        /// </summary>
        private static DateTime ToRequiredDate(
            object value,
            string columnName)
        {
            if (value == null || value == DBNull.Value)
            {
                throw new InvalidOperationException(
                    columnName + " is required.");
            }

            return Convert.ToDateTime(value);
        }

        /// <summary>
        /// Converts a nullable date value.
        /// </summary>
        private static DateTime? ToNullableDate(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            return Convert.ToDateTime(value);
        }

        #endregion
    }
}
