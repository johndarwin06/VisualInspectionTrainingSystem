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
    /// </summary>
    public class SessionRepository
    {
        #region Constants

        private const string TableName = "tbl_training_sessions";

        #endregion

        #region Fields

        private readonly MySqlService _database;

        private readonly AnswerRepository _answerRepository;

        #endregion

        #region Constructors

        public SessionRepository()
        {
            _database = new MySqlService();

            _answerRepository = new AnswerRepository(_database);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Saves one completed training session and its answers.
        /// </summary>
        public int Save(TrainingSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (session.User == null)
                throw new InvalidOperationException("Training session has no user.");

            using (MySqlTransaction transaction = _database.BeginTransaction())
            {
                try
                {
                    MySqlConnection connection = _database.GetConnection();

                    EnsureTable(
                        connection,
                        transaction);

                    _answerRepository.EnsureTable(
                        connection,
                        transaction);

                    int sessionId = InsertSession(
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

                    return sessionId;
                }
                catch
                {
                    transaction.Rollback();

                    throw;
                }
                finally
                {
                    _database.CloseConnection();
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates the session table when it does not exist.
        /// </summary>
        private void EnsureTable(
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS tbl_training_sessions
(
    SessionID INT NOT NULL AUTO_INCREMENT,
    UserID INT NULL,
    EmployeeNo VARCHAR(50) NULL,
    FullName VARCHAR(255) NULL,
    Started DATETIME NOT NULL,
    Finished DATETIME NULL,
    DurationSeconds DOUBLE NOT NULL DEFAULT 0,
    TotalQuestions INT NOT NULL DEFAULT 0,
    AnsweredQuestions INT NOT NULL DEFAULT 0,
    GoodAnswers INT NOT NULL DEFAULT 0,
    NgAnswers INT NOT NULL DEFAULT 0,
    CorrectAnswers INT NOT NULL DEFAULT 0,
    WrongAnswers INT NOT NULL DEFAULT 0,
    Accuracy DOUBLE NOT NULL DEFAULT 0,
    CreatedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (SessionID),
    INDEX IX_TrainingSessions_UserID (UserID),
    INDEX IX_TrainingSessions_Started (Started)
);";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.ExecuteNonQuery();
            }
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
    UserID,
    EmployeeNo,
    FullName,
    Started,
    Finished,
    DurationSeconds,
    TotalQuestions,
    AnsweredQuestions,
    GoodAnswers,
    NgAnswers,
    CorrectAnswers,
    WrongAnswers,
    Accuracy
)
VALUES
(
    @UserID,
    @EmployeeNo,
    @FullName,
    @Started,
    @Finished,
    @DurationSeconds,
    @TotalQuestions,
    @AnsweredQuestions,
    @GoodAnswers,
    @NgAnswers,
    @CorrectAnswers,
    @WrongAnswers,
    @Accuracy
);
";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@UserID", GetUserIdValue(session.User));
                command.Parameters.AddWithValue("@EmployeeNo", ToDbValue(session.User.EmployeeNo));
                command.Parameters.AddWithValue("@FullName", ToDbValue(session.User.FullName));
                command.Parameters.AddWithValue("@Started", session.Started);
                command.Parameters.AddWithValue("@Finished", GetFinishedValue(session));
                command.Parameters.AddWithValue("@DurationSeconds", Math.Round(session.Duration.TotalSeconds, 2));
                command.Parameters.AddWithValue("@TotalQuestions", session.TotalQuestions);
                command.Parameters.AddWithValue("@AnsweredQuestions", session.AnsweredQuestions);
                command.Parameters.AddWithValue("@GoodAnswers", CountAnswers(session, QuizAnswerType.Good));
                command.Parameters.AddWithValue("@NgAnswers", CountAnswers(session, QuizAnswerType.Ng));
                command.Parameters.AddWithValue("@CorrectAnswers", session.CorrectAnswers);
                command.Parameters.AddWithValue("@WrongAnswers", session.WrongAnswers);
                command.Parameters.AddWithValue("@Accuracy", session.Accuracy);

                command.ExecuteNonQuery();

                return Convert.ToInt32(command.LastInsertedId);
            }
        }

        /// <summary>
        /// Counts answers by selected answer type.
        /// </summary>
        private static int CountAnswers(
            TrainingSession session,
            QuizAnswerType answerType)
        {
            int count = 0;

            foreach (QuizAnswer answer in session.Answers)
            {
                if (answer != null &&
                    answer.UserAnswer == answerType)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Converts the user ID to a database value.
        /// </summary>
        private static object GetUserIdValue(User user)
        {
            if (user == null ||
                user.UserID <= 0)
            {
                return DBNull.Value;
            }

            return user.UserID;
        }

        /// <summary>
        /// Converts session finish time to a database value.
        /// </summary>
        private static object GetFinishedValue(TrainingSession session)
        {
            if (session.Finished.HasValue)
                return session.Finished.Value;

            return DBNull.Value;
        }

        /// <summary>
        /// Converts nullable strings to database values.
        /// </summary>
        private static object ToDbValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DBNull.Value;

            return value;
        }

        #endregion
    }
}
