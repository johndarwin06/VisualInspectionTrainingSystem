#region Namespaces

using MySql.Data.MySqlClient;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Performance
{
    /// <summary>
    /// Measures representative production repository reads against the permanently
    /// marked test-only schema. All rows are uniquely owned and removed by the
    /// shared database-test lifecycle even when an assertion fails.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Performance)]
    [Category(TestCategories.Database)]
    [NonParallelizable]
    public sealed class DatabasePerformanceTests : DatabaseTestFixtureBase
    {
        #region Constants

        private const int TraineeCount = 500;
        private const int SessionCount = 500;
        private const int AnswersPerSession = 20;
        private const int HistorySessionCount = 120;
        private const int MeasuredSampleCount = 5;
        private const int DashboardTrendDayCount = 7;
        private const int BulkReviewUniqueImageCount = 300;
        private const string SyntheticPassword = "Performance-Test-Only!22";

        #endregion

        #region Tests

        /// <summary>
        /// Measures administrator and trainee read paths at a representative scale,
        /// verifies functional results, records safe plan metadata, and confirms
        /// that repository-owned connections are closed after every workload.
        /// </summary>
        [Test]
        public void RepresentativeRepositoryReads_AreMeasuredAndRemainFunctional()
        {
            DateTime dayStart = DateTime.Today;
            DateTime dayEnd = dayStart.AddDays(1);
            string administrator = Run.Employee("A");
            string primaryTrainee = Run.Employee("T000");
            ProductionFingerprint productionBefore =
                CaptureProductionFingerprint();

            try
            {
                string passwordHash = new PasswordHashService().HashPassword(
                    SyntheticPassword);
                SeedRepresentativeWorkload(dayStart, passwordHash);
                WriteDatabaseVersion();
                WriteRepresentativeQueryPlans(administrator, primaryTrainee);

                using (MySqlService database = Run.CreateDatabaseService())
                {
                    MeasureAuthentication(database, administrator, primaryTrainee);
                    MeasureDashboard(database, dayStart, dayEnd);
                    MeasureReports(database, dayStart);
                    MeasureReviewQueue(database);
                    MeasureBulkReview(database, administrator);
                    MeasureUserManagement(database, administrator);
                    MeasureTrainingHistory(database, primaryTrainee, dayStart, dayEnd);
                }

                Assert.That(
                    Run.CountOwnedRows(),
                    Is.EqualTo(1 + TraineeCount + SessionCount +
                               (SessionCount * AnswersPerSession) +
                               BulkReviewUniqueImageCount),
                    "The representative workload must remain intact until teardown cleanup.");
            }
            finally
            {
                ProductionFingerprint productionAfter =
                    CaptureProductionFingerprint();

                TestContext.Progress.WriteLine(
                    "RESOURCE|ProductionFingerprint|SchemaHash={0}|RowCountHash={1}|Stable={2}|SyntheticRows={3}",
                    productionAfter.SchemaHash,
                    productionAfter.RowCountHash,
                    productionBefore.Matches(productionAfter),
                    productionAfter.SyntheticRowCount);

                Assert.Multiple(delegate
                {
                    Assert.That(productionBefore.SchemaHash, Is.EqualTo(productionAfter.SchemaHash));
                    Assert.That(productionBefore.RowCountHash, Is.EqualTo(productionAfter.RowCountHash));
                    Assert.That(productionBefore.SyntheticRowCount, Is.Zero);
                    Assert.That(productionAfter.SyntheticRowCount, Is.Zero);
                });
            }
        }

        /// <summary>
        /// Guards the production dashboard convention: an exact seven-local-day
        /// half-open trend ending at the next local midnight.
        /// </summary>
        [Test]
        public void DashboardTrendRange_UsesSupportedSevenLocalDayBoundaries()
        {
            DateTime dayEnd = DateTime.Today.AddDays(1);
            DateTime trendStart = CreateDashboardTrendStart(dayEnd);

            Assert.Multiple(delegate
            {
                Assert.That(dayEnd.TimeOfDay, Is.EqualTo(TimeSpan.Zero));
                Assert.That(trendStart.TimeOfDay, Is.EqualTo(TimeSpan.Zero));
                Assert.That(
                    (dayEnd - trendStart).TotalDays,
                    Is.EqualTo(DashboardTrendDayCount));
                Assert.That(trendStart, Is.EqualTo(dayEnd.AddDays(-7)));
            });
        }

        #endregion

        #region Repository Workloads

        private static void MeasureAuthentication(
            MySqlService database,
            string administrator,
            string trainee)
        {
            UserRepository repository = new UserRepository(database);
            PasswordHashService passwordHashService = new PasswordHashService();
            User administratorResult = null;
            User traineeResult = null;
            bool administratorVerified = false;
            bool traineeVerified = false;

            PerformanceMeasurement.Measure(
                "Database.Authentication.Administrator",
                1,
                MeasuredSampleCount,
                delegate
                {
                    administratorResult = repository.GetByEmployeeNo(administrator);
                    administratorVerified = administratorResult != null &&
                        passwordHashService.VerifyPassword(
                            SyntheticPassword,
                            administratorResult.PasswordHash);
                });

            PerformanceMeasurement.Measure(
                "Database.Authentication.Trainee",
                1,
                MeasuredSampleCount,
                delegate
                {
                    traineeResult = repository.GetByEmployeeNo(trainee);
                    traineeVerified = traineeResult != null &&
                        passwordHashService.VerifyPassword(
                            SyntheticPassword,
                            traineeResult.PasswordHash);
                });

            Assert.Multiple(delegate
            {
                Assert.That(administratorVerified, Is.True);
                Assert.That(traineeVerified, Is.True);
                Assert.That(administratorResult.Role, Is.EqualTo(UserRoles.Admin));
                Assert.That(traineeResult.Role, Is.EqualTo(UserRoles.User));
                Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
            });
        }

        private static void MeasureDashboard(
            MySqlService database,
            DateTime dayStart,
            DateTime dayEnd)
        {
            DashboardRepository repository = new DashboardRepository(database);
            DashboardSnapshot snapshot = null;
            DateTime trendStart = CreateDashboardTrendStart(dayEnd);

            Assert.Multiple(delegate
            {
                Assert.That(trendStart.TimeOfDay, Is.EqualTo(TimeSpan.Zero));
                Assert.That(dayEnd.TimeOfDay, Is.EqualTo(TimeSpan.Zero));
                Assert.That(
                    (dayEnd - trendStart).TotalDays,
                    Is.EqualTo(DashboardTrendDayCount));
            });

            PerformanceMeasurement.Measure(
                "Database.DashboardSnapshot.500Sessions.10000Answers",
                1,
                MeasuredSampleCount,
                delegate
                {
                    snapshot = repository.GetSnapshot(
                        dayStart,
                        dayEnd,
                        trendStart,
                        dayEnd,
                        500);
                });

            Assert.Multiple(delegate
            {
                Assert.That(snapshot, Is.Not.Null);
                Assert.That(snapshot.Metrics.TodaysTraining, Is.EqualTo(SessionCount));
                Assert.That(snapshot.RecentSessions.Count, Is.EqualTo(SessionCount));
                Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
            });
        }

        private static DateTime CreateDashboardTrendStart(DateTime trendEnd)
        {
            if (trendEnd.TimeOfDay != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Dashboard trend end must be aligned to local midnight.",
                    nameof(trendEnd));
            }

            return trendEnd.AddDays(-DashboardTrendDayCount);
        }

        private static void MeasureReports(
            MySqlService database,
            DateTime reportDate)
        {
            ReportRepository repository = new ReportRepository(database);
            ReportPeriod period = ReportPeriod.CreateDaily(reportDate);
            ReportSnapshot display = null;
            ReportSnapshot export = null;

            PerformanceMeasurement.Measure(
                "Database.Reports.Display.500Sessions.10000Answers",
                1,
                MeasuredSampleCount,
                delegate { display = repository.GetDisplaySnapshot(period); });

            PerformanceMeasurement.Measure(
                "Database.Reports.Export.500Sessions.10000Answers",
                1,
                MeasuredSampleCount,
                delegate { export = repository.GetExportSnapshot(period); });

            Assert.Multiple(delegate
            {
                Assert.That(display.Summary.SessionCount, Is.EqualTo(SessionCount));
                Assert.That(display.Sessions.Count, Is.EqualTo(SessionCount));
                Assert.That(display.IsDisplayLimited, Is.False);
                Assert.That(export.Summary.SessionCount, Is.EqualTo(SessionCount));
                Assert.That(export.Sessions.Count, Is.EqualTo(SessionCount));
                Assert.That(export.IsExportLimitExceeded, Is.False);
                Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
            });
        }

        private static void MeasureReviewQueue(MySqlService database)
        {
            AnswerRepository repository = new AnswerRepository(database);
            List<QuizAnswer> answers = null;

            PerformanceMeasurement.Measure(
                "Database.ReviewQueue.10000Answers",
                1,
                MeasuredSampleCount,
                delegate { answers = repository.GetForReview(); });

            Assert.Multiple(delegate
            {
                Assert.That(answers, Has.Count.EqualTo(SessionCount * AnswersPerSession));
                Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
            });
        }

        private static void MeasureBulkReview(
            MySqlService database,
            string administrator)
        {
            AnswerRepository repository = new AnswerRepository(database);
            List<QuizAnswer> queue = repository.GetForReview();
            List<List<QuizAnswer>> batches = queue
                .Where(answer =>
                    !answer.CorrectAnswer.HasValue &&
                    !string.IsNullOrWhiteSpace(answer.ImageHash))
                .GroupBy(answer => answer.ImageHash, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(BulkReviewUniqueImageCount)
                .Select((answer, index) => new { answer, index })
                .GroupBy(item => item.index / 25)
                .Select(group => group.Select(item => item.answer).ToList())
                .ToList();
            int batchIndex = 0;
            AnswerRepository.ReviewOperationResult result = null;

            Assert.That(
                batches,
                Has.Count.EqualTo(BulkReviewUniqueImageCount / 25));

            PerformanceMeasurement.Measure(
                "Database.ReviewWorkflow.BulkGOOD.25Images",
                1,
                MeasuredSampleCount,
                delegate
                {
                    result = repository.ReviewAnswers(
                        batches[batchIndex++],
                        QuizAnswerType.Good,
                        administrator);
                });

            PerformanceMeasurement.Measure(
                "Database.ReviewWorkflow.BulkNG.25Images",
                1,
                MeasuredSampleCount,
                delegate
                {
                    result = repository.ReviewAnswers(
                        batches[batchIndex++],
                        QuizAnswerType.Ng,
                        administrator);
                });

            Assert.Multiple(delegate
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.SelectedRowCount, Is.EqualTo(25));
                Assert.That(result.UniqueImageCount, Is.EqualTo(25));
                Assert.That(result.UpdatedAnswerCount, Is.EqualTo(250));
                Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
            });
        }

        private static void MeasureUserManagement(
            MySqlService database,
            string administrator)
        {
            UserRepository repository = new UserRepository(database);
            IList<User> users = null;

            PerformanceMeasurement.Measure(
                "Database.UserManagement.501Users",
                1,
                MeasuredSampleCount,
                delegate { users = repository.GetAllForManagement(administrator); });

            Assert.Multiple(delegate
            {
                Assert.That(users, Has.Count.EqualTo(TraineeCount + 1));
                Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
            });

            int targetUserId = users
                .Single(user => string.Equals(
                    user.EmployeeNo,
                    administrator.Substring(0, administrator.Length - 1) + "T499",
                    StringComparison.Ordinal))
                .UserID;

            PerformanceMeasurement.Measure(
                "Database.UserManagement.RoleActivationCycle.501Users",
                1,
                MeasuredSampleCount,
                delegate
                {
                    repository.SetUserRole(
                        administrator,
                        targetUserId,
                        UserRoles.User,
                        UserRoles.Admin);
                    repository.SetUserRole(
                        administrator,
                        targetUserId,
                        UserRoles.Admin,
                        UserRoles.User);
                    repository.SetUserActive(
                        administrator,
                        targetUserId,
                        true,
                        false);
                    repository.SetUserActive(
                        administrator,
                        targetUserId,
                        false,
                        true);
                });

            Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
        }

        private static void MeasureTrainingHistory(
            MySqlService database,
            string employeeNo,
            DateTime dayStart,
            DateTime dayEnd)
        {
            TrainingHistoryRepository repository =
                new TrainingHistoryRepository(database);
            TrainingHistoryPage page = null;
            TrainingHistoryQuery query = new TrainingHistoryQuery
            {
                SearchText = string.Empty,
                StartInclusive = dayStart,
                EndExclusive = dayEnd,
                ReviewFilter = TrainingHistoryReviewFilter.All,
                Offset = 0,
                Limit = 100
            };

            PerformanceMeasurement.Measure(
                "Database.TrainingHistory.120Sessions.2400Answers.Page100",
                1,
                MeasuredSampleCount,
                delegate { page = repository.GetHistoryPage(employeeNo, query); });

            Assert.Multiple(delegate
            {
                Assert.That(page.Sessions, Has.Count.EqualTo(100));
                Assert.That(page.HasMore, Is.True);
                Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
            });
        }

        #endregion

        #region Workload Seeding

        private void SeedRepresentativeWorkload(
            DateTime dayStart,
            string passwordHash)
        {
            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlTransaction transaction = connection.BeginTransaction(
                IsolationLevel.ReadCommitted))
            {
                try
                {
                    InsertUsers(connection, transaction, passwordHash);
                    InsertSessionsAndAnswers(connection, transaction, dayStart);
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
                        // Preserve the seed failure that initiated rollback.
                    }

                    throw;
                }
            }
        }

        private void InsertUsers(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string passwordHash)
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
    b'1',
    @CreatedDate
);";

            using (MySqlCommand command = new MySqlCommand(
                sql,
                connection,
                transaction))
            {
                command.Parameters.Add("@EmployeeNo", MySqlDbType.VarChar);
                command.Parameters.Add("@FullName", MySqlDbType.VarChar);
                command.Parameters.Add("@PasswordHash", MySqlDbType.VarChar);
                command.Parameters.Add("@Role", MySqlDbType.VarChar);
                command.Parameters.Add("@Department", MySqlDbType.VarChar);
                command.Parameters.Add("@CreatedDate", MySqlDbType.DateTime);
                command.Prepare();

                InsertUserCommand(
                    command,
                    Run.Employee("A"),
                    UserRoles.Admin,
                    passwordHash,
                    0);

                for (int index = 0; index < TraineeCount; index++)
                {
                    InsertUserCommand(
                        command,
                        Run.Employee("T" + index.ToString("D3", CultureInfo.InvariantCulture)),
                        UserRoles.User,
                        passwordHash,
                        index + 1);
                }
            }
        }

        private static void InsertUserCommand(
            MySqlCommand command,
            string employeeNo,
            string role,
            string passwordHash,
            int sequence)
        {
            command.Parameters["@EmployeeNo"].Value = employeeNo;
            command.Parameters["@FullName"].Value =
                "Synthetic Performance User " +
                sequence.ToString(CultureInfo.InvariantCulture);
            command.Parameters["@PasswordHash"].Value = passwordHash;
            command.Parameters["@Role"].Value = role;
            command.Parameters["@Department"].Value = "TEST-ONLY";
            command.Parameters["@CreatedDate"].Value = DateTime.UtcNow;
            command.ExecuteNonQuery();
        }

        private void InsertSessionsAndAnswers(
            MySqlConnection connection,
            MySqlTransaction transaction,
            DateTime dayStart)
        {
            using (MySqlCommand sessionCommand = CreateSessionInsertCommand(
                       connection,
                       transaction))
            using (MySqlCommand answerCommand = CreateAnswerInsertCommand(
                       connection,
                       transaction))
            {
                for (int sessionIndex = 0;
                     sessionIndex < SessionCount;
                     sessionIndex++)
                {
                    string employeeNo = sessionIndex < HistorySessionCount
                        ? Run.Employee("T000")
                        : Run.Employee(
                            "T" +
                            (sessionIndex - HistorySessionCount + 1).ToString(
                                "D3",
                                CultureInfo.InvariantCulture));
                    DateTime startTime = dayStart.AddMinutes(sessionIndex % 600);
                    int sessionId = InsertSessionCommand(
                        sessionCommand,
                        employeeNo,
                        startTime,
                        sessionIndex);

                    for (int answerIndex = 0;
                         answerIndex < AnswersPerSession;
                         answerIndex++)
                    {
                        InsertAnswerCommand(
                            answerCommand,
                            sessionId,
                            sessionIndex,
                            answerIndex);
                    }
                }
            }
        }

        private MySqlCommand CreateSessionInsertCommand(
            MySqlConnection connection,
            MySqlTransaction transaction)
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

            MySqlCommand command = new MySqlCommand(sql, connection, transaction);
            command.Parameters.Add("@EmployeeNo", MySqlDbType.VarChar);
            command.Parameters.Add("@StartTime", MySqlDbType.DateTime);
            command.Parameters.Add("@EndTime", MySqlDbType.DateTime);
            command.Parameters.Add("@TotalQuestions", MySqlDbType.Int32);
            command.Parameters.Add("@DuplicateKey", MySqlDbType.VarChar);
            command.Prepare();
            return command;
        }

        private int InsertSessionCommand(
            MySqlCommand command,
            string employeeNo,
            DateTime startTime,
            int sequence)
        {
            command.Parameters["@EmployeeNo"].Value = employeeNo;
            command.Parameters["@StartTime"].Value = startTime;
            command.Parameters["@EndTime"].Value = startTime.AddMinutes(10);
            command.Parameters["@TotalQuestions"].Value = AnswersPerSession;
            command.Parameters["@DuplicateKey"].Value =
                Run.RunId + "-PERF-" + sequence.ToString(CultureInfo.InvariantCulture);
            command.ExecuteNonQuery();
            return Convert.ToInt32(command.LastInsertedId);
        }

        private static MySqlCommand CreateAnswerInsertCommand(
            MySqlConnection connection,
            MySqlTransaction transaction)
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
    NULL
);";

            MySqlCommand command = new MySqlCommand(sql, connection, transaction);
            command.Parameters.Add("@SessionID", MySqlDbType.Int32);
            command.Parameters.Add("@ImageID", MySqlDbType.Int32);
            command.Parameters.Add("@ImageHash", MySqlDbType.String);
            command.Parameters.Add("@ImageFileName", MySqlDbType.VarChar);
            command.Parameters.Add("@UserAnswer", MySqlDbType.VarChar);
            command.Parameters.Add("@CorrectAnswer", MySqlDbType.VarChar);
            command.Parameters.Add("@IsCorrect", MySqlDbType.Bit);
            command.Parameters.Add("@AnswerTime", MySqlDbType.DateTime);
            command.Parameters.Add("@ReviewSource", MySqlDbType.VarChar);
            command.Parameters.Add("@ReviewedAt", MySqlDbType.DateTime);
            command.Prepare();
            return command;
        }

        private void InsertAnswerCommand(
            MySqlCommand command,
            int sessionId,
            int sessionIndex,
            int answerIndex)
        {
            bool pending = answerIndex % 2 == 0;
            string userAnswer = answerIndex % 2 == 0 ? "GOOD" : "NG";
            string correctAnswer = pending
                ? null
                : (answerIndex % 3 == 0 ? "NG" : "GOOD");
            bool isCorrect = !pending &&
                             string.Equals(
                                 userAnswer,
                                 correctAnswer,
                                 StringComparison.Ordinal);
            string hash = Run.ImageHash(
                (sessionIndex / 10).ToString(CultureInfo.InvariantCulture) + "-" +
                answerIndex.ToString(CultureInfo.InvariantCulture));

            command.Parameters["@SessionID"].Value = sessionId;
            command.Parameters["@ImageID"].Value = answerIndex + 1;
            command.Parameters["@ImageHash"].Value = hash;
            command.Parameters["@ImageFileName"].Value =
                Run.RunId + "-" +
                sessionIndex.ToString(CultureInfo.InvariantCulture) + "-" +
                answerIndex.ToString(CultureInfo.InvariantCulture) + ".bmp";
            command.Parameters["@UserAnswer"].Value = userAnswer;
            command.Parameters["@CorrectAnswer"].Value =
                pending ? (object)DBNull.Value : correctAnswer;
            command.Parameters["@IsCorrect"].Value = isCorrect;
            command.Parameters["@AnswerTime"].Value = DateTime.Today.AddMinutes(
                sessionIndex % 600).AddSeconds(answerIndex);
            command.Parameters["@ReviewSource"].Value =
                pending ? (object)DBNull.Value : QuizAnswer.ManualReviewSource;
            command.Parameters["@ReviewedAt"].Value =
                pending ? (object)DBNull.Value : DateTime.UtcNow;
            command.ExecuteNonQuery();
        }

        #endregion

        #region Safe Database Diagnostics

        private static ProductionFingerprint CaptureProductionFingerprint()
        {
            try
            {
                MySqlConnectionStringBuilder settings =
                    new MySqlConnectionStringBuilder(
                        ConfigurationService.GetMySqlConnectionString());
                settings.Pooling = false;
                settings.PersistSecurityInfo = false;
                settings.ConnectionTimeout =
                    settings.ConnectionTimeout == 0U
                        ? 5U
                        : Math.Min(settings.ConnectionTimeout, 5U);
                settings.DefaultCommandTimeout =
                    settings.DefaultCommandTimeout == 0U
                        ? 15U
                        : Math.Min(settings.DefaultCommandTimeout, 15U);

                using (MySqlConnection connection = new MySqlConnection(
                           settings.ConnectionString))
                {
                    connection.Open();

                    using (MySqlTransaction transaction =
                               connection.BeginTransaction(
                                   IsolationLevel.RepeatableRead))
                    {
                        string schemaHash = ReadSchemaHash(
                            connection,
                            transaction,
                            settings.Database);
                        string rowCountHash = ReadRowCountHash(
                            connection,
                            transaction);
                        int syntheticRows = ReadProductionSyntheticRowCount(
                            connection,
                            transaction);
                        transaction.Commit();

                        return new ProductionFingerprint(
                            schemaHash,
                            rowCountHash,
                            syntheticRows);
                    }
                }
            }
            catch (AssertionException)
            {
                throw;
            }
            catch
            {
                Assert.Fail(
                    "The read-only production fingerprint could not be captured safely.");
                throw;
            }
        }

        private static string ReadSchemaHash(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string schemaName)
        {
            const string sql = @"
SELECT
    TABLE_NAME,
    COLUMN_NAME,
    ORDINAL_POSITION,
    COLUMN_TYPE,
    IS_NULLABLE,
    COLUMN_KEY,
    EXTRA
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_NAME IN
      (
          'tbl_users',
          'tbl_training_session',
          'tbl_quiz_answer',
          'tbl_image_review_truth'
      )
ORDER BY TABLE_NAME, ORDINAL_POSITION;";

            StringBuilder contract = new StringBuilder();

            using (MySqlCommand command = new MySqlCommand(
                       sql,
                       connection,
                       transaction))
            {
                command.Parameters.AddWithValue("@SchemaName", schemaName);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        for (int index = 0; index < reader.FieldCount; index++)
                        {
                            contract.Append(
                                Convert.ToString(
                                    reader.GetValue(index),
                                    CultureInfo.InvariantCulture));
                            contract.Append('|');
                        }

                        contract.AppendLine();
                    }
                }
            }

            Assert.That(
                contract.Length,
                Is.GreaterThan(0),
                "The production schema contract was unavailable.");
            return ComputeTextHash(contract.ToString());
        }

        private static string ReadRowCountHash(
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            string[] tableNames =
            {
                "tbl_image_review_truth",
                "tbl_quiz_answer",
                "tbl_training_session",
                "tbl_users"
            };
            StringBuilder counts = new StringBuilder();

            foreach (string tableName in tableNames)
            {
                using (MySqlCommand command = new MySqlCommand(
                           "SELECT COUNT(*) FROM `" + tableName + "`;",
                           connection,
                           transaction))
                {
                    long count = Convert.ToInt64(
                        command.ExecuteScalar(),
                        CultureInfo.InvariantCulture);
                    counts.Append(tableName);
                    counts.Append('=');
                    counts.Append(count.ToString(CultureInfo.InvariantCulture));
                    counts.AppendLine();
                }
            }

            return ComputeTextHash(counts.ToString());
        }

        private static int ReadProductionSyntheticRowCount(
            MySqlConnection connection,
            MySqlTransaction transaction)
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
     FROM tbl_image_review_truth
     WHERE ReviewerEmployeeNo LIKE @EmployeePattern);";

            using (MySqlCommand command = new MySqlCommand(
                       sql,
                       connection,
                       transaction))
            {
                command.Parameters.AddWithValue(
                    "@EmployeePattern",
                    TestDatabaseRunContext.SyntheticEmployeePrefix + "%");
                return Convert.ToInt32(
                    command.ExecuteScalar(),
                    CultureInfo.InvariantCulture);
            }
        }

        private static string ComputeTextHash(string text)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(text ?? string.Empty));
                return BitConverter.ToString(hash)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private void WriteDatabaseVersion()
        {
            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(
                "SELECT VERSION();",
                connection))
            {
                string rawVersion = Convert.ToString(
                    command.ExecuteScalar(),
                    CultureInfo.InvariantCulture);
                string safeVersion = string.IsNullOrWhiteSpace(rawVersion)
                    ? "Unknown"
                    : rawVersion.Split('-')[0];

                TestContext.Progress.WriteLine(
                    "RESOURCE|Database.Version|Value={0}",
                    safeVersion);
            }
        }

        private void WriteRepresentativeQueryPlans(
            string administrator,
            string trainee)
        {
            using (MySqlConnection connection = Run.OpenConnection())
            {
                QueryPlan userPlan = ReadPlan(
                    connection,
                    "EXPLAIN SELECT UserID FROM tbl_users " +
                    "WHERE EmployeeNo = @Value LIMIT 1;",
                    administrator);
                QueryPlan hashPlan = ReadPlan(
                    connection,
                    "EXPLAIN SELECT AnswerID FROM tbl_quiz_answer " +
                    "WHERE ImageHash = @Value;",
                    Run.ImageHash("0-0"));
                QueryPlan historyPlan = ReadPlan(
                    connection,
                    "EXPLAIN SELECT SessionID FROM tbl_training_session " +
                    "WHERE EmployeeNo = @Value AND EndTime IS NOT NULL " +
                    "ORDER BY StartTime DESC, SessionID DESC LIMIT 101;",
                    trainee);
                QueryPlan rangePlan = ReadPlan(
                    connection,
                    "EXPLAIN SELECT COUNT(*) FROM tbl_training_session " +
                    "WHERE StartTime >= @StartTime AND StartTime < @EndTime;",
                    DateTime.Today,
                    DateTime.Today.AddDays(1));

                userPlan.Write("Users.EmployeeNoLookup");
                hashPlan.Write("Answers.ImageHashLookup");
                historyPlan.Write("History.EmployeeSessions");
                rangePlan.Write("Reports.StartTimeRange");

                Assert.Multiple(delegate
                {
                    Assert.That(
                        userPlan.Key,
                        Is.EqualTo("UX_tbl_users_EmployeeNo").IgnoreCase,
                        "The unique employee lookup should use its declared index.");
                    Assert.That(
                        hashPlan.Key,
                        Is.EqualTo("IX_tbl_quiz_answer_ImageHash").IgnoreCase,
                        "The stable image lookup should use its declared index.");
                });
            }
        }

        private static QueryPlan ReadPlan(
            MySqlConnection connection,
            string sql,
            object value)
        {
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Value", value);
                return ReadFirstPlanRow(command);
            }
        }

        private static QueryPlan ReadPlan(
            MySqlConnection connection,
            string sql,
            DateTime startTime,
            DateTime endTime)
        {
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@StartTime", startTime);
                command.Parameters.AddWithValue("@EndTime", endTime);
                return ReadFirstPlanRow(command);
            }
        }

        private static QueryPlan ReadFirstPlanRow(MySqlCommand command)
        {
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                Assert.That(reader.Read(), Is.True, "EXPLAIN must return a plan row.");
                return new QueryPlan(
                    ReadSafePlanValue(reader, "type"),
                    ReadSafePlanValue(reader, "key"),
                    ReadSafePlanValue(reader, "rows"));
            }
        }

        private static string ReadSafePlanValue(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];
            return value == null || value == DBNull.Value
                ? "None"
                : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        #endregion

        #region Nested Types

        private sealed class ProductionFingerprint
        {
            internal ProductionFingerprint(
                string schemaHash,
                string rowCountHash,
                int syntheticRowCount)
            {
                SchemaHash = schemaHash;
                RowCountHash = rowCountHash;
                SyntheticRowCount = syntheticRowCount;
            }

            internal string SchemaHash { get; private set; }

            internal string RowCountHash { get; private set; }

            internal int SyntheticRowCount { get; private set; }

            internal bool Matches(ProductionFingerprint other)
            {
                return other != null &&
                       string.Equals(
                           SchemaHash,
                           other.SchemaHash,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           RowCountHash,
                           other.RowCountHash,
                           StringComparison.Ordinal) &&
                       SyntheticRowCount == other.SyntheticRowCount;
            }
        }

        private sealed class QueryPlan
        {
            internal QueryPlan(string accessType, string key, string estimatedRows)
            {
                AccessType = accessType;
                Key = key;
                EstimatedRows = estimatedRows;
            }

            internal string AccessType { get; private set; }

            internal string Key { get; private set; }

            internal string EstimatedRows { get; private set; }

            internal void Write(string workload)
            {
                TestContext.Progress.WriteLine(
                    "QUERYPLAN|{0}|Access={1}|Key={2}|EstimatedRows={3}",
                    workload,
                    AccessType,
                    Key,
                    EstimatedRows);
            }
        }

        #endregion
    }
}
