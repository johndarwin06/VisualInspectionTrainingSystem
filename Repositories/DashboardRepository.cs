#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.Repositories
{
    /// <summary>
    /// Provides read-only dashboard data from MySQL.
    /// </summary>
    public class DashboardRepository
    {
        #region Constants

        private const int MaximumRecentSessionLimit = 500;

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
            WHEN UPPER(a.UserAnswer) = 'GOOD' THEN 1
            ELSE 0
        END) AS GoodCount,
        SUM(CASE
            WHEN UPPER(a.UserAnswer) = 'NG' THEN 1
            ELSE 0
        END) AS NgCount,
        SUM(CASE
            WHEN a.CorrectAnswer IS NOT NULL THEN 1
            ELSE 0
        END) AS ReviewedAnswers,
        SUM(CASE
            WHEN a.CorrectAnswer IS NOT NULL
             AND UPPER(a.UserAnswer) = UPPER(a.CorrectAnswer) THEN 1
            ELSE 0
        END) AS CorrectReviewedAnswers,
        SUM(CASE
            WHEN a.CorrectAnswer IS NOT NULL
             AND
             (
                 a.UserAnswer IS NULL OR
                 UPPER(a.UserAnswer) <> UPPER(a.CorrectAnswer)
             ) THEN 1
            ELSE 0
        END) AS WrongReviewedAnswers,
        SUM(CASE
            WHEN a.CorrectAnswer IS NULL THEN 1
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
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads dashboard metrics for the current local calendar day.
        /// </summary>
        /// <returns>Daily dashboard metrics.</returns>
        public virtual DashboardMetrics GetMetrics()
        {
            DateTime dayStart = DateTime.Today;

            return GetMetrics(
                dayStart,
                dayStart.AddDays(1));
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
                DataTable table = _database.ExecuteDataTable(
                    DailyMetricsSql,
                    CreateDateParameter("@DayStart", dayStart),
                    CreateDateParameter("@DayEnd", dayEnd));

                if (table.Rows.Count == 0)
                    return new DashboardMetrics();

                return MapMetrics(table.Rows[0]);
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
                DataTable table = _database.ExecuteDataTable(
                    RecentSessionsSql,
                    new MySqlParameter("@Limit", limit));

                List<DashboardSessionSummary> sessions =
                    new List<DashboardSessionSummary>();

                foreach (DataRow row in table.Rows)
                {
                    sessions.Add(MapSession(row));
                }

                return sessions;
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        #endregion

        #region Mapping

        /// <summary>
        /// Maps one aggregate dashboard row.
        /// </summary>
        /// <param name="row">Aggregate row returned by MySQL.</param>
        /// <returns>Mapped dashboard metrics.</returns>
        private static DashboardMetrics MapMetrics(DataRow row)
        {
            int todaysTraining = ToInt(row["TodaysTraining"]);
            int goodCount = ToInt(row["GoodCount"]);
            int ngCount = ToInt(row["NgCount"]);
            int reviewedAnswers = ToInt(row["ReviewedAnswers"]);
            int correctReviewedAnswers = ToInt(row["CorrectReviewedAnswers"]);
            int wrongReviewedAnswers = ToInt(row["WrongReviewedAnswers"]);
            decimal? averageReviewedAccuracy =
                ToNullableDecimal(row["AverageReviewedAccuracy"]);

            return new DashboardMetrics
            {
                TodaysTraining = todaysTraining,
                AverageReviewedAccuracy = averageReviewedAccuracy,
                TimeSpentSeconds = ToLong(row["TimeSpentSeconds"]),
                GoodCount = goodCount,
                NgCount = ngCount,
                ReviewedAnswers = reviewedAnswers,
                CorrectReviewedAnswers = correctReviewedAnswers,
                WrongReviewedAnswers = wrongReviewedAnswers,
                PendingAnswers = ToInt(row["PendingAnswers"]),
                TotalSessions = todaysTraining,
                TotalAnswers = goodCount + ngCount,
                ActiveTrainees = ToInt(row["ActiveTrainees"]),
                AverageAccuracy = averageReviewedAccuracy ?? 0,
                LatestSessionTime = ToNullableDate(row["LatestSessionTime"])
            };
        }

        /// <summary>
        /// Maps one recent session row.
        /// </summary>
        /// <param name="row">Session row returned by MySQL.</param>
        /// <returns>Mapped recent session summary.</returns>
        private static DashboardSessionSummary MapSession(DataRow row)
        {
            return new DashboardSessionSummary
            {
                SessionID = ToRequiredInt(row["SessionID"], "SessionID"),
                EmployeeNo = ToRequiredString(row["EmployeeNo"], "EmployeeNo"),
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
        /// Validates a half-open dashboard range.
        /// </summary>
        /// <param name="dayStart">Inclusive start boundary.</param>
        /// <param name="dayEnd">Exclusive end boundary.</param>
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
        /// Validates the requested recent-session limit.
        /// </summary>
        /// <param name="limit">Requested row limit.</param>
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

        #region Parameter Helpers

        /// <summary>
        /// Creates a strongly typed MySQL date parameter.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="value">Local date boundary.</param>
        /// <returns>Configured MySQL parameter.</returns>
        private static MySqlParameter CreateDateParameter(
            string name,
            DateTime value)
        {
            return new MySqlParameter(name, MySqlDbType.DateTime)
            {
                Value = value
            };
        }

        #endregion

        #region Conversion Helpers

        /// <summary>
        /// Converts a nullable numeric value to an integer.
        /// </summary>
        private static int ToInt(object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Converts a nullable numeric value to a long integer.
        /// </summary>
        private static long ToLong(object value)
        {
            if (value == null ||
                value == DBNull.Value)
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
            if (value == null ||
                value == DBNull.Value)
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
            if (value == null ||
                value == DBNull.Value)
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
            if (value == null ||
                value == DBNull.Value)
            {
                return null;
            }

            return Convert.ToDecimal(value);
        }

        /// <summary>
        /// Converts a required string value.
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
        /// Converts a required DateTime value.
        /// </summary>
        private static DateTime ToRequiredDate(
            object value,
            string columnName)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                throw new InvalidOperationException(
                    columnName + " is required.");
            }

            return Convert.ToDateTime(value);
        }

        /// <summary>
        /// Converts a nullable DateTime value.
        /// </summary>
        private static DateTime? ToNullableDate(object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return null;
            }

            return Convert.ToDateTime(value);
        }

        #endregion
    }
}
