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
    /// Provides database access for saved quiz answers.
    /// </summary>
    public class AnswerRepository
    {
        #region Constants

        private const string TableName = "tbl_training_answers";

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
CREATE TABLE IF NOT EXISTS tbl_training_answers
(
    AnswerID INT NOT NULL AUTO_INCREMENT,
    SessionID INT NOT NULL,
    Sequence INT NOT NULL,
    ImageID INT NOT NULL,
    FileName VARCHAR(255) NULL,
    FilePath VARCHAR(1000) NULL,
    UserAnswer VARCHAR(20) NOT NULL,
    CorrectAnswer VARCHAR(20) NULL,
    IsCorrect BIT NOT NULL DEFAULT 0,
    AnswerTime DATETIME NOT NULL,
    ElapsedSeconds DOUBLE NOT NULL DEFAULT 0,
    CreatedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (AnswerID),
    INDEX IX_TrainingAnswers_SessionID (SessionID),
    INDEX IX_TrainingAnswers_ImageID (ImageID)
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
    Sequence,
    ImageID,
    FileName,
    FilePath,
    UserAnswer,
    CorrectAnswer,
    IsCorrect,
    AnswerTime,
    ElapsedSeconds
)
VALUES
(
    @SessionID,
    @Sequence,
    @ImageID,
    @FileName,
    @FilePath,
    @UserAnswer,
    @CorrectAnswer,
    @IsCorrect,
    @AnswerTime,
    @ElapsedSeconds
);";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@SessionID", sessionId);
                command.Parameters.AddWithValue("@Sequence", answer.Sequence);
                command.Parameters.AddWithValue("@ImageID", answer.ImageID);
                command.Parameters.AddWithValue("@FileName", ToDbValue(answer.FileName));
                command.Parameters.AddWithValue("@FilePath", ToDbValue(answer.FilePath));
                command.Parameters.AddWithValue("@UserAnswer", GetAnswerText(answer.UserAnswer));
                command.Parameters.AddWithValue("@CorrectAnswer", GetNullableAnswerText(answer.CorrectAnswer));
                command.Parameters.AddWithValue("@IsCorrect", answer.IsCorrect);
                command.Parameters.AddWithValue("@AnswerTime", answer.AnswerTime);
                command.Parameters.AddWithValue("@ElapsedSeconds", answer.ElapsedSeconds);

                command.ExecuteNonQuery();
            }
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

        /// <summary>
        /// Converts an answer enum to the stored text value.
        /// </summary>
        private static string GetAnswerText(QuizAnswerType answer)
        {
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

        #endregion
    }
}
