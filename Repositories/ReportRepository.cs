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
    /// Provides read-only report snapshots from the existing MySQL tables.
    /// </summary>
    public class ReportRepository : IReportRepository
    {
        #region Constants

        /// <summary>
        /// Maximum session rows shown in the interactive Reports table.
        /// </summary>
        public const int InteractiveDisplayLimit = 500;

        /// <summary>
        /// Maximum session rows permitted in one generated export.
        /// </summary>
        public const int MaximumExportSessionCount = 10000;

        private const string SummarySql = @"
SELECT
    COUNT(*) AS SessionCount,
    IFNULL(SUM(CASE WHEN s.EndTime IS NOT NULL THEN 1 ELSE 0 END), 0) AS CompletedSessionCount,
    IFNULL(SUM(CASE WHEN s.EndTime IS NULL THEN 1 ELSE 0 END), 0) AS OpenSessionCount,
    IFNULL(SUM(s.TotalQuestions), 0) AS TotalQuestions,
    IFNULL(SUM(IFNULL(answerTotals.CorrectAnswers, 0)), 0) AS CorrectAnswers,
    IFNULL(SUM(IFNULL(answerTotals.WrongAnswers, 0)), 0) AS WrongAnswers,
    IFNULL(SUM(IFNULL(answerTotals.PendingAnswers, 0)), 0) AS PendingAnswers,
    IFNULL(SUM(IFNULL(answerTotals.ReviewedAnswers, 0)), 0) AS ReviewedAnswers,
    COUNT(DISTINCT s.EmployeeNo) AS TraineeCount,
    CASE
        WHEN IFNULL(SUM(IFNULL(answerTotals.ReviewedAnswers, 0)), 0) = 0 THEN NULL
        ELSE ROUND(
            IFNULL(SUM(IFNULL(answerTotals.CorrectAnswers, 0)), 0) * 100.0 /
            SUM(IFNULL(answerTotals.ReviewedAnswers, 0)),
            2)
    END AS AverageReviewedAccuracy,
    MIN(s.StartTime) AS FirstSessionTime,
    MAX(s.StartTime) AS LastSessionTime
FROM tbl_training_session s
LEFT JOIN
(
    SELECT
        SessionID,
        SUM(CASE
            WHEN UPPER(TRIM(CorrectAnswer)) IN ('GOOD', 'NG')
             AND UPPER(TRIM(UserAnswer)) IN ('GOOD', 'NG')
             AND UPPER(TRIM(UserAnswer)) = UPPER(TRIM(CorrectAnswer)) THEN 1
            ELSE 0
        END) AS CorrectAnswers,
        SUM(CASE
            WHEN UPPER(TRIM(CorrectAnswer)) IN ('GOOD', 'NG')
             AND
             (
                 UserAnswer IS NULL OR
                 UPPER(TRIM(UserAnswer)) NOT IN ('GOOD', 'NG') OR
                 UPPER(TRIM(UserAnswer)) <> UPPER(TRIM(CorrectAnswer))
             ) THEN 1
            ELSE 0
        END) AS WrongAnswers,
        SUM(CASE
            WHEN CorrectAnswer IS NULL
              OR UPPER(TRIM(CorrectAnswer)) NOT IN ('GOOD', 'NG') THEN 1
            ELSE 0
        END) AS PendingAnswers,
        SUM(CASE
            WHEN UPPER(TRIM(CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
            ELSE 0
        END) AS ReviewedAnswers
    FROM tbl_quiz_answer
    GROUP BY SessionID
) answerTotals
    ON answerTotals.SessionID = s.SessionID
WHERE (@StartDate IS NULL OR s.StartTime >= @StartDate)
  AND (@EndDate IS NULL OR s.StartTime < @EndDate);";

        private const string SessionsSql = @"
SELECT
    s.SessionID,
    s.EmployeeNo,
    IFNULL(u.FullName, '') AS FullName,
    IFNULL(u.Department, '') AS Department,
    s.StartTime,
    s.EndTime,
    s.TotalQuestions,
    IFNULL(SUM(CASE
        WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG')
         AND UPPER(TRIM(a.UserAnswer)) IN ('GOOD', 'NG')
         AND UPPER(TRIM(a.UserAnswer)) = UPPER(TRIM(a.CorrectAnswer)) THEN 1
        ELSE 0
    END), 0) AS CorrectAnswers,
    IFNULL(SUM(CASE
        WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG')
         AND
         (
             a.UserAnswer IS NULL OR
             UPPER(TRIM(a.UserAnswer)) NOT IN ('GOOD', 'NG') OR
             UPPER(TRIM(a.UserAnswer)) <> UPPER(TRIM(a.CorrectAnswer))
         ) THEN 1
        ELSE 0
    END), 0) AS WrongAnswers,
    IFNULL(SUM(CASE
        WHEN a.AnswerID IS NOT NULL
         AND
         (
             a.CorrectAnswer IS NULL OR
             UPPER(TRIM(a.CorrectAnswer)) NOT IN ('GOOD', 'NG')
         ) THEN 1
        ELSE 0
    END), 0) AS PendingAnswers,
    IFNULL(SUM(CASE
        WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
        ELSE 0
    END), 0) AS ReviewedAnswers,
    CASE
        WHEN IFNULL(SUM(CASE
            WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
            ELSE 0
        END), 0) = 0 THEN NULL
        ELSE ROUND(
            IFNULL(SUM(CASE
                WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG')
                 AND UPPER(TRIM(a.UserAnswer)) IN ('GOOD', 'NG')
                 AND UPPER(TRIM(a.UserAnswer)) = UPPER(TRIM(a.CorrectAnswer)) THEN 1
                ELSE 0
            END), 0) * 100.0 /
            SUM(CASE
                WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
                ELSE 0
            END),
            2)
    END AS ReviewedAccuracy
FROM tbl_training_session s
LEFT JOIN tbl_users u
    ON u.EmployeeNo = s.EmployeeNo
LEFT JOIN tbl_quiz_answer a
    ON a.SessionID = s.SessionID
WHERE (@StartDate IS NULL OR s.StartTime >= @StartDate)
  AND (@EndDate IS NULL OR s.StartTime < @EndDate)
GROUP BY
    s.SessionID,
    s.EmployeeNo,
    u.FullName,
    u.Department,
    s.StartTime,
    s.EndTime,
    s.TotalQuestions
ORDER BY s.StartTime DESC, s.SessionID DESC
LIMIT @Limit;";

        #endregion

        #region Fields

        private readonly MySqlService _database;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the report repository with the configured database service.
        /// </summary>
        public ReportRepository()
            : this(new MySqlService())
        {
        }

        /// <summary>
        /// Initializes the report repository with a supplied database service.
        /// </summary>
        /// <param name="database">The database service used for read-only queries.</param>
        internal ReportRepository(MySqlService database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            _database = database;
        }

        #endregion

        #region Snapshot Loading

        /// <summary>
        /// Loads an interactive report snapshot bounded to the disclosed display limit.
        /// </summary>
        /// <param name="period">The selected half-open report period.</param>
        /// <returns>The display report snapshot.</returns>
        public ReportSnapshot GetDisplaySnapshot(ReportPeriod period)
        {
            ValidatePeriod(period);

            ReportSummary summary = GetSummary(
                period.StartInclusive,
                period.EndExclusive);
            List<ReportSessionRow> sessions = LoadSessions(
                period.StartInclusive,
                period.EndExclusive,
                InteractiveDisplayLimit);

            return new ReportSnapshot
            {
                Period = period,
                Summary = summary,
                Sessions = sessions,
                GeneratedAtLocal = DateTime.Now,
                IsDisplayLimited = summary.SessionCount > sessions.Count
            };
        }

        /// <summary>
        /// Loads all matching export rows when the documented safeguard permits it.
        /// </summary>
        /// <param name="period">The selected half-open report period.</param>
        /// <returns>A complete or explicitly over-limit export snapshot.</returns>
        public ReportSnapshot GetExportSnapshot(ReportPeriod period)
        {
            ValidatePeriod(period);

            ReportSummary summary = GetSummary(
                period.StartInclusive,
                period.EndExclusive);

            if (summary.SessionCount > MaximumExportSessionCount)
            {
                return CreateExportLimitSnapshot(period, summary);
            }

            List<ReportSessionRow> sessions = LoadSessions(
                period.StartInclusive,
                period.EndExclusive,
                MaximumExportSessionCount + 1);

            if (sessions.Count > MaximumExportSessionCount)
            {
                return CreateExportLimitSnapshot(period, summary);
            }

            return new ReportSnapshot
            {
                Period = period,
                Summary = summary,
                Sessions = sessions,
                GeneratedAtLocal = DateTime.Now
            };
        }

        #endregion

        #region Compatibility Queries

        /// <summary>
        /// Loads aggregate report values for the selected date range.
        /// </summary>
        /// <param name="startDate">Optional inclusive local start boundary.</param>
        /// <param name="endDateExclusive">Optional exclusive local end boundary.</param>
        /// <returns>The aggregate report values.</returns>
        public ReportSummary GetSummary(
            DateTime? startDate,
            DateTime? endDateExclusive)
        {
            ValidateDateRange(startDate, endDateExclusive);

            try
            {
                DataTable table = _database.ExecuteDataTable(
                    SummarySql,
                    CreateDateParameter("@StartDate", startDate),
                    CreateDateParameter("@EndDate", endDateExclusive));

                if (table.Rows.Count == 0)
                {
                    return new ReportSummary();
                }

                return MapSummary(table.Rows[0]);
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Loads session-level report rows using the existing interactive limit.
        /// </summary>
        /// <param name="startDate">Optional inclusive local start boundary.</param>
        /// <param name="endDateExclusive">Optional exclusive local end boundary.</param>
        /// <returns>Deterministically ordered session rows.</returns>
        public List<ReportSessionRow> GetSessions(
            DateTime? startDate,
            DateTime? endDateExclusive)
        {
            return LoadSessions(
                startDate,
                endDateExclusive,
                InteractiveDisplayLimit);
        }

        #endregion

        #region Query Helpers

        /// <summary>
        /// Loads a bounded session set for display or export.
        /// </summary>
        private List<ReportSessionRow> LoadSessions(
            DateTime? startDate,
            DateTime? endDateExclusive,
            int limit)
        {
            ValidateDateRange(startDate, endDateExclusive);

            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            try
            {
                DataTable table = _database.ExecuteDataTable(
                    SessionsSql,
                    CreateDateParameter("@StartDate", startDate),
                    CreateDateParameter("@EndDate", endDateExclusive),
                    new MySqlParameter("@Limit", MySqlDbType.Int32)
                    {
                        Value = limit
                    });

                List<ReportSessionRow> sessions =
                    new List<ReportSessionRow>(table.Rows.Count);

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

        /// <summary>
        /// Creates a snapshot that explicitly blocks a silently truncated export.
        /// </summary>
        private static ReportSnapshot CreateExportLimitSnapshot(
            ReportPeriod period,
            ReportSummary summary)
        {
            return new ReportSnapshot
            {
                Period = period,
                Summary = summary,
                GeneratedAtLocal = DateTime.Now,
                IsExportLimitExceeded = true
            };
        }

        #endregion

        #region Mapping

        /// <summary>
        /// Maps aggregate report values from one query row.
        /// </summary>
        private static ReportSummary MapSummary(DataRow row)
        {
            return new ReportSummary
            {
                SessionCount = ToInt(row["SessionCount"]),
                CompletedSessionCount = ToInt(row["CompletedSessionCount"]),
                OpenSessionCount = ToInt(row["OpenSessionCount"]),
                TotalQuestions = ToInt(row["TotalQuestions"]),
                CorrectAnswers = ToInt(row["CorrectAnswers"]),
                WrongAnswers = ToInt(row["WrongAnswers"]),
                PendingAnswers = ToInt(row["PendingAnswers"]),
                ReviewedAnswers = ToInt(row["ReviewedAnswers"]),
                TraineeCount = ToInt(row["TraineeCount"]),
                AverageReviewedAccuracy = ToNullableDecimal(
                    row["AverageReviewedAccuracy"]),
                FirstSessionTime = ToNullableDate(row["FirstSessionTime"]),
                LastSessionTime = ToNullableDate(row["LastSessionTime"])
            };
        }

        /// <summary>
        /// Maps one session report row.
        /// </summary>
        private static ReportSessionRow MapSession(DataRow row)
        {
            return new ReportSessionRow
            {
                SessionID = ToRequiredInt(row["SessionID"], "SessionID"),
                EmployeeNo = ToRequiredString(row["EmployeeNo"], "EmployeeNo"),
                FullName = ToOptionalString(row["FullName"]),
                Department = ToOptionalString(row["Department"]),
                StartTime = ToRequiredDate(row["StartTime"], "StartTime"),
                EndTime = ToNullableDate(row["EndTime"]),
                TotalQuestions = ToInt(row["TotalQuestions"]),
                CorrectAnswers = ToInt(row["CorrectAnswers"]),
                WrongAnswers = ToInt(row["WrongAnswers"]),
                PendingAnswers = ToInt(row["PendingAnswers"]),
                ReviewedAnswers = ToInt(row["ReviewedAnswers"]),
                ReviewedAccuracy = ToNullableDecimal(row["ReviewedAccuracy"])
            };
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates a required report period.
        /// </summary>
        private static void ValidatePeriod(ReportPeriod period)
        {
            if (period == null)
            {
                throw new ArgumentNullException(nameof(period));
            }

            ValidateDateRange(
                period.StartInclusive,
                period.EndExclusive);
        }

        /// <summary>
        /// Validates date-range parameters before SQL execution.
        /// </summary>
        private static void ValidateDateRange(
            DateTime? startDate,
            DateTime? endDateExclusive)
        {
            if (startDate.HasValue &&
                endDateExclusive.HasValue &&
                startDate.Value > endDateExclusive.Value)
            {
                throw new ArgumentException(
                    "The report start boundary must not be later than the end boundary.");
            }
        }

        #endregion

        #region SQL Parameters

        /// <summary>
        /// Creates a nullable parameter for a local date boundary.
        /// </summary>
        private static MySqlParameter CreateDateParameter(
            string name,
            DateTime? value)
        {
            MySqlParameter parameter = new MySqlParameter(
                name,
                MySqlDbType.DateTime);

            parameter.Value = value.HasValue
                ? (object)value.Value
                : DBNull.Value;

            return parameter;
        }

        #endregion

        #region Conversion Helpers

        /// <summary>
        /// Converts an optional numeric database value to an integer.
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
        /// Converts a required numeric database value to an integer.
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
        /// Converts an optional numeric database value to a nullable decimal.
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
        /// Converts an optional string database value.
        /// </summary>
        private static string ToOptionalString(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            return value.ToString();
        }

        /// <summary>
        /// Converts a required string database value.
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
        /// Converts a required date database value.
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
        /// Converts an optional date database value.
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
