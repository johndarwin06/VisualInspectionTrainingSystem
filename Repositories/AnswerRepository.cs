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
    /// Provides database access for saved quiz answers.
    /// Matches the existing tbl_quiz_answer schema.
    /// </summary>
    public class AnswerRepository
    {
        #region Constants

        private const string TableName = "tbl_quiz_answer";

        #endregion

        #region Fields

        private readonly MySqlService _database;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the answer repository.
        /// </summary>
        public AnswerRepository()
            : this(new MySqlService())
        {
        }

        internal AnswerRepository(MySqlService database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads saved answers for admin review.
        /// </summary>
        public List<QuizAnswer> GetForReview()
        {
            const string sql = @"
SELECT
    a.AnswerID,
    a.SessionID,
    s.EmployeeNo,
    a.ImageID,
    a.UserAnswer,
    a.CorrectAnswer,
    a.IsCorrect,
    a.AnswerTime
FROM tbl_quiz_answer a
INNER JOIN tbl_training_session s
    ON s.SessionID = a.SessionID
ORDER BY a.AnswerTime DESC, a.AnswerID DESC;";

            try
            {
                DataTable table = _database.ExecuteDataTable(sql);

                List<QuizAnswer> answers = new List<QuizAnswer>();

                foreach (DataRow row in table.Rows)
                {
                    answers.Add(MapAnswer(row));
                }

                return answers;
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Assigns the correct answer and recalculates the parent session atomically.
        /// </summary>
        public void ReviewAnswer(
            int answerId,
            QuizAnswerType correctAnswer)
        {
            if (answerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(answerId));

            ValidateAnswerType(
                correctAnswer,
                nameof(correctAnswer));

            MySqlTransaction transaction = null;

            try
            {
                _database.OpenConnection();

                MySqlConnection connection = _database.GetConnection();

                transaction = connection.BeginTransaction();

                int sessionId = GetSessionId(
                    answerId,
                    connection,
                    transaction);

                if (sessionId <= 0)
                    throw new InvalidOperationException("Answer was not found.");

                UpdateReviewedAnswer(
                    answerId,
                    correctAnswer,
                    connection,
                    transaction);

                RecalculateSession(
                    sessionId,
                    connection,
                    transaction);

                transaction.Commit();
                transaction = null;
            }
            catch (Exception ex)
            {
                RollbackTransaction(
                    transaction,
                    "admin answer review",
                    ex);

                throw new InvalidOperationException(
                    "Failed to review the selected answer. The review update and session recalculation were rolled back.",
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

        /// <summary>
        /// Saves all answers for one training session atomically.
        /// </summary>
        public void SaveMany(
            int sessionId,
            IEnumerable<QuizAnswer> answers)
        {
            if (sessionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sessionId));

            List<QuizAnswer> validatedAnswers =
                ValidateAnswersForPersistence(answers);

            MySqlTransaction transaction = null;

            try
            {
                _database.OpenConnection();

                MySqlConnection connection = _database.GetConnection();

                EnsureTable(connection);

                transaction = connection.BeginTransaction();

                SaveMany(
                    sessionId,
                    validatedAnswers,
                    connection,
                    transaction);

                transaction.Commit();
                transaction = null;
            }
            catch (Exception ex)
            {
                RollbackTransaction(
                    transaction,
                    "answer persistence",
                    ex);

                throw new InvalidOperationException(
                    "Failed to save quiz answers. The answer insert transaction was rolled back.",
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
        /// Creates the answer table when it does not exist.
        /// DDL is intentionally executed outside data transactions.
        /// </summary>
        internal void EnsureTable(MySqlConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            const string sql = @"
CREATE TABLE IF NOT EXISTS tbl_quiz_answer
(
    AnswerID INT AUTO_INCREMENT PRIMARY KEY,
    SessionID INT NOT NULL,
    ImageID INT NOT NULL,
    UserAnswer VARCHAR(10),
    CorrectAnswer VARCHAR(10),
    IsCorrect BIT,
    AnswerTime DATETIME,
    FOREIGN KEY(SessionID)
        REFERENCES tbl_training_session(SessionID)
);";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Saves answers using an existing connection and transaction.
        /// </summary>
        internal void SaveMany(
            int sessionId,
            IEnumerable<QuizAnswer> answers,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            if (sessionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sessionId));

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            List<QuizAnswer> validatedAnswers =
                ValidateAnswersForPersistence(answers);

            foreach (QuizAnswer answer in validatedAnswers)
            {
                Save(
                    sessionId,
                    answer,
                    connection,
                    transaction);
            }
        }

        /// <summary>
        /// Validates one answer before it reaches MySQL.
        /// </summary>
        internal static void ValidateAnswerForPersistence(
            QuizAnswer answer,
            string parameterName)
        {
            if (answer == null)
                throw new ArgumentNullException(parameterName);

            if (answer.ImageID <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "ImageID must be greater than zero.");
            }

            ValidateAnswerType(
                answer.UserAnswer,
                parameterName + ".UserAnswer");

            if (answer.CorrectAnswer.HasValue)
            {
                ValidateAnswerType(
                    answer.CorrectAnswer.Value,
                    parameterName + ".CorrectAnswer");

                bool expectedIsCorrect =
                    answer.UserAnswer == answer.CorrectAnswer.Value;

                if (answer.IsCorrect != expectedIsCorrect)
                {
                    throw new ArgumentException(
                        "IsCorrect must match UserAnswer and CorrectAnswer.",
                        parameterName);
                }
            }
            else if (answer.IsCorrect)
            {
                throw new ArgumentException(
                    "CorrectAnswer can be null only for pending review answers.",
                    parameterName);
            }

            if (answer.AnswerTime == DateTime.MinValue)
            {
                throw new ArgumentException(
                    "AnswerTime is required.",
                    parameterName);
            }

            if (answer.ElapsedSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "ElapsedSeconds must not be negative.");
            }
        }

        #endregion

        #region Mapping

        /// <summary>
        /// Maps one database row to a quiz answer.
        /// </summary>
        private static QuizAnswer MapAnswer(DataRow row)
        {
            if (row == null)
                throw new ArgumentNullException(nameof(row));

            QuizAnswerType? correctAnswer =
                ReadNullableAnswer(row, "CorrectAnswer");

            QuizAnswer answer = new QuizAnswer
            {
                AnswerID = ReadRequiredInt(row, "AnswerID"),

                SessionID = ReadRequiredInt(row, "SessionID"),

                EmployeeNo = ReadRequiredString(row, "EmployeeNo"),

                ImageID = ReadRequiredInt(row, "ImageID"),

                UserAnswer = ReadRequiredAnswer(row, "UserAnswer"),

                CorrectAnswer = correctAnswer,

                IsCorrect = ReadIsCorrect(row, correctAnswer.HasValue),

                AnswerTime = ReadRequiredDate(row, "AnswerTime")
            };

            return answer;
        }

        #endregion

        #region Transaction Helpers

        /// <summary>
        /// Returns the parent session ID for an answer using the active transaction.
        /// </summary>
        private static int GetSessionId(
            int answerId,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            if (answerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(answerId));

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            const string sql = @"
SELECT SessionID
FROM tbl_quiz_answer
WHERE AnswerID = @AnswerID
LIMIT 1
FOR UPDATE;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@AnswerID", answerId);

                object result = command.ExecuteScalar();

                if (result == null ||
                    result == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(result);
            }
        }

        /// <summary>
        /// Updates one reviewed answer inside the active transaction.
        /// </summary>
        private static void UpdateReviewedAnswer(
            int answerId,
            QuizAnswerType correctAnswer,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            if (answerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(answerId));

            ValidateAnswerType(
                correctAnswer,
                nameof(correctAnswer));

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            string correctAnswerText = GetAnswerText(correctAnswer);

            const string sql = @"
UPDATE tbl_quiz_answer
SET
    CorrectAnswer = @CorrectAnswer,
    IsCorrect = CASE
        WHEN UPPER(UserAnswer) = @CorrectAnswer THEN 1
        ELSE 0
    END
WHERE AnswerID = @AnswerID;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@CorrectAnswer", correctAnswerText);
                command.Parameters.AddWithValue("@AnswerID", answerId);

                int affectedRows = command.ExecuteNonQuery();

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException(
                        "Answer review update did not affect exactly one row.");
                }
            }
        }

        /// <summary>
        /// Updates the summary columns for one training session inside the active transaction.
        /// </summary>
        private static void RecalculateSession(
            int sessionId,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            if (sessionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sessionId));

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            EnsureSessionExists(
                sessionId,
                connection,
                transaction);

            const string sql = @"
UPDATE tbl_training_session s
LEFT JOIN
(
    SELECT
        SessionID,
        SUM(CASE WHEN CorrectAnswer IS NOT NULL AND IsCorrect = 1 THEN 1 ELSE 0 END) AS CorrectAnswers,
        SUM(CASE WHEN CorrectAnswer IS NOT NULL AND IsCorrect = 0 THEN 1 ELSE 0 END) AS WrongAnswers,
        SUM(CASE WHEN CorrectAnswer IS NOT NULL THEN 1 ELSE 0 END) AS ReviewedAnswers
    FROM tbl_quiz_answer
    WHERE SessionID = @SessionID
    GROUP BY SessionID
) answerTotals
    ON answerTotals.SessionID = s.SessionID
SET
    s.CorrectAnswers = IFNULL(answerTotals.CorrectAnswers, 0),
    s.WrongAnswers = IFNULL(answerTotals.WrongAnswers, 0),
    s.Accuracy = CASE
        WHEN IFNULL(answerTotals.ReviewedAnswers, 0) = 0 THEN 0
        ELSE ROUND(IFNULL(answerTotals.CorrectAnswers, 0) / answerTotals.ReviewedAnswers * 100, 2)
    END
WHERE s.SessionID = @SessionID;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@SessionID", sessionId);

                int affectedRows = command.ExecuteNonQuery();

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException(
                        "Session recalculation did not affect exactly one row.");
                }
            }
        }

        /// <summary>
        /// Verifies and locks the parent training session before recalculation.
        /// </summary>
        private static void EnsureSessionExists(
            int sessionId,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
SELECT SessionID
FROM tbl_training_session
WHERE SessionID = @SessionID
LIMIT 1
FOR UPDATE;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@SessionID", sessionId);

                object result = command.ExecuteScalar();

                if (result == null ||
                    result == DBNull.Value)
                {
                    throw new InvalidOperationException(
                        "Parent training session was not found for recalculation.");
                }
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

        #endregion

        #region Save Helpers

        /// <summary>
        /// Saves one answer using an existing transaction.
        /// </summary>
        private static void Save(
            int sessionId,
            QuizAnswer answer,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            ValidateAnswerForPersistence(
                answer,
                nameof(answer));

            string sql = @"
INSERT INTO " + TableName + @"
(
    SessionID,
    ImageID,
    UserAnswer,
    CorrectAnswer,
    IsCorrect,
    AnswerTime
)
VALUES
(
    @SessionID,
    @ImageID,
    @UserAnswer,
    @CorrectAnswer,
    @IsCorrect,
    @AnswerTime
);";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@SessionID", sessionId);
                command.Parameters.AddWithValue("@ImageID", answer.ImageID);
                command.Parameters.AddWithValue("@UserAnswer", GetAnswerText(answer.UserAnswer));
                command.Parameters.AddWithValue("@CorrectAnswer", GetNullableAnswerText(answer.CorrectAnswer));
                command.Parameters.AddWithValue("@IsCorrect", answer.IsCorrect);
                command.Parameters.AddWithValue("@AnswerTime", TrimToSecond(answer.AnswerTime));

                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Validates an answer collection and materializes it once.
        /// </summary>
        private static List<QuizAnswer> ValidateAnswersForPersistence(
            IEnumerable<QuizAnswer> answers)
        {
            if (answers == null)
                throw new ArgumentNullException(nameof(answers));

            List<QuizAnswer> validatedAnswers = new List<QuizAnswer>();
            int index = 0;

            foreach (QuizAnswer answer in answers)
            {
                ValidateAnswerForPersistence(
                    answer,
                    "answers[" + index + "]");

                validatedAnswers.Add(answer);

                index++;
            }

            return validatedAnswers;
        }

        #endregion

        #region Conversion Helpers

        /// <summary>
        /// Converts an answer enum to the stored text value.
        /// </summary>
        private static string GetAnswerText(QuizAnswerType answer)
        {
            ValidateAnswerType(
                answer,
                nameof(answer));

            return answer.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// Converts a nullable answer enum to the stored text value.
        /// </summary>
        private static object GetNullableAnswerText(QuizAnswerType? answer)
        {
            if (!answer.HasValue)
                return DBNull.Value;

            return GetAnswerText(answer.Value);
        }

        /// <summary>
        /// Validates that an answer enum is supported by the application.
        /// </summary>
        private static void ValidateAnswerType(
            QuizAnswerType answer,
            string parameterName)
        {
            if (!Enum.IsDefined(typeof(QuizAnswerType), answer))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    answer,
                    "UserAnswer must be GOOD or NG.");
            }
        }

        /// <summary>
        /// Reads a required integer column.
        /// </summary>
        private static int ReadRequiredInt(
            DataRow row,
            string columnName)
        {
            object value = row[columnName];

            if (value == null ||
                value == DBNull.Value)
            {
                throw new InvalidOperationException(
                    columnName + " is required.");
            }

            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Reads a required string column.
        /// </summary>
        private static string ReadRequiredString(
            DataRow row,
            string columnName)
        {
            object value = row[columnName];

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
        /// Reads a required datetime column.
        /// </summary>
        private static DateTime ReadRequiredDate(
            DataRow row,
            string columnName)
        {
            object value = row[columnName];

            if (value == null ||
                value == DBNull.Value)
            {
                throw new InvalidOperationException(
                    columnName + " is required.");
            }

            return Convert.ToDateTime(value);
        }

        /// <summary>
        /// Reads a required user answer column.
        /// </summary>
        private static QuizAnswerType ReadRequiredAnswer(
            DataRow row,
            string columnName)
        {
            object value = row[columnName];
            QuizAnswerType answer;

            if (value == null ||
                value == DBNull.Value ||
                !Enum.TryParse(
                    value.ToString(),
                    true,
                    out answer) ||
                !Enum.IsDefined(typeof(QuizAnswerType), answer))
            {
                throw new InvalidOperationException(
                    columnName + " must be GOOD or NG.");
            }

            return answer;
        }

        /// <summary>
        /// Reads a nullable correct answer column.
        /// </summary>
        private static QuizAnswerType? ReadNullableAnswer(
            DataRow row,
            string columnName)
        {
            object value = row[columnName];
            QuizAnswerType answer;

            if (value == null ||
                value == DBNull.Value ||
                string.IsNullOrWhiteSpace(value.ToString()))
            {
                return null;
            }

            if (!Enum.TryParse(
                    value.ToString(),
                    true,
                    out answer) ||
                !Enum.IsDefined(typeof(QuizAnswerType), answer))
            {
                throw new InvalidOperationException(
                    columnName + " must be GOOD or NG when present.");
            }

            return answer;
        }

        /// <summary>
        /// Reads the nullable IsCorrect column without counting pending review as wrong.
        /// </summary>
        private static bool ReadIsCorrect(
            DataRow row,
            bool isReviewed)
        {
            object value = row["IsCorrect"];

            if (value == null ||
                value == DBNull.Value)
            {
                if (isReviewed)
                {
                    throw new InvalidOperationException(
                        "IsCorrect is required when CorrectAnswer is present.");
                }

                return false;
            }

            return Convert.ToBoolean(value);
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
    }
}
