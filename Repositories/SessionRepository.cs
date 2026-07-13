#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
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

                if (IsDuplicateException(ex))
                {
                    throw;
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
        /// Creates the session table when it does not exist.
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
    FOREIGN KEY(EmployeeNo)
        REFERENCES tbl_users(EmployeeNo)
);";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.ExecuteNonQuery();
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

            return new ValidatedSession(
                session,
                answers,
                TrimToSecond(session.Started),
                TrimToSecond(session.Finished.Value));
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

            const string sql = @"
SELECT s.SessionID
FROM tbl_training_session s
WHERE s.EmployeeNo = @EmployeeNo
  AND ABS(TIMESTAMPDIFF(SECOND, s.StartTime, @StartTime)) = 0
  AND s.EndTime IS NOT NULL
  AND ABS(TIMESTAMPDIFF(SECOND, s.EndTime, @EndTime)) = 0
  AND s.TotalQuestions = @TotalQuestions
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
                command.Parameters.AddWithValue("@AnswerCount", session.Answers.Count);

                object result = command.ExecuteScalar();

                if (result != null &&
                    result != DBNull.Value)
                {
                    throw new InvalidOperationException(
                        "Duplicate completed quiz session detected. No new session or answers were saved.");
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
    Accuracy
)
VALUES
(
    @EmployeeNo,
    @StartTime,
    @EndTime,
    @TotalQuestions,
    @CorrectAnswers,
    @WrongAnswers,
    @Accuracy
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

                command.ExecuteNonQuery();

                return Convert.ToInt32(command.LastInsertedId);
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
        /// Returns true when an exception already describes duplicate persistence.
        /// </summary>
        private static bool IsDuplicateException(Exception ex)
        {
            return ex is InvalidOperationException &&
                   ex.Message.IndexOf(
                       "Duplicate completed quiz session",
                       StringComparison.OrdinalIgnoreCase) >= 0;
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
                DateTime endTime)
            {
                Source = source;
                Answers = answers;
                StartTime = startTime;
                EndTime = endTime;
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
        }

        #endregion
    }
}
