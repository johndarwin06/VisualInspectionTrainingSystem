#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.Repositories
{
    /// <summary>
    /// Provides bounded read-only history data for an employee identity supplied only by the service boundary.
    /// </summary>
    internal class TrainingHistoryRepository
    {
        #region Constants

        private const int MaximumPageSize = 100;
        private const int MaximumOffset = 10000;
        private const int MaximumSearchLength = 100;

        private const string HistoryPageSql = @"
SELECT
    s.SessionID,
    s.StartTime,
    s.EndTime,
    s.TotalQuestions,
    COUNT(a.AnswerID) AS AnswerCount,
    SUM(CASE
        WHEN UPPER(TRIM(a.UserAnswer)) = 'GOOD' THEN 1
        ELSE 0
    END) AS UserGoodAnswers,
    SUM(CASE
        WHEN UPPER(TRIM(a.UserAnswer)) = 'NG' THEN 1
        ELSE 0
    END) AS UserNgAnswers,
    SUM(CASE
        WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
        ELSE 0
    END) AS ReviewedAnswers,
    SUM(CASE
        WHEN a.AnswerID IS NOT NULL
         AND
         (
             a.CorrectAnswer IS NULL OR
             UPPER(TRIM(a.CorrectAnswer)) NOT IN ('GOOD', 'NG')
         ) THEN 1
        ELSE 0
    END) AS PendingAnswers,
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
    CASE
        WHEN SUM(CASE
            WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
            ELSE 0
        END) = 0 THEN NULL
        ELSE ROUND(
            SUM(CASE
                WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG')
                 AND UPPER(TRIM(a.UserAnswer)) IN ('GOOD', 'NG')
                 AND UPPER(TRIM(a.UserAnswer)) = UPPER(TRIM(a.CorrectAnswer)) THEN 1
                ELSE 0
            END) * 100.0 /
            SUM(CASE
                WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
                ELSE 0
            END),
            2)
    END AS ReviewedAccuracy
FROM tbl_training_session s
LEFT JOIN tbl_quiz_answer a
    ON a.SessionID = s.SessionID
WHERE s.EmployeeNo = @EmployeeNo
  AND s.EndTime IS NOT NULL
  AND (@StartInclusive IS NULL OR s.EndTime >= @StartInclusive)
  AND (@EndExclusive IS NULL OR s.EndTime < @EndExclusive)
  AND
  (
      @SearchText = '' OR
      CAST(s.SessionID AS CHAR) = @SearchText OR
      EXISTS
      (
          SELECT 1
          FROM tbl_quiz_answer searchAnswer
          WHERE searchAnswer.SessionID = s.SessionID
            AND INSTR(
                UPPER(IFNULL(searchAnswer.ImageFileName, '')),
                @SearchText) > 0
      )
  )
GROUP BY
    s.SessionID,
    s.StartTime,
    s.EndTime,
    s.TotalQuestions
HAVING
    @ReviewFilter = 0 OR
    (@ReviewFilter = 1 AND ReviewedAnswers = 0) OR
    (@ReviewFilter = 2 AND ReviewedAnswers > 0 AND PendingAnswers > 0) OR
    (@ReviewFilter = 3 AND AnswerCount > 0 AND PendingAnswers = 0)
ORDER BY s.StartTime DESC, s.SessionID DESC
LIMIT @Limit OFFSET @Offset;";

        private const string SessionSummarySql = @"
SELECT
    s.SessionID,
    s.StartTime,
    s.EndTime,
    s.TotalQuestions,
    COUNT(a.AnswerID) AS AnswerCount,
    SUM(CASE
        WHEN UPPER(TRIM(a.UserAnswer)) = 'GOOD' THEN 1
        ELSE 0
    END) AS UserGoodAnswers,
    SUM(CASE
        WHEN UPPER(TRIM(a.UserAnswer)) = 'NG' THEN 1
        ELSE 0
    END) AS UserNgAnswers,
    SUM(CASE
        WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
        ELSE 0
    END) AS ReviewedAnswers,
    SUM(CASE
        WHEN a.AnswerID IS NOT NULL
         AND
         (
             a.CorrectAnswer IS NULL OR
             UPPER(TRIM(a.CorrectAnswer)) NOT IN ('GOOD', 'NG')
         ) THEN 1
        ELSE 0
    END) AS PendingAnswers,
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
    CASE
        WHEN SUM(CASE
            WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
            ELSE 0
        END) = 0 THEN NULL
        ELSE ROUND(
            SUM(CASE
                WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG')
                 AND UPPER(TRIM(a.UserAnswer)) IN ('GOOD', 'NG')
                 AND UPPER(TRIM(a.UserAnswer)) = UPPER(TRIM(a.CorrectAnswer)) THEN 1
                ELSE 0
            END) * 100.0 /
            SUM(CASE
                WHEN UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
                ELSE 0
            END),
            2)
    END AS ReviewedAccuracy
FROM tbl_training_session s
LEFT JOIN tbl_quiz_answer a
    ON a.SessionID = s.SessionID
WHERE s.EmployeeNo = @EmployeeNo
  AND s.SessionID = @SessionID
  AND s.EndTime IS NOT NULL
GROUP BY
    s.SessionID,
    s.StartTime,
    s.EndTime,
    s.TotalQuestions;";

        private const string SessionAnswersSql = @"
SELECT
    a.AnswerID,
    a.SessionID,
    a.ImageHash,
    a.ImageFileName,
    a.UserAnswer,
    a.CorrectAnswer,
    a.ReviewSource
FROM tbl_quiz_answer a
INNER JOIN tbl_training_session s
    ON s.SessionID = a.SessionID
WHERE s.EmployeeNo = @EmployeeNo
  AND s.SessionID = @SessionID
  AND s.EndTime IS NOT NULL
ORDER BY a.AnswerTime ASC, a.AnswerID ASC;";

        #endregion

        #region Fields

        private readonly MySqlService _database;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the repository with the application's configured database service.
        /// </summary>
        public TrainingHistoryRepository()
            : this(new MySqlService())
        {
        }

        /// <summary>
        /// Initializes the repository with an explicit database service.
        /// </summary>
        /// <param name="database">Database service retained by the repository.</param>
        internal TrainingHistoryRepository(MySqlService database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        #endregion

        #region Internal Read Methods

        /// <summary>
        /// Loads one bounded current-identity history page in deterministic newest-first order.
        /// </summary>
        /// <param name="employeeNo">Employee identity captured by the service boundary.</param>
        /// <param name="query">Filter and paging request with no user identity.</param>
        /// <returns>A bounded page containing at most the requested number of rows.</returns>
        internal virtual TrainingHistoryPage GetHistoryPage(
            string employeeNo,
            TrainingHistoryQuery query)
        {
            string normalizedEmployeeNo = ValidateEmployeeNo(employeeNo);
            ValidateQuery(query);

            int requestedLimit = query.Limit + 1;
            List<TrainingHistorySessionSummary> sessions =
                new List<TrainingHistorySessionSummary>();

            try
            {
                _database.OpenConnection();

                using (MySqlCommand command = new MySqlCommand(
                    HistoryPageSql,
                    _database.GetConnection()))
                {
                    AddIdentityParameter(command, normalizedEmployeeNo);
                    AddQueryParameters(command, query, requestedLimit);

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            sessions.Add(MapSummary(reader));
                        }
                    }
                }
            }
            finally
            {
                _database.CloseConnection();
            }

            bool hasMore = sessions.Count > query.Limit;

            if (hasMore)
            {
                sessions.RemoveAt(sessions.Count - 1);
            }

            return new TrainingHistoryPage(sessions, hasMore);
        }

        /// <summary>
        /// Loads one authorized session summary and answer list through a consistent read transaction.
        /// </summary>
        /// <param name="employeeNo">Employee identity captured by the service boundary.</param>
        /// <param name="sessionId">Requested session identity.</param>
        /// <returns>The session detail, or null when the session is absent or belongs to another user.</returns>
        internal virtual TrainingHistorySessionDetail GetSessionDetail(
            string employeeNo,
            int sessionId)
        {
            string normalizedEmployeeNo = ValidateEmployeeNo(employeeNo);

            if (sessionId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sessionId),
                    "Session identity must be greater than zero.");
            }

            MySqlTransaction transaction = null;

            try
            {
                _database.OpenConnection();
                MySqlConnection connection = _database.GetConnection();
                transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);

                TrainingHistorySessionSummary summary = LoadSummary(
                    connection,
                    transaction,
                    normalizedEmployeeNo,
                    sessionId);

                if (summary == null)
                {
                    transaction.Commit();
                    transaction = null;
                    return null;
                }

                List<TrainingHistoryAnswerDetail> answers = LoadAnswers(
                    connection,
                    transaction,
                    normalizedEmployeeNo,
                    sessionId);

                TrainingHistorySessionDetail detail =
                    new TrainingHistorySessionDetail(summary, answers);

                transaction.Commit();
                transaction = null;

                return detail;
            }
            catch
            {
                RollbackQuietly(transaction);
                transaction = null;
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    RollbackQuietly(transaction);
                }

                _database.CloseConnection();
            }
        }

        #endregion

        #region Detail Loading

        private static TrainingHistorySessionSummary LoadSummary(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string employeeNo,
            int sessionId)
        {
            using (MySqlCommand command = new MySqlCommand(
                SessionSummarySql,
                connection,
                transaction))
            {
                AddIdentityParameter(command, employeeNo);
                command.Parameters.Add(
                    "@SessionID",
                    MySqlDbType.Int32).Value = sessionId;

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    return reader.Read()
                        ? MapSummary(reader)
                        : null;
                }
            }
        }

        private static List<TrainingHistoryAnswerDetail> LoadAnswers(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string employeeNo,
            int sessionId)
        {
            List<TrainingHistoryAnswerDetail> answers =
                new List<TrainingHistoryAnswerDetail>();

            using (MySqlCommand command = new MySqlCommand(
                SessionAnswersSql,
                connection,
                transaction))
            {
                AddIdentityParameter(command, employeeNo);
                command.Parameters.Add(
                    "@SessionID",
                    MySqlDbType.Int32).Value = sessionId;

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    int questionNumber = 0;

                    while (reader.Read())
                    {
                        questionNumber++;
                        answers.Add(MapAnswer(reader, questionNumber));
                    }
                }
            }

            return answers;
        }

        #endregion

        #region Mapping

        private static TrainingHistorySessionSummary MapSummary(
            MySqlDataReader reader)
        {
            return new TrainingHistorySessionSummary
            {
                SessionID = ReadRequiredInt(reader, "SessionID"),
                StartTime = ReadRequiredDate(reader, "StartTime"),
                EndTime = ReadRequiredDate(reader, "EndTime"),
                TotalQuestions = ReadNonNegativeInt(reader, "TotalQuestions"),
                AnswerCount = ReadNonNegativeInt(reader, "AnswerCount"),
                UserGoodAnswers = ReadNonNegativeInt(reader, "UserGoodAnswers"),
                UserNgAnswers = ReadNonNegativeInt(reader, "UserNgAnswers"),
                ReviewedAnswers = ReadNonNegativeInt(reader, "ReviewedAnswers"),
                PendingAnswers = ReadNonNegativeInt(reader, "PendingAnswers"),
                CorrectReviewedAnswers = ReadNonNegativeInt(
                    reader,
                    "CorrectReviewedAnswers"),
                WrongReviewedAnswers = ReadNonNegativeInt(
                    reader,
                    "WrongReviewedAnswers"),
                ReviewedAccuracy = ReadNullableDecimal(reader, "ReviewedAccuracy")
            };
        }

        private static TrainingHistoryAnswerDetail MapAnswer(
            MySqlDataReader reader,
            int questionNumber)
        {
            string userAnswer = NormalizeAnswerText(
                ReadOptionalString(reader, "UserAnswer"));
            string correctAnswer = NormalizeAnswerText(
                ReadOptionalString(reader, "CorrectAnswer"));
            bool isReviewed = correctAnswer != null;
            bool isCorrect = isReviewed &&
                             userAnswer != null &&
                             string.Equals(
                                 userAnswer,
                                 correctAnswer,
                                 StringComparison.Ordinal);

            return new TrainingHistoryAnswerDetail
            {
                AnswerID = ReadRequiredInt(reader, "AnswerID"),
                SessionID = ReadRequiredInt(reader, "SessionID"),
                QuestionNumber = questionNumber,
                ImageFileName = GetSafeFileName(
                    ReadOptionalString(reader, "ImageFileName")),
                ShortImageIdentifier = GetShortImageIdentifier(
                    ReadOptionalString(reader, "ImageHash")),
                UserAnswerText = userAnswer ?? "Unknown",
                CorrectAnswerText = correctAnswer ?? "Pending",
                OutcomeText = !isReviewed
                    ? "Pending"
                    : (isCorrect ? "Correct" : "Wrong"),
                ReviewSourceText = GetReviewSourceText(
                    isReviewed,
                    ReadOptionalString(reader, "ReviewSource")),
                IsReviewed = isReviewed,
                IsCorrect = isCorrect,
                ElapsedSeconds = null
            };
        }

        #endregion

        #region Parameters and Validation

        private static void AddIdentityParameter(
            MySqlCommand command,
            string employeeNo)
        {
            command.Parameters.Add(
                "@EmployeeNo",
                MySqlDbType.VarChar,
                20).Value = employeeNo;
        }

        private static void AddQueryParameters(
            MySqlCommand command,
            TrainingHistoryQuery query,
            int requestedLimit)
        {
            MySqlParameter start = command.Parameters.Add(
                "@StartInclusive",
                MySqlDbType.DateTime);
            start.Value = query.StartInclusive.HasValue
                ? (object)query.StartInclusive.Value
                : DBNull.Value;

            MySqlParameter end = command.Parameters.Add(
                "@EndExclusive",
                MySqlDbType.DateTime);
            end.Value = query.EndExclusive.HasValue
                ? (object)query.EndExclusive.Value
                : DBNull.Value;

            command.Parameters.Add(
                "@SearchText",
                MySqlDbType.VarChar,
                MaximumSearchLength).Value = NormalizeSearch(query.SearchText);
            command.Parameters.Add(
                "@ReviewFilter",
                MySqlDbType.Int32).Value = (int)query.ReviewFilter;
            command.Parameters.Add(
                "@Limit",
                MySqlDbType.Int32).Value = requestedLimit;
            command.Parameters.Add(
                "@Offset",
                MySqlDbType.Int32).Value = query.Offset;
        }

        private static string ValidateEmployeeNo(string employeeNo)
        {
            if (string.IsNullOrWhiteSpace(employeeNo))
            {
                throw new UnauthorizedAccessException(
                    "An authenticated user is required for training history.");
            }

            string normalized = employeeNo.Trim();

            if (normalized.Length > 20)
            {
                throw new UnauthorizedAccessException(
                    "The authenticated user identity is invalid.");
            }

            return normalized;
        }

        private static void ValidateQuery(TrainingHistoryQuery query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            if (query.Limit <= 0 || query.Limit > MaximumPageSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(query.Limit),
                    "History page size must be between 1 and 100.");
            }

            if (query.Offset < 0 || query.Offset > MaximumOffset)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(query.Offset),
                    "History page offset is outside the supported range.");
            }

            if (query.StartInclusive.HasValue &&
                query.EndExclusive.HasValue &&
                query.EndExclusive.Value <= query.StartInclusive.Value)
            {
                throw new ArgumentException(
                    "History end date must be later than the start date.");
            }

            if (!Enum.IsDefined(
                    typeof(TrainingHistoryReviewFilter),
                    query.ReviewFilter))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(query.ReviewFilter),
                    "History review filter is unsupported.");
            }

            if (!string.IsNullOrWhiteSpace(query.SearchText) &&
                query.SearchText.Trim().Length > MaximumSearchLength)
            {
                throw new ArgumentException(
                    "History search text is too long.",
                    nameof(query.SearchText));
            }
        }

        private static string NormalizeSearch(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Formats persisted review provenance without exposing reviewer identity.
        /// </summary>
        private static string GetReviewSourceText(
            bool isReviewed,
            string reviewSource)
        {
            if (!isReviewed)
                return "Pending";

            return string.Equals(
                reviewSource == null ? null : reviewSource.Trim(),
                QuizAnswer.AutomaticReviewSource,
                StringComparison.OrdinalIgnoreCase)
                ? "Automatic Review"
                : "Administrator Review";
        }

        #endregion

        #region Conversion Helpers

        private static int ReadRequiredInt(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];

            if (value == null || value == DBNull.Value)
            {
                throw new InvalidOperationException(columnName + " is required.");
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static int ReadNonNegativeInt(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];

            if (value == null || value == DBNull.Value)
                return 0;

            int converted = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return converted < 0 ? 0 : converted;
        }

        private static DateTime ReadRequiredDate(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];

            if (value == null || value == DBNull.Value)
            {
                throw new InvalidOperationException(columnName + " is required.");
            }

            return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        }

        private static decimal? ReadNullableDecimal(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];

            if (value == null || value == DBNull.Value)
                return null;

            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        private static string ReadOptionalString(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];
            return value == null || value == DBNull.Value
                ? null
                : value.ToString();
        }

        private static string NormalizeAnswerText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string normalized = value.Trim().ToUpperInvariant();
            return normalized == "GOOD" || normalized == "NG"
                ? normalized
                : null;
        }

        private static string GetSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            try
            {
                return Path.GetFileName(value.Trim());
            }
            catch
            {
                return null;
            }
        }

        private static string GetShortImageIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string normalized = value.Trim().ToLowerInvariant();

            if (normalized.Length != 64)
                return null;

            for (int index = 0; index < normalized.Length; index++)
            {
                char character = normalized[index];
                bool isHex = character >= '0' && character <= '9' ||
                             character >= 'a' && character <= 'f';

                if (!isHex)
                    return null;
            }

            return normalized.Substring(0, 12);
        }

        #endregion

        #region Transaction Cleanup

        private static void RollbackQuietly(MySqlTransaction transaction)
        {
            if (transaction == null)
                return;

            try
            {
                transaction.Rollback();
            }
            catch
            {
                // Preserve the original read failure while still closing the connection.
            }
        }

        #endregion
    }
}
