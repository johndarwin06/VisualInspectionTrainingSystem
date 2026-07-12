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
            const string sql = @"
SELECT
    COUNT(*) AS SessionCount,
    IFNULL(SUM(s.TotalQuestions), 0) AS TotalQuestions,
    IFNULL(SUM(s.CorrectAnswers), 0) AS CorrectAnswers,
    IFNULL(SUM(s.WrongAnswers), 0) AS WrongAnswers,
    COUNT(DISTINCT s.EmployeeNo) AS TraineeCount,
    IFNULL(ROUND(AVG(s.Accuracy), 2), 0) AS AverageAccuracy,
    MIN(s.StartTime) AS FirstSessionTime,
    MAX(s.StartTime) AS LastSessionTime,
    (
        SELECT COUNT(*)
        FROM tbl_quiz_answer a
        INNER JOIN tbl_training_session ts
            ON ts.SessionID = a.SessionID
        WHERE (@StartDate IS NULL OR ts.StartTime >= @StartDate)
          AND (@EndDate IS NULL OR ts.StartTime < @EndDate)
          AND a.CorrectAnswer IS NULL
    ) AS PendingAnswers,
    (
        SELECT COUNT(*)
        FROM tbl_quiz_answer a
        INNER JOIN tbl_training_session ts
            ON ts.SessionID = a.SessionID
        WHERE (@StartDate IS NULL OR ts.StartTime >= @StartDate)
          AND (@EndDate IS NULL OR ts.StartTime < @EndDate)
          AND a.CorrectAnswer IS NOT NULL
    ) AS ReviewedAnswers
FROM tbl_training_session s
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
            const string sql = @"
SELECT
    s.SessionID,
    s.EmployeeNo,
    IFNULL(u.FullName, '') AS FullName,
    IFNULL(u.Department, '') AS Department,
    s.StartTime,
    s.EndTime,
    s.TotalQuestions,
    s.CorrectAnswers,
    s.WrongAnswers,
    s.Accuracy,
    (
        SELECT COUNT(*)
        FROM tbl_quiz_answer a
        WHERE a.SessionID = s.SessionID
          AND a.CorrectAnswer IS NULL
    ) AS PendingAnswers,
    (
        SELECT COUNT(*)
        FROM tbl_quiz_answer a
        WHERE a.SessionID = s.SessionID
          AND a.CorrectAnswer IS NOT NULL
    ) AS ReviewedAnswers
FROM tbl_training_session s
LEFT JOIN tbl_users u
    ON u.EmployeeNo = s.EmployeeNo
WHERE (@StartDate IS NULL OR s.StartTime >= @StartDate)
  AND (@EndDate IS NULL OR s.StartTime < @EndDate)
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

        private static ReportSessionRow MapSession(DataRow row)
        {
            return new ReportSessionRow
            {
                SessionID = ToInt(row["SessionID"]),
                EmployeeNo = row["EmployeeNo"].ToString(),
                FullName = row["FullName"].ToString(),
                Department = row["Department"].ToString(),
                StartTime = ToDate(row["StartTime"]),
                EndTime = ToNullableDate(row["EndTime"]),
                TotalQuestions = ToInt(row["TotalQuestions"]),
                CorrectAnswers = ToInt(row["CorrectAnswers"]),
                WrongAnswers = ToInt(row["WrongAnswers"]),
                PendingAnswers = ToInt(row["PendingAnswers"]),
                ReviewedAnswers = ToInt(row["ReviewedAnswers"]),
                Accuracy = ToDecimal(row["Accuracy"])
            };
        }

        private static MySqlParameter CreateDateParameter(
            string name,
            DateTime? value)
        {
            return new MySqlParameter(
                name,
                value.HasValue ? (object)value.Value : DBNull.Value);
        }

        private static int ToInt(object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(value);
        }

        private static decimal ToDecimal(object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToDecimal(value);
        }

        private static DateTime ToDate(object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return DateTime.MinValue;
            }

            return Convert.ToDateTime(value);
        }

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
