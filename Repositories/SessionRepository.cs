#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.Repositories
{
    /// <summary>
    /// Provides database access for completed training sessions.
    /// Matches the tbl_training_session schema that uses EmployeeNo.
    /// </summary>
    public class SessionRepository
    {
        #region Constants

        private const string TableName = "tbl_training_session";
        private const string DuplicateKeyColumnName = "DuplicateKey";
        private const string DuplicateKeyIndexName = "UX_tbl_training_session_DuplicateKey";

        #endregion

        #region Fields

        private readonly MySqlService _database;

        private readonly AnswerRepository _answerRepository;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the session repository.
        /// </summary>
        public SessionRepository()
        {
            _database = new MySqlService();

            _answerRepository = new AnswerRepository(_database);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Saves one completed training session and its answers atomically.
        /// </summary>
        /// <param name="session">The completed training session.</param>
        /// <returns>The saved session identity.</returns>
        public int Save(TrainingSession session)
        {
            ValidatedSession validatedSession = ValidateSession(session);

            MySqlTransaction transaction = null;
            int sessionId = 0;

            try
            {
                _database.OpenConnection();

                MySqlConnection connection = _database.GetConnection();

                EnsureTable(connection);

                _answerRepository.EnsureTable(connection);

                transaction = connection.BeginTransaction();

                EnsureNotDuplicate(
                    validatedSession,
                    connection,
                    transaction);

                sessionId = InsertSession(
                    validatedSession,
                    connection,
                    transaction);

                session.SessionID = sessionId;

                _answerRepository.SaveMany(
                    sessionId,
                    validatedSession.Answers,
                    connection,
                    transaction);

                transaction.Commit();
                transaction = null;

                return sessionId;
            }
            catch (Exception ex)
            {
                RollbackTransaction(
                    transaction,
                    "completed quiz session persistence",
                    ex);

                if (sessionId > 0)
                {
                    session.SessionID = 0;
                }

                if (IsDuplicateValidationException(ex))
                {
                    throw;
                }

                if (IsDuplicateKeyException(ex))
                {
                    throw CreateDuplicateSessionException(ex);
                }

                throw new InvalidOperationException(
                    "Failed to save the completed quiz session. The session and answer inserts were rolled back.",
                    ex);
            }
            finally
            {
                if (transaction != null)
                {
                    transaction.Dispose();
                }

                _database.CloseConnection();
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates and upgrades the session table when needed.
        /// DDL is intentionally executed outside data transactions.
        /// </summary>
        /// <param name="connection">The open MySQL connection.</param>
        internal void EnsureTable(MySqlConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            const string sql = @"
CREATE TABLE IF NOT EXISTS tbl_training_session
(
    SessionID INT AUTO_INCREMENT PRIMARY KEY,
    EmployeeNo VARCHAR(20) NOT NULL,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME NULL,
    TotalQuestions INT DEFAULT 0,
    CorrectAnswers INT DEFAULT 0,
    WrongAnswers INT DEFAULT 0,
    Accuracy DECIMAL(5,2) DEFAULT 0,
    DuplicateKey VARCHAR(64) NULL,
    UNIQUE KEY UX_tbl_training_session_DuplicateKey (DuplicateKey),
    FOREIGN KEY(EmployeeNo)
        REFERENCES tbl_users(EmployeeNo)
);";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }

            EnsureDuplicateKeyColumn(connection);

            if (!IndexExists(
                    connection,
                    TableName,
                    DuplicateKeyIndexName))
            {
                BackfillDuplicateKeys(connection);

                EnsureDuplicateKeyIndex(connection);
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates a completed training session before persistence.
        /// </summary>
        private static ValidatedSession ValidateSession(TrainingSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (session.SessionID > 0)
            {
                throw new InvalidOperationException(
                    "Training session has already been saved.");
            }

            if (session.User == null)
            {
                throw new ArgumentException(
                    "Training session has no user.",
                    nameof(session));
            }

            if (string.IsNullOrWhiteSpace(session.User.EmployeeNo))
            {
                throw new ArgumentException(
                    "Training session user has no EmployeeNo.",
                    nameof(session));
            }

            if (session.TotalQuestions <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(session),
                    "TotalQuestions must be greater than zero.");
            }

            if (!session.Finished.HasValue)
            {
                throw new InvalidOperationException(
                    "Only completed training sessions can be saved.");
            }

            if (session.Started > session.Finished.Value)
            {
                throw new ArgumentException(
                    "StartTime must not be later than EndTime.",
                    nameof(session));
            }

            if (session.Answers == null)
                throw new ArgumentNullException("session.Answers");

            List<QuizAnswer> answers = new List<QuizAnswer>(session.Answers);

            if (answers.Count == 0)
            {
                throw new ArgumentException(
                    "Answer collections must not be empty.",
                    nameof(session));
            }

            if (answers.Count != session.TotalQuestions)
            {
                throw new ArgumentException(
                    "Answer count must match TotalQuestions for a completed training session.",
                    nameof(session));
            }

            for (int index = 0; index < answers.Count; index++)
            {
                AnswerRepository.ValidateAnswerForPersistence(
                    answers[index],
                    "session.Answers[" + index + "]");
            }

            ValidateScoreTotals(session);

            DateTime startTime = TrimToSecond(session.Started);
            DateTime endTime = TrimToSecond(session.Finished.Value);
            string duplicateKey = BuildDuplicateKey(
                session.User.EmployeeNo,
                startTime,
                endTime,
                session.TotalQuestions,
                answers.Count);

            return new ValidatedSession(
                session,
                answers,
                startTime,
                endTime,
                duplicateKey);
        }

        /// <summary>
        /// Validates calculated training-session totals.
        /// </summary>
        private static void ValidateScoreTotals(TrainingSession session)
        {
            int totalQuestions = session.TotalQuestions;
            int correctAnswers = session.CorrectAnswers;
            int wrongAnswers = session.WrongAnswers;
            double accuracy = session.Accuracy;

            if (correctAnswers < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(session),
                    "CorrectAnswers must not be negative.");
            }

            if (wrongAnswers < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(session),
                    "WrongAnswers must not be negative.");
            }

            if (correctAnswers + wrongAnswers > totalQuestions)
            {
                throw new ArgumentException(
                    "CorrectAnswers and WrongAnswers must not exceed TotalQuestions.",
                    nameof(session));
            }

            if (accuracy < 0 ||
                accuracy > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(session),
                    "Accuracy must be from 0 through 100.");
            }
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Prevents the same completion event from being saved twice.
        /// </summary>
        private static void EnsureNotDuplicate(
            ValidatedSession session,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            EnsureNoDuplicateKey(
                session,
                connection,
                transaction);

            EnsureNoLegacyDuplicate(
                session,
                connection,
                transaction);
        }

        /// <summary>
        /// Checks the database-enforced duplicate key before insertion.
        /// </summary>
        private static void EnsureNoDuplicateKey(
            ValidatedSession session,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
SELECT SessionID
FROM tbl_training_session
WHERE DuplicateKey = @DuplicateKey
LIMIT 1
FOR UPDATE;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@DuplicateKey", session.DuplicateKey);

                object result = command.ExecuteScalar();

                if (result != null &&
                    result != DBNull.Value)
                {
                    throw CreateDuplicateSessionException(null);
                }
            }
        }

        /// <summary>
        /// Checks historical rows that may not have a duplicate key because they predate the unique index.
        /// </summary>
        private static void EnsureNoLegacyDuplicate(
            ValidatedSession session,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
SELECT s.SessionID
FROM tbl_training_session s
WHERE s.EmployeeNo = @EmployeeNo
  AND ABS(TIMESTAMPDIFF(SECOND, s.StartTime, @StartTime)) = 0
  AND s.EndTime IS NOT NULL
  AND ABS(TIMESTAMPDIFF(SECOND, s.EndTime, @EndTime)) = 0
  AND s.TotalQuestions = @TotalQuestions
  AND (s.DuplicateKey IS NULL OR s.DuplicateKey <> @DuplicateKey)
  AND
  (
      SELECT COUNT(*)
      FROM tbl_quiz_answer a
      WHERE a.SessionID = s.SessionID
  ) = @AnswerCount
ORDER BY s.SessionID DESC
LIMIT 1
FOR UPDATE;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@EmployeeNo", session.EmployeeNo);
                command.Parameters.AddWithValue("@StartTime", session.StartTime);
                command.Parameters.AddWithValue("@EndTime", session.EndTime);
                command.Parameters.AddWithValue("@TotalQuestions", session.TotalQuestions);
                command.Parameters.AddWithValue("@AnswerCount", session.AnswerCount);
                command.Parameters.AddWithValue("@DuplicateKey", session.DuplicateKey);

                object result = command.ExecuteScalar();

                if (result != null &&
                    result != DBNull.Value)
                {
                    throw CreateDuplicateSessionException(null);
                }
            }
        }

        /// <summary>
        /// Inserts the session header and returns the generated identity.
        /// </summary>
        private static int InsertSession(
            ValidatedSession session,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            string sql = @"
INSERT INTO " + TableName + @"
(
    EmployeeNo,
    StartTime,
    EndTime,
    TotalQuestions,
    CorrectAnswers,
    WrongAnswers,
    Accuracy,
    DuplicateKey
)
VALUES
(
    @EmployeeNo,
    @StartTime,
    @EndTime,
    @TotalQuestions,
    @CorrectAnswers,
    @WrongAnswers,
    @Accuracy,
    @DuplicateKey
);";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@EmployeeNo", session.EmployeeNo);
                command.Parameters.AddWithValue("@StartTime", session.StartTime);
                command.Parameters.AddWithValue("@EndTime", session.EndTime);
                command.Parameters.AddWithValue("@TotalQuestions", session.TotalQuestions);
                command.Parameters.AddWithValue("@CorrectAnswers", session.CorrectAnswers);
                command.Parameters.AddWithValue("@WrongAnswers", session.WrongAnswers);
                command.Parameters.AddWithValue("@Accuracy", session.Accuracy);
                command.Parameters.AddWithValue("@DuplicateKey", session.DuplicateKey);

                command.ExecuteNonQuery();

                return Convert.ToInt32(command.LastInsertedId);
            }
        }

        #endregion

        #region Schema Upgrade

        /// <summary>
        /// Ensures the duplicate key column exists for existing installations.
        /// </summary>
        private static void EnsureDuplicateKeyColumn(MySqlConnection connection)
        {
            if (ColumnExists(
                    connection,
                    TableName,
                    DuplicateKeyColumnName))
            {
                return;
            }

            const string sql = @"
ALTER TABLE tbl_training_session
ADD COLUMN DuplicateKey VARCHAR(64) NULL AFTER Accuracy;";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Backfills duplicate keys for existing non-duplicate completed sessions before the unique index is created.
        /// </summary>
        private static void BackfillDuplicateKeys(MySqlConnection connection)
        {
            List<ExistingSessionDuplicateInfo> sessions =
                LoadExistingSessionDuplicateInfo(connection);

            Dictionary<string, List<int>> idsByDuplicateKey =
                new Dictionary<string, List<int>>(StringComparer.Ordinal);

            foreach (ExistingSessionDuplicateInfo session in sessions)
            {
                if (string.IsNullOrWhiteSpace(session.DuplicateKey))
                {
                    UpdateDuplicateKey(
                        connection,
                        session.SessionID,
                        null);

                    continue;
                }

                List<int> sessionIds;

                if (!idsByDuplicateKey.TryGetValue(
                        session.DuplicateKey,
                        out sessionIds))
                {
                    sessionIds = new List<int>();
                    idsByDuplicateKey.Add(
                        session.DuplicateKey,
                        sessionIds);
                }

                sessionIds.Add(session.SessionID);
            }

            foreach (KeyValuePair<string, List<int>> duplicateGroup in idsByDuplicateKey)
            {
                string duplicateKeyToStore =
                    duplicateGroup.Value.Count == 1
                        ? duplicateGroup.Key
                        : null;

                foreach (int sessionId in duplicateGroup.Value)
                {
                    UpdateDuplicateKey(
                        connection,
                        sessionId,
                        duplicateKeyToStore);
                }
            }
        }

        /// <summary>
        /// Loads existing sessions with the answer count needed to build duplicate keys.
        /// </summary>
        private static List<ExistingSessionDuplicateInfo> LoadExistingSessionDuplicateInfo(
            MySqlConnection connection)
        {
            const string sql = @"
SELECT
    s.SessionID,
    s.EmployeeNo,
    s.StartTime,
    s.EndTime,
    s.TotalQuestions,
    COUNT(a.AnswerID) AS AnswerCount
FROM tbl_training_session s
LEFT JOIN tbl_quiz_answer a
    ON a.SessionID = s.SessionID
GROUP BY
    s.SessionID,
    s.EmployeeNo,
    s.StartTime,
    s.EndTime,
    s.TotalQuestions;";

            List<ExistingSessionDuplicateInfo> sessions =
                new List<ExistingSessionDuplicateInfo>();

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    int sessionId = Convert.ToInt32(reader["SessionID"]);
                    string duplicateKey = TryBuildExistingDuplicateKey(reader);

                    sessions.Add(
                        new ExistingSessionDuplicateInfo(
                            sessionId,
                            duplicateKey));
                }
            }

            return sessions;
        }

        /// <summary>
        /// Creates the unique duplicate key index when it is missing.
        /// </summary>
        private static void EnsureDuplicateKeyIndex(MySqlConnection connection)
        {
            if (IndexExists(
                    connection,
                    TableName,
                    DuplicateKeyIndexName))
            {
                return;
            }

            const string sql = @"
CREATE UNIQUE INDEX UX_tbl_training_session_DuplicateKey
ON tbl_training_session (DuplicateKey);";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Updates a session duplicate key during schema migration.
        /// </summary>
        private static void UpdateDuplicateKey(
            MySqlConnection connection,
            int sessionId,
            string duplicateKey)
        {
            const string sql = @"
UPDATE tbl_training_session
SET DuplicateKey = @DuplicateKey
WHERE SessionID = @SessionID;";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue(
                    "@DuplicateKey",
                    string.IsNullOrWhiteSpace(duplicateKey)
                        ? (object)DBNull.Value
                        : duplicateKey);

                command.Parameters.AddWithValue("@SessionID", sessionId);

                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Returns true when a table column exists in the current database.
        /// </summary>
        private static bool ColumnExists(
            MySqlConnection connection,
            string tableName,
            string columnName)
        {
            const string sql = @"
SELECT COUNT(*)
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @TableName
  AND COLUMN_NAME = @ColumnName;";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);
                command.Parameters.AddWithValue("@ColumnName", columnName);

                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        /// <summary>
        /// Returns true when an index exists in the current database.
        /// </summary>
        private static bool IndexExists(
            MySqlConnection connection,
            string tableName,
            string indexName)
        {
            const string sql = @"
SELECT COUNT(*)
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @TableName
  AND INDEX_NAME = @IndexName;";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);
                command.Parameters.AddWithValue("@IndexName", indexName);

                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        #endregion

        #region Duplicate Key Helpers

        /// <summary>
        /// Builds the stable completion key protected by the database unique index.
        /// </summary>
        private static string BuildDuplicateKey(
            string employeeNo,
            DateTime startTime,
            DateTime endTime,
            int totalQuestions,
            int answerCount)
        {
            string material =
                NormalizeEmployeeNo(employeeNo) +
                "|" +
                startTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) +
                "|" +
                endTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) +
                "|" +
                totalQuestions.ToString(CultureInfo.InvariantCulture) +
                "|" +
                answerCount.ToString(CultureInfo.InvariantCulture);

            return ComputeSha256Hex(material);
        }

        /// <summary>
        /// Builds a duplicate key for an existing completed session, or null when the historical row is incomplete.
        /// </summary>
        private static string TryBuildExistingDuplicateKey(MySqlDataReader reader)
        {
            if (reader["EmployeeNo"] == null ||
                reader["EmployeeNo"] == DBNull.Value ||
                string.IsNullOrWhiteSpace(reader["EmployeeNo"].ToString()) ||
                reader["StartTime"] == null ||
                reader["StartTime"] == DBNull.Value ||
                reader["EndTime"] == null ||
                reader["EndTime"] == DBNull.Value ||
                reader["TotalQuestions"] == null ||
                reader["TotalQuestions"] == DBNull.Value ||
                reader["AnswerCount"] == null ||
                reader["AnswerCount"] == DBNull.Value)
            {
                return null;
            }

            int totalQuestions = Convert.ToInt32(reader["TotalQuestions"]);
            int answerCount = Convert.ToInt32(reader["AnswerCount"]);

            if (totalQuestions <= 0 ||
                answerCount <= 0)
            {
                return null;
            }

            return BuildDuplicateKey(
                reader["EmployeeNo"].ToString(),
                TrimToSecond(Convert.ToDateTime(reader["StartTime"])),
                TrimToSecond(Convert.ToDateTime(reader["EndTime"])),
                totalQuestions,
                answerCount);
        }

        /// <summary>
        /// Normalizes employee numbers for a database-level idempotency key.
        /// </summary>
        private static string NormalizeEmployeeNo(string employeeNo)
        {
            return employeeNo.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Computes a lowercase SHA-256 hex string.
        /// </summary>
        private static string ComputeSha256Hex(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);

                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(
                        hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Rolls back an active transaction and preserves rollback failures.
        /// </summary>
        private static void RollbackTransaction(
            MySqlTransaction transaction,
            string operationName,
            Exception originalException)
        {
            if (transaction == null)
                return;

            try
            {
                transaction.Rollback();
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Failed to roll back the " +
                    operationName +
                    " transaction.",
                    new AggregateException(
                        originalException,
                        rollbackException));
            }
        }

        /// <summary>
        /// Creates the standard duplicate-session exception without sensitive details.
        /// </summary>
        private static InvalidOperationException CreateDuplicateSessionException(Exception innerException)
        {
            return new InvalidOperationException(
                "Duplicate completed quiz session detected. No new session or answers were saved.",
                innerException);
        }

        /// <summary>
        /// Returns true when an exception already describes duplicate persistence.
        /// </summary>
        private static bool IsDuplicateValidationException(Exception ex)
        {
            return ex is InvalidOperationException &&
                   ex.Message.IndexOf(
                       "Duplicate completed quiz session",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Returns true when MySQL rejected the insert through the unique duplicate-session key.
        /// </summary>
        private static bool IsDuplicateKeyException(Exception ex)
        {
            if (ex == null)
                return false;

            MySqlException mysqlException = ex as MySqlException;

            if (mysqlException != null &&
                mysqlException.Number == 1062)
            {
                return true;
            }

            return IsDuplicateKeyException(ex.InnerException);
        }

        /// <summary>
        /// Normalizes values to MySQL DATETIME second precision.
        /// </summary>
        private static DateTime TrimToSecond(DateTime value)
        {
            return new DateTime(
                value.Year,
                value.Month,
                value.Day,
                value.Hour,
                value.Minute,
                value.Second,
                value.Kind);
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Immutable validated session values used for persistence.
        /// </summary>
        private sealed class ValidatedSession
        {
            public ValidatedSession(
                TrainingSession source,
                List<QuizAnswer> answers,
                DateTime startTime,
                DateTime endTime,
                string duplicateKey)
            {
                Source = source;
                Answers = answers;
                StartTime = startTime;
                EndTime = endTime;
                DuplicateKey = duplicateKey;
            }

            public TrainingSession Source
            {
                get;
                private set;
            }

            public List<QuizAnswer> Answers
            {
                get;
                private set;
            }

            public string EmployeeNo
            {
                get
                {
                    return Source.User.EmployeeNo.Trim();
                }
            }

            public DateTime StartTime
            {
                get;
                private set;
            }

            public DateTime EndTime
            {
                get;
                private set;
            }

            public int TotalQuestions
            {
                get
                {
                    return Source.TotalQuestions;
                }
            }

            public int CorrectAnswers
            {
                get
                {
                    return Source.CorrectAnswers;
                }
            }

            public int WrongAnswers
            {
                get
                {
                    return Source.WrongAnswers;
                }
            }

            public double Accuracy
            {
                get
                {
                    return Source.Accuracy;
                }
            }

            public int AnswerCount
            {
                get
                {
                    return Answers.Count;
                }
            }

            public string DuplicateKey
            {
                get;
                private set;
            }
        }

        /// <summary>
        /// Historical session information used during duplicate-key migration.
        /// </summary>
        private sealed class ExistingSessionDuplicateInfo
        {
            public ExistingSessionDuplicateInfo(
                int sessionId,
                string duplicateKey)
            {
                SessionID = sessionId;
                DuplicateKey = duplicateKey;
            }

            public int SessionID
            {
                get;
                private set;
            }

            public string DuplicateKey
            {
                get;
                private set;
            }
        }

        #endregion
    }
}
