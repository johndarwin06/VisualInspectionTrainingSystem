#region Namespaces

using MySql.Data.MySqlClient;
using System;
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
            ValidateSession(session);

            MySqlTransaction transaction = null;
            int sessionId = 0;

            try
            {
                _database.OpenConnection();

                MySqlConnection connection = _database.GetConnection();

                EnsureTable(connection);

                _answerRepository.EnsureTable(connection);

                transaction = connection.BeginTransaction();

                sessionId = InsertSession(
                    session,
                    connection,
                    transaction);

                session.SessionID = sessionId;

                _answerRepository.SaveMany(
                    sessionId,
                    session.Answers,
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

                if (sessionId > 0 &&
                    session != null)
                {
                    session.SessionID = 0;
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

        #region Private Methods

        /// <summary>
        /// Validates a completed training session before persistence.
        /// </summary>
        private static void ValidateSession(TrainingSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (session.User == null)
                throw new InvalidOperationException("Training session has no user.");

            if (string.IsNullOrWhiteSpace(session.User.EmployeeNo))
                throw new InvalidOperationException("Training session user has no EmployeeNo.");
        }

        /// <summary>
        /// Inserts the session header and returns the generated identity.
        /// </summary>
        private int InsertSession(
            TrainingSession session,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            string sql = $@"
INSERT INTO {TableName}
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
                command.Parameters.AddWithValue("@EmployeeNo", session.User.EmployeeNo);
                command.Parameters.AddWithValue("@StartTime", session.Started);
                command.Parameters.AddWithValue("@EndTime", GetEndTimeValue(session));
                command.Parameters.AddWithValue("@TotalQuestions", session.TotalQuestions);
                command.Parameters.AddWithValue("@CorrectAnswers", session.CorrectAnswers);
                command.Parameters.AddWithValue("@WrongAnswers", session.WrongAnswers);
                command.Parameters.AddWithValue("@Accuracy", session.Accuracy);

                command.ExecuteNonQuery();

                return Convert.ToInt32(command.LastInsertedId);
            }
        }

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
        /// Converts session finish time to a database value.
        /// </summary>
        private static object GetEndTimeValue(TrainingSession session)
        {
            if (session.Finished.HasValue)
                return session.Finished.Value;

            return DBNull.Value;
        }

        #endregion
    }
}
