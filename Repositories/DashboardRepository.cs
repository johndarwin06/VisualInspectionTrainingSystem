#region Namespaces

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
        #region Fields

        private readonly MySqlService _database;

        #endregion

        #region Constructors

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
            if (limit <= 0)
                limit = 10;

            string sql = @"
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
LIMIT " + limit + ";";

            try
            {
                DataTable table = _database.ExecuteDataTable(sql);

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

        private static DashboardSessionSummary MapSession(DataRow row)
        {
            return new DashboardSessionSummary
            {
                SessionID = ToInt(row["SessionID"]),
                EmployeeNo = row["EmployeeNo"].ToString(),
                StartTime = ToDate(row["StartTime"]),
                EndTime = ToNullableDate(row["EndTime"]),
                TotalQuestions = ToInt(row["TotalQuestions"]),
                CorrectAnswers = ToInt(row["CorrectAnswers"]),
                WrongAnswers = ToInt(row["WrongAnswers"]),
                Accuracy = ToDecimal(row["Accuracy"])
            };
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
