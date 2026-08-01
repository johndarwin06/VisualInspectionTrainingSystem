#region Namespaces

using MySql.Data.MySqlClient;
using NUnit.Framework;
using System;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Infrastructure
{
    /// <summary>
    /// Owns deterministic synthetic rows for one database test and removes only
    /// rows carrying that test's unique employee prefix.
    /// </summary>
    internal sealed class TestDatabaseRunContext : IDisposable
    {
        #region Constants

        /// <summary>Prefix reserved exclusively for permanent database-test rows.</summary>
        public const string SyntheticEmployeePrefix = "I19T";

        #endregion

        #region Fields

        private readonly TestDatabaseConfiguration _configuration;
        private bool _disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates one run identity that fits the production Employee Number limit.
        /// </summary>
        /// <param name="configuration">Validated test-only configuration.</param>
        public TestDatabaseRunContext(TestDatabaseConfiguration configuration)
        {
            _configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            RunId = SyntheticEmployeePrefix +
                    Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
        }

        #endregion

        #region Properties

        /// <summary>Gets the unique synthetic row prefix for this test.</summary>
        public string RunId
        {
            get;
            private set;
        }

        #endregion

        #region Factories

        /// <summary>Opens a marker-validated connection owned by the caller.</summary>
        public MySqlConnection OpenConnection()
        {
            ThrowIfDisposed();
            return _configuration.OpenConnection();
        }

        /// <summary>Creates a real production database service against the isolated schema.</summary>
        public MySqlService CreateDatabaseService()
        {
            ThrowIfDisposed();
            return _configuration.CreateDatabaseService();
        }

        /// <summary>Builds a unique employee number with an optional short suffix.</summary>
        public string Employee(string suffix)
        {
            string value = RunId + (suffix ?? string.Empty).Trim().ToUpperInvariant();

            if (value.Length > 20)
                throw new ArgumentOutOfRangeException(nameof(suffix));

            return value;
        }

        /// <summary>Builds a stable 64-character hexadecimal image hash.</summary>
        public string ImageHash(string suffix)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(
                    RunId + "|" + (suffix ?? string.Empty));
                return BitConverter.ToString(algorithm.ComputeHash(bytes))
                    .Replace("-", string.Empty);
            }
        }

        #endregion

        #region Synthetic Inserts

        /// <summary>Inserts one run-owned user and returns its identity.</summary>
        public int InsertUser(
            string suffix,
            string passwordHash,
            string role,
            object isActive)
        {
            const string sql = @"
INSERT INTO tbl_users
(
    EmployeeNo,
    FullName,
    PasswordHash,
    Role,
    Department,
    IsActive,
    CreatedDate
)
VALUES
(
    @EmployeeNo,
    @FullName,
    @PasswordHash,
    @Role,
    @Department,
    @IsActive,
    @CreatedDate
);";

            using (MySqlConnection connection = OpenConnection())
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeeNo", Employee(suffix));
                command.Parameters.AddWithValue("@FullName", "Synthetic Test User");
                command.Parameters.AddWithValue("@PasswordHash", passwordHash);
                command.Parameters.AddWithValue("@Role", role);
                command.Parameters.AddWithValue("@Department", "TEST-ONLY");
                command.Parameters.AddWithValue(
                    "@IsActive",
                    isActive ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CreatedDate", TrimToSecond(DateTime.UtcNow));
                command.ExecuteNonQuery();
                return Convert.ToInt32(command.LastInsertedId);
            }
        }

        /// <summary>Inserts one run-owned session and returns its identity.</summary>
        public int InsertSession(
            string employeeNo,
            DateTime startTime,
            DateTime? endTime,
            int totalQuestions,
            string duplicateKey)
        {
            const string sql = @"
INSERT INTO tbl_training_session
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
    0,
    0,
    0,
    @DuplicateKey
);";

            using (MySqlConnection connection = OpenConnection())
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeeNo", employeeNo);
                command.Parameters.AddWithValue("@StartTime", TrimToSecond(startTime));
                command.Parameters.AddWithValue(
                    "@EndTime",
                    endTime.HasValue
                        ? (object)TrimToSecond(endTime.Value)
                        : DBNull.Value);
                command.Parameters.AddWithValue("@TotalQuestions", totalQuestions);
                command.Parameters.AddWithValue(
                    "@DuplicateKey",
                    string.IsNullOrWhiteSpace(duplicateKey)
                        ? (object)DBNull.Value
                        : duplicateKey);
                command.ExecuteNonQuery();
                return Convert.ToInt32(command.LastInsertedId);
            }
        }

        /// <summary>Inserts one run-owned answer and returns its identity.</summary>
        public int InsertAnswer(
            int sessionId,
            int imageId,
            string imageHash,
            string userAnswer,
            string correctAnswer,
            bool? isCorrect,
            string reviewSource,
            string reviewedBy)
        {
            const string sql = @"
INSERT INTO tbl_quiz_answer
(
    SessionID,
    ImageID,
    ImageHash,
    ImageFileName,
    UserAnswer,
    CorrectAnswer,
    IsCorrect,
    AnswerTime,
    ReviewSource,
    ReviewedAt,
    ReviewedBy
)
VALUES
(
    @SessionID,
    @ImageID,
    @ImageHash,
    @ImageFileName,
    @UserAnswer,
    @CorrectAnswer,
    @IsCorrect,
    @AnswerTime,
    @ReviewSource,
    @ReviewedAt,
    @ReviewedBy
);";

            using (MySqlConnection connection = OpenConnection())
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@SessionID", sessionId);
                command.Parameters.AddWithValue("@ImageID", imageId);
                command.Parameters.AddWithValue(
                    "@ImageHash",
                    string.IsNullOrWhiteSpace(imageHash)
                        ? (object)DBNull.Value
                        : imageHash);
                command.Parameters.AddWithValue(
                    "@ImageFileName",
                    RunId + "-" + imageId.ToString(CultureInfo.InvariantCulture) + ".png");
                command.Parameters.AddWithValue(
                    "@UserAnswer",
                    userAnswer == null ? (object)DBNull.Value : userAnswer);
                command.Parameters.AddWithValue(
                    "@CorrectAnswer",
                    correctAnswer == null ? (object)DBNull.Value : correctAnswer);
                command.Parameters.AddWithValue(
                    "@IsCorrect",
                    isCorrect.HasValue ? (object)isCorrect.Value : DBNull.Value);
                command.Parameters.AddWithValue("@AnswerTime", TrimToSecond(DateTime.UtcNow));
                command.Parameters.AddWithValue(
                    "@ReviewSource",
                    reviewSource == null ? (object)DBNull.Value : reviewSource);
                command.Parameters.AddWithValue(
                    "@ReviewedAt",
                    correctAnswer == null ? (object)DBNull.Value : TrimToSecond(DateTime.UtcNow));
                command.Parameters.AddWithValue(
                    "@ReviewedBy",
                    reviewedBy == null ? (object)DBNull.Value : reviewedBy);
                command.ExecuteNonQuery();
                return Convert.ToInt32(command.LastInsertedId);
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Deletes only rows owned by this run in foreign-key-safe order.
        /// </summary>
        public void Cleanup()
        {
            if (_disposed)
                return;

            using (MySqlConnection connection = _configuration.OpenConnection())
            using (MySqlTransaction transaction = connection.BeginTransaction(
                IsolationLevel.ReadCommitted))
            {
                try
                {
                    DeleteOwnedTruth(connection, transaction, RunId + "%");
                    DeleteOwnedAnswers(connection, transaction, RunId + "%");
                    DeleteOwnedSessions(connection, transaction, RunId + "%");
                    DeleteOwnedUsers(connection, transaction, RunId + "%");
                    transaction.Commit();
                }
                catch
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch
                    {
                        // Preserve the cleanup failure that initiated rollback.
                    }

                    throw;
                }
            }
        }

        /// <summary>Counts run-owned rows across every permanent application table.</summary>
        public int CountOwnedRows()
        {
            return CountSyntheticRows(_configuration, RunId + "%");
        }

        /// <summary>Counts every residual row using the reserved Issue #19 prefix.</summary>
        public static int CountAllSyntheticRows(
            TestDatabaseConfiguration configuration)
        {
            return CountSyntheticRows(
                configuration,
                SyntheticEmployeePrefix + "%");
        }

        private static int CountSyntheticRows(
            TestDatabaseConfiguration configuration,
            string employeePattern)
        {
            const string sql = @"
SELECT
    (SELECT COUNT(*)
     FROM tbl_users
     WHERE EmployeeNo LIKE @EmployeePattern) +
    (SELECT COUNT(*)
     FROM tbl_training_session
     WHERE EmployeeNo LIKE @EmployeePattern) +
    (SELECT COUNT(*)
     FROM tbl_quiz_answer a
     INNER JOIN tbl_training_session s ON s.SessionID = a.SessionID
     WHERE s.EmployeeNo LIKE @EmployeePattern) +
    (SELECT COUNT(*)
     FROM tbl_image_review_truth t
     WHERE t.ReviewerEmployeeNo LIKE @EmployeePattern
        OR t.SourceAnswerID IN
           (
               SELECT a.AnswerID
               FROM tbl_quiz_answer a
               INNER JOIN tbl_training_session s ON s.SessionID = a.SessionID
               WHERE s.EmployeeNo LIKE @EmployeePattern
           )
        OR t.ImageHash IN
           (
               SELECT a.ImageHash
               FROM tbl_quiz_answer a
               INNER JOIN tbl_training_session s ON s.SessionID = a.SessionID
               WHERE s.EmployeeNo LIKE @EmployeePattern
                 AND a.ImageHash IS NOT NULL
           ));";

            using (MySqlConnection connection = configuration.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeePattern", employeePattern);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static void DeleteOwnedTruth(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string employeePattern)
        {
            const string sql = @"
DELETE FROM tbl_image_review_truth
WHERE ReviewerEmployeeNo LIKE @EmployeePattern
   OR SourceAnswerID IN
      (
          SELECT a.AnswerID
          FROM tbl_quiz_answer a
          INNER JOIN tbl_training_session s ON s.SessionID = a.SessionID
          WHERE s.EmployeeNo LIKE @EmployeePattern
      )
   OR ImageHash IN
      (
          SELECT a.ImageHash
          FROM tbl_quiz_answer a
          INNER JOIN tbl_training_session s ON s.SessionID = a.SessionID
          WHERE s.EmployeeNo LIKE @EmployeePattern
            AND a.ImageHash IS NOT NULL
      );";

            ExecuteOwnedDelete(connection, transaction, sql, employeePattern);
        }

        private static void DeleteOwnedAnswers(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string employeePattern)
        {
            const string sql = @"
DELETE a
FROM tbl_quiz_answer a
INNER JOIN tbl_training_session s ON s.SessionID = a.SessionID
WHERE s.EmployeeNo LIKE @EmployeePattern;";

            ExecuteOwnedDelete(connection, transaction, sql, employeePattern);
        }

        private static void DeleteOwnedSessions(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string employeePattern)
        {
            const string sql = @"
DELETE FROM tbl_training_session
WHERE EmployeeNo LIKE @EmployeePattern;";

            ExecuteOwnedDelete(connection, transaction, sql, employeePattern);
        }

        private static void DeleteOwnedUsers(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string employeePattern)
        {
            const string sql = @"
DELETE FROM tbl_users
WHERE EmployeeNo LIKE @EmployeePattern;";

            ExecuteOwnedDelete(connection, transaction, sql, employeePattern);
        }

        private static void ExecuteOwnedDelete(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string sql,
            string employeePattern)
        {
            using (MySqlCommand command = new MySqlCommand(
                sql,
                connection,
                transaction))
            {
                command.Parameters.AddWithValue(
                    "@EmployeePattern",
                    employeePattern);
                command.ExecuteNonQuery();
            }
        }

        #endregion

        #region IDisposable

        /// <summary>Removes run-owned rows and prevents further use.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            Cleanup();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TestDatabaseRunContext));
        }

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

    /// <summary>
    /// Establishes one isolated synthetic run per functional database test and
    /// verifies cleanup even when the test body fails.
    /// </summary>
    public abstract class DatabaseTestFixtureBase
    {
        #region Properties

        private protected TestDatabaseConfiguration Configuration
        {
            get;
            private set;
        }

        private protected TestDatabaseRunContext Run
        {
            get;
            private set;
        }

        #endregion

        #region NUnit Lifecycle

        /// <summary>Validates the marker, upgrades schema deterministically, and starts a unique run.</summary>
        [SetUp]
        public void SetUpDatabaseRun()
        {
            Configuration = TestDatabaseConfiguration.Require();
            TestDatabaseSchema.EnsureCurrent(Configuration);
            Run = new TestDatabaseRunContext(Configuration);
        }

        /// <summary>Removes only current-run rows and asserts exact cleanup.</summary>
        [TearDown]
        public void TearDownDatabaseRun()
        {
            if (Run == null)
                return;

            Run.Cleanup();
            Assert.That(
                Run.CountOwnedRows(),
                Is.Zero,
                "The database test left synthetic rows owned by its run identifier.");
            Run.Dispose();
            Run = null;
        }

        #endregion
    }
}
