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

        internal DashboardRepository(MySqlService database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads high-level training and review metrics.
        /// </summary>
        public DashboardMetrics GetMetrics()
        {
            const string sql = @"
SELECT
    (SELECT COUNT(*) FROM tbl_training_session) AS TotalSessions,
    (SELECT COUNT(*) FROM tbl_quiz_answer) AS TotalAnswers,
    (SELECT COUNT(*) FROM tbl_quiz_answer WHERE CorrectAnswer IS NOT NULL) AS ReviewedAnswers,
    (SELECT COUNT(*) FROM tbl_quiz_answer WHERE CorrectAnswer IS NULL) AS PendingAnswers,
    (SELECT COUNT(DISTINCT EmployeeNo) FROM tbl_training_session) AS ActiveTrainees,
    (SELECT IFNULL(ROUND(AVG(Accuracy), 2), 0) FROM tbl_training_session) AS AverageAccuracy,
    (SELECT MAX(StartTime) FROM tbl_training_session) AS LatestSessionTime;";

            try
            {
                DataTable table = _database.ExecuteDataTable(sql);

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
        /// Loads the most recent training sessions.
        /// </summary>
        public List<DashboardSessionSummary> GetRecentSessions(int limit)
        {
            ValidateLimit(limit);

            const string sql = @"
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

            try
            {
                DataTable table = _database.ExecuteDataTable(
                    sql,
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
        /// Maps aggregate dashboard values.
        /// </summary>
        private static DashboardMetrics MapMetrics(DataRow row)
        {
            return new DashboardMetrics
            {
                TotalSessions = ToInt(row["TotalSessions"]),
                TotalAnswers = ToInt(row["TotalAnswers"]),
                ReviewedAnswers = ToInt(row["ReviewedAnswers"]),
                PendingAnswers = ToInt(row["PendingAnswers"]),
                ActiveTrainees = ToInt(row["ActiveTrainees"]),
                AverageAccuracy = ToDecimal(row["AverageAccuracy"]),
                LatestSessionTime = ToNullableDate(row["LatestSessionTime"])
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

        #region Conversion Helpers

        private static int ToInt(object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(value);
        }

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

        private static decimal ToDecimal(object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToDecimal(value);
        }

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
