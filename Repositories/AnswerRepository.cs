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
        /// Assigns the correct answer for one saved quiz answer.
        /// </summary>
        public void ReviewAnswer(
            int answerId,
            QuizAnswerType correctAnswer)
        {
            if (answerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(answerId));

            int sessionId = GetSessionId(answerId);

            if (sessionId <= 0)
                throw new InvalidOperationException("Answer was not found.");

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

            try
            {
                _database.ExecuteNonQuery(
                    sql,
                    new MySqlParameter("@CorrectAnswer", correctAnswerText),
                    new MySqlParameter("@AnswerID", answerId));
            }
            finally
            {
                _database.CloseConnection();
            }

            RecalculateSession(sessionId);
        }

        /// <summary>
        /// Saves all answers for one training session.
        /// </summary>
        public void SaveMany(
            int sessionId,
            IEnumerable<QuizAnswer> answers)
        {
            if (sessionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sessionId));

            using (MySqlTransaction transaction = _database.BeginTransaction())
            {
                try
                {
                    EnsureTable(
                        _database.GetConnection(),
                        transaction);

                    SaveMany(
                        sessionId,
                        answers,
                        _database.GetConnection(),
                        transaction);

                    transaction.Commit();
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

        #region Internal Methods

        /// <summary>
        /// Creates the answer table when it does not exist.
        /// </summary>
        internal void EnsureTable(
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
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

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Saves answers using an existing transaction.
        /// </summary>
        internal void SaveMany(
            int sessionId,
            IEnumerable<QuizAnswer> answers,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            if (answers == null)
                return;

            foreach (QuizAnswer answer in answers)
            {
                if (answer == null)
                    continue;

                Save(
                    sessionId,
                    answer,
                    connection,
                    transaction);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Maps one database row to a quiz answer.
        /// </summary>
        private static QuizAnswer MapAnswer(DataRow row)
        {
            QuizAnswer answer = new QuizAnswer
            {
                AnswerID = Convert.ToInt32(row["AnswerID"]),

                SessionID = Convert.ToInt32(row["SessionID"]),

                EmployeeNo = row["EmployeeNo"].ToString(),

                ImageID = Convert.ToInt32(row["ImageID"]),

                UserAnswer = ParseAnswer(row["UserAnswer"]),

                CorrectAnswer = ParseNullableAnswer(row["CorrectAnswer"]),

                IsCorrect = ToBoolean(row["IsCorrect"]),

                AnswerTime = Convert.ToDateTime(row["AnswerTime"])
            };

            return answer;
        }

        /// <summary>
        /// Returns the parent session ID for an answer.
        /// </summary>
        private int GetSessionId(int answerId)
        {
            const string sql = @"
SELECT SessionID
FROM tbl_quiz_answer
WHERE AnswerID = @AnswerID
LIMIT 1;";

            try
            {
                object result = _database.ExecuteScalar(
                    sql,
                    new MySqlParameter("@AnswerID", answerId));

                if (result == null ||
                    result == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(result);
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Updates the summary columns for one training session.
        /// </summary>
        private void RecalculateSession(int sessionId)
        {
            const string sql = @"
UPDATE tbl_training_session
SET
    CorrectAnswers =
    (
        SELECT COUNT(*)
        FROM tbl_quiz_answer
        WHERE SessionID = @SessionID
          AND CorrectAnswer IS NOT NULL
          AND IsCorrect = 1
    ),
    WrongAnswers =
    (
        SELECT COUNT(*)
        FROM tbl_quiz_answer
        WHERE SessionID = @SessionID
          AND CorrectAnswer IS NOT NULL
          AND IsCorrect = 0
    ),
    Accuracy =
    (
        SELECT
            CASE
                WHEN COUNT(*) = 0 THEN 0
                ELSE ROUND(SUM(CASE WHEN IsCorrect = 1 THEN 1 ELSE 0 END) / COUNT(*) * 100, 2)
            END
        FROM tbl_quiz_answer
        WHERE SessionID = @SessionID
          AND CorrectAnswer IS NOT NULL
    )
WHERE SessionID = @SessionID;";

            try
            {
                _database.ExecuteNonQuery(
                    sql,
                    new MySqlParameter("@SessionID", sessionId));
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Saves one answer using an existing transaction.
        /// </summary>
        private void Save(
            int sessionId,
            QuizAnswer answer,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            string sql = $@"
INSERT INTO {TableName}
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
                command.Parameters.AddWithValue("@AnswerTime", answer.AnswerTime);

                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Converts an answer enum to the stored text value.
        /// </summary>
        private static string GetAnswerText(QuizAnswerType answer)
        {
            return answer.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// Parses a stored answer value.
        /// </summary>
        private static QuizAnswerType ParseAnswer(object value)
        {
            QuizAnswerType answer;

            if (value != null &&
                value != DBNull.Value &&
                Enum.TryParse(
                    value.ToString(),
                    true,
                    out answer))
            {
                return answer;
            }

            return QuizAnswerType.Good;
        }

        /// <summary>
        /// Parses a nullable stored answer value.
        /// </summary>
        private static QuizAnswerType? ParseNullableAnswer(object value)
        {
            QuizAnswerType answer;

            if (value != null &&
                value != DBNull.Value &&
                Enum.TryParse(
                    value.ToString(),
                    true,
                    out answer))
            {
                return answer;
            }

            return null;
        }

        /// <summary>
        /// Converts database bit values to Boolean.
        /// </summary>
        private static bool ToBoolean(object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return false;
            }

            return Convert.ToBoolean(value);
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

        #endregion
    }
}
