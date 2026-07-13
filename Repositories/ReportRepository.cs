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
    /// Provides read-only report data from the existing MySQL tables.
    /// </summary>
    public class ReportRepository
    {
        #region Fields

        private readonly MySqlService _database;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the report repository.
        /// </summary>
        public ReportRepository()
            : this(new MySqlService())
        {
        }

        internal ReportRepository(MySqlService database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads aggregate report values for the selected date range.
        /// </summary>
        public ReportSummary GetSummary(
            DateTime? startDate,
            DateTime? endDateExclusive)
        {
            ValidateDateRange(
                startDate,
                endDateExclusive);

            const string sql = @"
SELECT
    COUNT(*) AS SessionCount,
    IFNULL(SUM(s.TotalQuestions), 0) AS TotalQuestions,
    IFNULL(SUM(IFNULL(answerTotals.CorrectAnswers, 0)), 0) AS CorrectAnswers,
    IFNULL(SUM(IFNULL(answerTotals.WrongAnswers, 0)), 0) AS WrongAnswers,
    COUNT(DISTINCT s.EmployeeNo) AS TraineeCount,
    CASE
        WHEN IFNULL(SUM(IFNULL(answerTotals.ReviewedAnswers, 0)), 0) = 0 THEN 0
        ELSE ROUND(
            IFNULL(SUM(IFNULL(answerTotals.CorrectAnswers, 0)), 0) /
            SUM(IFNULL(answerTotals.ReviewedAnswers, 0)) * 100,
            2)
    END AS AverageAccuracy,
    MIN(s.StartTime) AS FirstSessionTime,
    MAX(s.StartTime) AS LastSessionTime,
    IFNULL(SUM(IFNULL(answerTotals.PendingAnswers, 0)), 0) AS PendingAnswers,
    IFNULL(SUM(IFNULL(answerTotals.ReviewedAnswers, 0)), 0) AS ReviewedAnswers
FROM tbl_training_session s
LEFT JOIN
(
    SELECT
        SessionID,
        SUM(CASE WHEN CorrectAnswer IS NOT NULL AND IsCorrect = 1 THEN 1 ELSE 0 END) AS CorrectAnswers,
        SUM(CASE WHEN CorrectAnswer IS NOT NULL AND IsCorrect = 0 THEN 1 ELSE 0 END) AS WrongAnswers,
        SUM(CASE WHEN CorrectAnswer IS NULL THEN 1 ELSE 0 END) AS PendingAnswers,
        SUM(CASE WHEN CorrectAnswer IS NOT NULL THEN 1 ELSE 0 END) AS ReviewedAnswers
    FROM tbl_quiz_answer
    GROUP BY SessionID
) answerTotals
    ON answerTotals.SessionID = s.SessionID
WHERE (@StartDate IS NULL OR s.StartTime >= @StartDate)
  AND (@EndDate IS NULL OR s.StartTime < @EndDate);";

            try
            {
                DataTable table = _database.ExecuteDataTable(
                    sql,
                    CreateDateParameter("@StartDate", startDate),
                    CreateDateParameter("@EndDate", endDateExclusive));

                if (table.Rows.Count == 0)
                    return new ReportSummary();

                return MapSummary(table.Rows[0]);
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Loads session-level report rows for the selected date range.
        /// </summary>
        public List<ReportSessionRow> GetSessions(
            DateTime? startDate,
            DateTime? endDateExclusive)
        {
            ValidateDateRange(
                startDate,
                endDateExclusive);

            const string sql = @"
SELECT
    s.SessionID,
    s.EmployeeNo,
    IFNULL(u.FullName, '') AS FullName,
    IFNULL(u.Department, '') AS Department,
    s.StartTime,
    s.EndTime,
    s.TotalQuestions,
    IFNULL(SUM(CASE WHEN a.CorrectAnswer IS NOT NULL AND a.IsCorrect = 1 THEN 1 ELSE 0 END), 0) AS CorrectAnswers,
    IFNULL(SUM(CASE WHEN a.CorrectAnswer IS NOT NULL AND a.IsCorrect = 0 THEN 1 ELSE 0 END), 0) AS WrongAnswers,
    IFNULL(SUM(CASE WHEN a.CorrectAnswer IS NULL AND a.AnswerID IS NOT NULL THEN 1 ELSE 0 END), 0) AS PendingAnswers,
    IFNULL(SUM(CASE WHEN a.CorrectAnswer IS NOT NULL THEN 1 ELSE 0 END), 0) AS ReviewedAnswers,
    CASE
        WHEN IFNULL(SUM(CASE WHEN a.CorrectAnswer IS NOT NULL THEN 1 ELSE 0 END), 0) = 0 THEN 0
        ELSE ROUND(
            IFNULL(SUM(CASE WHEN a.CorrectAnswer IS NOT NULL AND a.IsCorrect = 1 THEN 1 ELSE 0 END), 0) /
            SUM(CASE WHEN a.CorrectAnswer IS NOT NULL THEN 1 ELSE 0 END) * 100,
            2)
    END AS Accuracy
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
LIMIT 500;";

            try
            {
                DataTable table = _database.ExecuteDataTable(
                    sql,
                    CreateDateParameter("@StartDate", startDate),
                    CreateDateParameter("@EndDate", endDateExclusive));

                List<ReportSessionRow> sessions =
                    new List<ReportSessionRow>();

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
        /// Maps aggregate report values.
        /// </summary>
        private static ReportSummary MapSummary(DataRow row)
        {
            return new ReportSummary
            {
                SessionCount = ToInt(row["SessionCount"]),
                TotalQuestions = ToInt(row["TotalQuestions"]),
                CorrectAnswers = ToInt(row["CorrectAnswers"]),
                WrongAnswers = ToInt(row["WrongAnswers"]),
                PendingAnswers = ToInt(row["PendingAnswers"]),
                ReviewedAnswers = ToInt(row["ReviewedAnswers"]),
                TraineeCount = ToInt(row["TraineeCount"]),
                AverageAccuracy = ToDecimal(row["AverageAccuracy"]),
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
                Accuracy = ToDecimal(row["Accuracy"])
            };
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates date range parameters before SQL execution.
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
                    "Start date must not be later than end date.");
            }
        }

        #endregion

        #region SQL Parameters

        /// <summary>
        /// Creates a nullable date parameter.
        /// </summary>
        private static MySqlParameter CreateDateParameter(
            string name,
            DateTime? value)
        {
            return new MySqlParameter(
                name,
                value.HasValue ? (object)value.Value : DBNull.Value);
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
        /// Converts a nullable string value.
        /// </summary>
        private static string ToOptionalString(object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return string.Empty;
            }

            return value.ToString();
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
