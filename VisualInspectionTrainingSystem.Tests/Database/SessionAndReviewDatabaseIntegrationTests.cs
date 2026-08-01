#region Namespaces

using MySql.Data.MySqlClient;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Database
{
    /// <summary>
    /// Exercises real session, answer, duplicate, truth, review, and recalculation
    /// transactions against unique synthetic rows.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Database)]
    [NonParallelizable]
    public sealed class SessionAndReviewDatabaseIntegrationTests : DatabaseTestFixtureBase
    {
        #region Constants

        private const string SyntheticPassword = "I19-Session-Only!42";

        #endregion

        #region Session Persistence

        /// <summary>Confirms a completed session and all answers commit atomically.</summary>
        [Test]
        public void CompletedSession_StoresHeaderAndAnswersInOneTransaction()
        {
            string employeeNo = SeedTrainee("S");
            DateTime start = TrimToSecond(DateTime.Now.AddMinutes(-10));
            DateTime end = start.AddMinutes(10);
            TrainingSession session = CreateSession(
                employeeNo,
                start,
                end,
                new[]
                {
                    CreateAnswer(1, Run.ImageHash("S1"), QuizAnswerType.Good),
                    CreateAnswer(2, Run.ImageHash("S2"), QuizAnswerType.Ng)
                });

            int sessionId;

            using (MySqlService database = Run.CreateDatabaseService())
            {
                sessionId = new SessionRepository(database).Save(session);
                Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
            }

            using (MySqlConnection connection = Run.OpenConnection())
            {
                Assert.Multiple(delegate
                {
                    Assert.That(sessionId, Is.GreaterThan(0));
                    Assert.That(session.SessionID, Is.EqualTo(sessionId));
                    Assert.That(
                        ExecuteCount(
                            connection,
                            "SELECT COUNT(*) FROM tbl_training_session WHERE SessionID = @Id;",
                            sessionId),
                        Is.EqualTo(1));
                    Assert.That(
                        ExecuteCount(
                            connection,
                            "SELECT COUNT(*) FROM tbl_quiz_answer WHERE SessionID = @Id;",
                            sessionId),
                        Is.EqualTo(2));
                    Assert.That(
                        ExecuteCount(
                            connection,
                            "SELECT COUNT(*) FROM tbl_quiz_answer WHERE SessionID = @Id AND CorrectAnswer IS NULL AND IsCorrect = b'0';",
                            sessionId),
                        Is.EqualTo(2));
                    Assert.That(
                        ExecuteCount(
                            connection,
                            "SELECT CorrectAnswers + WrongAnswers FROM tbl_training_session WHERE SessionID = @Id;",
                            sessionId),
                        Is.Zero);
                });
            }
        }

        /// <summary>Confirms a forced answer insert failure rolls back its parent session and answers.</summary>
        [Test]
        public void ForcedAnswerFailure_RollsBackSessionAndAnswers()
        {
            string employeeNo = SeedTrainee("F");
            TrainingSession session = CreateSession(
                employeeNo,
                TrimToSecond(DateTime.Now.AddMinutes(-1)),
                TrimToSecond(DateTime.Now),
                new[]
                {
                    CreateAnswer(190019, Run.ImageHash("FAIL"), QuizAnswerType.Good)
                });

            AddForcedAnswerFailureConstraint();

            try
            {
                using (MySqlService database = Run.CreateDatabaseService())
                {
                    InvalidOperationException failure =
                        Assert.Throws<InvalidOperationException>(delegate
                        {
                            new SessionRepository(database).Save(session);
                        });

                    Assert.Multiple(delegate
                    {
                        Assert.That(session.SessionID, Is.Zero);
                        Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
                        Assert.That(failure.Message, Does.Contain("rolled back"));
                        Assert.That(failure.Message, Does.Not.Contain("INSERT"));
                        Assert.That(failure.Message, Does.Not.Contain(Configuration.SchemaName));
                    });
                }
            }
            finally
            {
                RemoveForcedAnswerFailureConstraint();
            }

            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(@"
SELECT
    (SELECT COUNT(*) FROM tbl_training_session WHERE EmployeeNo = @EmployeeNo) +
    (SELECT COUNT(*)
     FROM tbl_quiz_answer a
     INNER JOIN tbl_training_session s ON s.SessionID = a.SessionID
     WHERE s.EmployeeNo = @EmployeeNo);", connection))
            {
                command.Parameters.AddWithValue("@EmployeeNo", employeeNo);
                Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.Zero);
            }
        }

        /// <summary>Confirms a committed duplicate returns the fixed non-sensitive duplicate message.</summary>
        [Test]
        public void SequentialDuplicateCompletedSession_ReturnsSafeDuplicateMessage()
        {
            string employeeNo = SeedTrainee("D");
            DateTime start = TrimToSecond(DateTime.Now.AddMinutes(-3));
            DateTime end = start.AddMinutes(1);
            TrainingSession first = CreateSession(
                employeeNo,
                start,
                end,
                new[]
                {
                    CreateAnswer(1, Run.ImageHash("D1"), QuizAnswerType.Good)
                });
            TrainingSession duplicate = CreateSession(
                employeeNo,
                start,
                end,
                new[]
                {
                    CreateAnswer(2, Run.ImageHash("D2"), QuizAnswerType.Good)
                });

            using (MySqlService database = Run.CreateDatabaseService())
            {
                SessionRepository repository = new SessionRepository(database);
                repository.Save(first);

                InvalidOperationException failure =
                    Assert.Throws<InvalidOperationException>(delegate
                    {
                        repository.Save(duplicate);
                    });

                Assert.Multiple(delegate
                {
                    Assert.That(
                        failure.Message,
                        Does.Contain("Duplicate completed quiz session"));
                    Assert.That(failure.Message, Does.Not.Contain("DuplicateKey"));
                    Assert.That(failure.Message, Does.Not.Contain("MySql"));
                    Assert.That(duplicate.SessionID, Is.Zero);
                });
            }
        }

        /// <summary>Confirms concurrent saves of one completion identity permit exactly one commit.</summary>
        [Test]
        public void ConcurrentDuplicateCompletedSession_AllowsExactlyOneSuccess()
        {
            string employeeNo = SeedTrainee("C");
            DateTime start = TrimToSecond(DateTime.Now.AddMinutes(-2));
            DateTime end = start.AddMinutes(1);
            TrainingSession first = CreateSession(
                employeeNo,
                start,
                end,
                new[]
                {
                    CreateAnswer(1, Run.ImageHash("C1"), QuizAnswerType.Good)
                });
            TrainingSession second = CreateSession(
                employeeNo,
                start,
                end,
                new[]
                {
                    CreateAnswer(2, Run.ImageHash("C2"), QuizAnswerType.Good)
                });
            Barrier gate = new Barrier(2);

            Task<SaveOutcome> firstTask = Task.Run(delegate
            {
                return SaveAtGate(first, gate);
            });
            Task<SaveOutcome> secondTask = Task.Run(delegate
            {
                return SaveAtGate(second, gate);
            });

            Assert.That(
                Task.WaitAll(
                    new Task[] { firstTask, secondTask },
                    TimeSpan.FromSeconds(20)),
                Is.True,
                "Concurrent session verification exceeded its bound.");

            SaveOutcome[] outcomes =
            {
                firstTask.Result,
                secondTask.Result
            };
            string safeFailure =
                outcomes.Single(value => !value.Succeeded).SafeMessage;

            Assert.Multiple(delegate
            {
                Assert.That(outcomes.Count(value => value.Succeeded), Is.EqualTo(1));
                Assert.That(outcomes.Count(value => !value.Succeeded), Is.EqualTo(1));
                Assert.That(
                    safeFailure ==
                        "Duplicate completed quiz session detected. No new session or answers were saved." ||
                    safeFailure ==
                        "Failed to save the completed quiz session. The session and answer inserts were rolled back.",
                    Is.True);
                Assert.That(
                    safeFailure,
                    Does.Not.Contain("DuplicateKey"));
                Assert.That(
                    safeFailure,
                    Does.Not.Contain("MySql"));
            });

            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(@"
SELECT COUNT(*)
FROM tbl_training_session
WHERE EmployeeNo = @EmployeeNo;", connection))
            {
                command.Parameters.AddWithValue("@EmployeeNo", employeeNo);
                Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(1));
            }
        }

        #endregion

        #region Review Workflow

        /// <summary>
        /// Confirms reusable truth creation, propagation, correction, conflict handling,
        /// pending exclusion, and session recalculation through real review transactions.
        /// </summary>
        [Test]
        public void ReviewTruth_PropagatesCorrectsAndRecalculatesSessions()
        {
            string employeeNo = SeedTrainee("V");
            string reviewer = SeedAdministrator("A");
            string sharedHash = Run.ImageHash("SHARED");
            DateTime start = TrimToSecond(DateTime.Today.AddHours(8));
            int firstSession = Run.InsertSession(
                employeeNo,
                start,
                start.AddMinutes(10),
                2,
                Run.ImageHash("DUP1"));
            int firstAnswer = Run.InsertAnswer(
                firstSession,
                1,
                sharedHash,
                " good ",
                null,
                null,
                null,
                null);
            Run.InsertAnswer(
                firstSession,
                2,
                Run.ImageHash("PENDING"),
                "NG",
                " MAYBE ",
                false,
                null,
                null);
            int secondSession = Run.InsertSession(
                employeeNo,
                start.AddMinutes(20),
                start.AddMinutes(25),
                1,
                Run.ImageHash("DUP2"));
            Run.InsertAnswer(
                secondSession,
                3,
                sharedHash,
                "ng",
                null,
                null,
                null,
                null);

            AnswerRepository repository = new AnswerRepository(
                Run.CreateDatabaseService());

            AnswerRepository.ReviewOperationResult initial =
                repository.ReviewAnswer(
                    firstAnswer,
                    QuizAnswerType.Good,
                    reviewer,
                    null,
                    null,
                    null);

            Assert.Multiple(delegate
            {
                Assert.That(initial.UpdatedAnswerCount, Is.EqualTo(2));
                Assert.That(ReadTruthVersion(sharedHash), Is.EqualTo(1));
                AssertSessionTotals(firstSession, 1, 0, 100.00m);
                AssertSessionTotals(secondSession, 0, 1, 0.00m);
                Assert.That(ReadPendingCount(firstSession), Is.EqualTo(1));
            });

            AnswerRepository.ReviewOperationResult corrected =
                repository.ReviewAnswer(
                    firstAnswer,
                    QuizAnswerType.Ng,
                    reviewer,
                    QuizAnswerType.Good,
                    null,
                    null);

            Assert.Multiple(delegate
            {
                Assert.That(corrected.CorrectionCount, Is.EqualTo(1));
                Assert.That(ReadTruthVersion(sharedHash), Is.EqualTo(2));
                AssertSessionTotals(firstSession, 0, 1, 0.00m);
                AssertSessionTotals(secondSession, 1, 0, 100.00m);
                Assert.That(ReadPendingCount(firstSession), Is.EqualTo(1));
            });

            InvalidOperationException stale =
                Assert.Throws<InvalidOperationException>(delegate
                {
                    repository.ReviewAnswer(
                        firstAnswer,
                        QuizAnswerType.Good,
                        reviewer,
                        QuizAnswerType.Good,
                        null,
                        null);
                });

            Assert.Multiple(delegate
            {
                Assert.That(stale.Message, Does.Contain("changed"));
                Assert.That(stale.Message, Does.Not.Contain(sharedHash));
                Assert.That(stale.Message, Does.Not.Contain("UPDATE"));
                Assert.That(ReadTruthVersion(sharedHash), Is.EqualTo(2));
            });
        }

        #endregion

        #region Helpers

        private string SeedTrainee(string suffix)
        {
            string hash = new PasswordHashService().HashPassword(SyntheticPassword);
            Run.InsertUser(suffix, hash, UserRoles.User, true);
            return Run.Employee(suffix);
        }

        private string SeedAdministrator(string suffix)
        {
            string hash = new PasswordHashService().HashPassword(SyntheticPassword);
            Run.InsertUser(suffix, hash, UserRoles.Admin, true);
            return Run.Employee(suffix);
        }

        private SaveOutcome SaveAtGate(
            TrainingSession session,
            Barrier gate)
        {
            using (MySqlService database = Run.CreateDatabaseService())
            {
                SessionRepository repository = new SessionRepository(database);
                gate.SignalAndWait(TimeSpan.FromSeconds(5));

                try
                {
                    return SaveOutcome.Success(repository.Save(session));
                }
                catch (Exception ex)
                {
                    return SaveOutcome.Failure(ex.Message);
                }
            }
        }

        private static TrainingSession CreateSession(
            string employeeNo,
            DateTime started,
            DateTime finished,
            IList<QuizAnswer> answers)
        {
            TrainingSession session = new TrainingSession
            {
                User = new User
                {
                    EmployeeNo = employeeNo,
                    Role = UserRoles.User,
                    IsActive = true
                }
            };

            foreach (QuizAnswer answer in answers)
            {
                session.Images.Add(new QuizImage
                {
                    ImageID = answer.ImageID,
                    ImageHash = answer.ImageHash,
                    FileName = answer.FileName
                });
                session.AddAnswer(answer);
            }

            SetAutoProperty(session, "Started", started);
            SetAutoProperty(session, "Finished", (DateTime?)finished);
            return session;
        }

        private static QuizAnswer CreateAnswer(
            int imageId,
            string imageHash,
            QuizAnswerType userAnswer)
        {
            return new QuizAnswer
            {
                ImageID = imageId,
                ImageHash = imageHash,
                FileName = "synthetic-" + imageId + ".png",
                UserAnswer = userAnswer,
                CorrectAnswer = null,
                IsCorrect = false,
                AnswerTime = TrimToSecond(DateTime.Now),
                ElapsedSeconds = 1.0
            };
        }

        private static void SetAutoProperty(
            TrainingSession session,
            string propertyName,
            object value)
        {
            FieldInfo field = typeof(TrainingSession).GetField(
                "<" + propertyName + ">k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
                throw new InvalidOperationException("The controlled session time seam is unavailable.");

            field.SetValue(session, value);
        }

        private static int ExecuteCount(
            MySqlConnection connection,
            string sql,
            int sessionId)
        {
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", sessionId);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private int ReadTruthVersion(string imageHash)
        {
            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(@"
SELECT Version
FROM tbl_image_review_truth
WHERE ImageHash = @ImageHash;", connection))
            {
                command.Parameters.AddWithValue("@ImageHash", imageHash);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        /// <summary>Adds a deterministic test-only answer failure rule.</summary>
        private void AddForcedAnswerFailureConstraint()
        {
            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(@"
ALTER TABLE tbl_quiz_answer
ADD CONSTRAINT __vits_test_force_answer_failure
CHECK (ImageID <> 190019);", connection))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>Removes the deterministic test-only answer failure rule.</summary>
        private void RemoveForcedAnswerFailureConstraint()
        {
            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(@"
ALTER TABLE tbl_quiz_answer
DROP CHECK __vits_test_force_answer_failure;", connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private int ReadPendingCount(int sessionId)
        {
            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(@"
SELECT COUNT(*)
FROM tbl_quiz_answer
WHERE SessionID = @SessionID
  AND
  (
      CorrectAnswer IS NULL OR
      UPPER(TRIM(CorrectAnswer)) NOT IN ('GOOD', 'NG')
  );", connection))
            {
                command.Parameters.AddWithValue("@SessionID", sessionId);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private void AssertSessionTotals(
            int sessionId,
            int correct,
            int wrong,
            decimal accuracy)
        {
            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(@"
SELECT CorrectAnswers, WrongAnswers, Accuracy
FROM tbl_training_session
WHERE SessionID = @SessionID;", connection))
            {
                command.Parameters.AddWithValue("@SessionID", sessionId);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    Assert.That(reader.Read(), Is.True);
                    Assert.Multiple(delegate
                    {
                        Assert.That(reader.GetInt32(0), Is.EqualTo(correct));
                        Assert.That(reader.GetInt32(1), Is.EqualTo(wrong));
                        Assert.That(reader.GetDecimal(2), Is.EqualTo(accuracy));
                    });
                }
            }
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

        private sealed class SaveOutcome
        {
            private SaveOutcome(
                bool succeeded,
                int sessionId,
                string safeMessage)
            {
                Succeeded = succeeded;
                SessionId = sessionId;
                SafeMessage = safeMessage;
            }

            public bool Succeeded { get; private set; }

            public int SessionId { get; private set; }

            public string SafeMessage { get; private set; }

            public static SaveOutcome Success(int sessionId)
            {
                return new SaveOutcome(true, sessionId, string.Empty);
            }

            public static SaveOutcome Failure(string safeMessage)
            {
                return new SaveOutcome(false, 0, safeMessage ?? string.Empty);
            }
        }

        #endregion
    }
}
