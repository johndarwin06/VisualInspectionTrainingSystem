#region Namespaces

using MySql.Data.MySqlClient;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Database
{
    /// <summary>
    /// Performs read-only identity and marker checks before schema or data tests run.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Database)]
    [Category(TestCategories.DatabasePreflight)]
    [NonParallelizable]
    public sealed class TestDatabasePreflightTests
    {
        #region Preflight Tests

        /// <summary>Confirms the live connection selects exactly the separately declared schema.</summary>
        [Test]
        public void DedicatedSchema_ConnectsOnlyToDeclaredTestDatabase()
        {
            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Require();

            using (MySqlConnection connection = configuration.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(
                "SELECT DATABASE();",
                connection))
            {
                string actualSchema = Convert.ToString(command.ExecuteScalar());

                Assert.Multiple(delegate
                {
                    Assert.That(
                        actualSchema,
                        Is.EqualTo(configuration.SchemaName).IgnoreCase);
                    Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
                });
            }
        }

        /// <summary>Confirms the permanent marker exists and emits only safe metadata.</summary>
        [Test]
        public void DedicatedSchema_MarkerAndSafeMetadata_AreValid()
        {
            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Require();

            using (MySqlConnection connection = configuration.OpenConnection())
            {
                TestDatabaseSchema.ValidateMarker(
                    connection,
                    configuration.SchemaName);
            }

            string metadata = configuration.GetSafeMetadata();
            TestContext.Progress.WriteLine("Database preflight: " + metadata);

            Assert.Multiple(delegate
            {
                Assert.That(metadata, Does.Contain(configuration.SchemaName));
                Assert.That(metadata, Does.Not.Contain("Password"));
                Assert.That(metadata, Does.Not.Contain("User ID"));
                Assert.That(metadata, Does.Not.Contain("Uid"));
                Assert.That(metadata, Does.Not.Contain("Pwd"));
            });
        }

        /// <summary>Confirms a disposed successful connection is closed.</summary>
        [Test]
        public void DedicatedSchema_ConnectionClosesAfterSuccess()
        {
            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Require();
            MySqlConnection connection = configuration.OpenConnection();

            Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));

            connection.Dispose();

            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        }

        /// <summary>
        /// Confirms the dedicated account has only schema-scoped grants, no global
        /// privileges, no other-schema grants, and no ability to delegate privileges.
        /// </summary>
        [Test]
        public void DedicatedAccount_GrantsAreRestrictedToTestSchema()
        {
            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Require();
            List<string> grants = new List<string>();

            using (MySqlConnection connection = configuration.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(
                "SHOW GRANTS FOR CURRENT_USER();",
                connection))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                    grants.Add(reader.GetString(0));
            }

            string schemaScope =
                " ON `" + configuration.SchemaName + "`.* TO ";
            string combined = string.Join(" ", grants).ToUpperInvariant();

            Assert.Multiple(delegate
            {
                Assert.That(grants, Is.Not.Empty);

                foreach (string grant in grants)
                {
                    bool usageOnly = grant.StartsWith(
                        "GRANT USAGE ON *.* TO ",
                        StringComparison.OrdinalIgnoreCase);
                    bool dedicatedSchemaOnly = grant.IndexOf(
                        schemaScope,
                        StringComparison.OrdinalIgnoreCase) >= 0;

                    Assert.That(
                        usageOnly || dedicatedSchemaOnly,
                        Is.True,
                        "The dedicated account has a grant outside the marked test schema.");
                    Assert.That(
                        grant.IndexOf(
                            "WITH GRANT OPTION",
                            StringComparison.OrdinalIgnoreCase),
                        Is.LessThan(0),
                        "The dedicated test account must not delegate privileges.");
                }

                foreach (string required in new[]
                {
                    "SELECT", "INSERT", "UPDATE", "DELETE", "CREATE",
                    "ALTER", "INDEX", "REFERENCES"
                })
                {
                    Assert.That(
                        combined,
                        Does.Contain(required),
                        "The dedicated account is missing a required schema-scoped privilege.");
                }
            });
        }

        #endregion
    }

    /// <summary>
    /// Exercises schema, connection, transaction, parameter, and logging safety
    /// inside a unique run that is cleaned after every test.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Database)]
    [NonParallelizable]
    public sealed class TestDatabaseSafetyTests : DatabaseTestFixtureBase
    {
        #region Schema Tests

        /// <summary>Creates or upgrades the four application tables and verifies exact contracts.</summary>
        [Test]
        public void DedicatedSchema_CreationAndContractVerification_AreDeterministic()
        {
            TestDatabaseSchema.EnsureCurrent(Configuration);

            using (MySqlConnection connection = Configuration.OpenConnection())
            {
                Assert.That(
                    delegate
                    {
                        TestDatabaseSchema.VerifyContract(
                            connection,
                            Configuration.SchemaName);
                    },
                    Throws.Nothing);
            }
        }

        #endregion

        #region Connection Tests

        /// <summary>Confirms an unreachable endpoint fails within a fixed bound and remains non-sensitive.</summary>
        [Test]
        public void ConnectionFailure_IsBoundedAndSafe()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Server = "127.0.0.1",
                Port = 1U,
                Database = TestDatabaseConfiguration.RequiredSchemaPrefix,
                Username = "test_runner_placeholder",
                Password = string.Empty,
                SslMode = "Disabled",
                ConnectionTimeoutSeconds = 1,
                RetryCount = 0,
                RetryDelayMilliseconds = 0
            };
            const string connectionString =
                "Server=127.0.0.1;Port=1;Database=visual_inspection_training_test;" +
                "User ID=test_runner_placeholder;Connection Timeout=1;Pooling=false;SslMode=Disabled;";

            Stopwatch stopwatch = Stopwatch.StartNew();

            using (MySqlService database = new MySqlService(
                settings,
                connectionString))
            {
                bool connected = database.TestConnection();
                stopwatch.Stop();

                Assert.Multiple(delegate
                {
                    Assert.That(connected, Is.False);
                    Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
                    Assert.That(database.LastConnectionError, Does.Contain("Database connection"));
                    Assert.That(database.LastConnectionError, Does.Not.Contain("test_runner_placeholder"));
                    Assert.That(database.GetConnection().State, Is.EqualTo(ConnectionState.Closed));
                });
            }
        }

        #endregion

        #region Transaction Tests

        /// <summary>Confirms rollback removes a run-owned insert after a deterministic duplicate failure.</summary>
        [Test]
        public void TransactionFailure_RollsBackAndClosesSafely()
        {
            string employeeNo = Run.Employee("R");
            const string hash =
                "$2a$04$012345678901234567890uK8TL0dpdGvWDQS5nZ0s4.1QSQnG";

            using (MySqlConnection connection = Run.OpenConnection())
            {
                MySqlTransaction transaction = connection.BeginTransaction(
                    IsolationLevel.RepeatableRead);

                try
                {
                    InsertUser(connection, transaction, employeeNo, hash);

                    Assert.That(
                        delegate
                        {
                            InsertUser(connection, transaction, employeeNo, hash);
                        },
                        Throws.TypeOf<MySqlException>());

                    transaction.Rollback();
                }
                finally
                {
                    transaction.Dispose();
                }

                using (MySqlCommand count = new MySqlCommand(
                    "SELECT COUNT(*) FROM tbl_users WHERE EmployeeNo = @EmployeeNo;",
                    connection))
                {
                    count.Parameters.AddWithValue("@EmployeeNo", employeeNo);
                    Assert.That(Convert.ToInt32(count.ExecuteScalar()), Is.Zero);
                }
            }
        }

        /// <summary>Confirms SQL-looking input remains inert when supplied as a parameter.</summary>
        [Test]
        public void ParameterizedPayload_DoesNotExecuteInjectedSql()
        {
            const string payload = "x'; DROP TABLE tbl_users; --";

            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(
                "SELECT @Payload;",
                connection))
            {
                command.Parameters.AddWithValue("@Payload", payload);
                string result = Convert.ToString(command.ExecuteScalar());

                Assert.That(result, Is.EqualTo(payload));
            }

            using (MySqlConnection connection = Run.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(@"
SELECT COUNT(*)
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'tbl_users';", connection))
            {
                Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(1));
            }
        }

        #endregion

        #region Logging Tests

        /// <summary>Confirms database diagnostics retain safe metadata while removing secrets and values.</summary>
        [Test]
        public void DatabaseLogging_RedactsCredentialsConnectionAndParameters()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "VitsDatabaseLogging-" + Guid.NewGuid().ToString("N"));
            string configured = Path.Combine(root, "configured");
            string fallback = Path.Combine(root, "fallback");

            try
            {
                using (ApplicationErrorLogger.UseConfigurationForTesting(
                    configured,
                    fallback,
                    ApplicationLogLevel.Debug,
                    1024L * 1024L,
                    1))
                {
                    Exception failure = new InvalidOperationException(
                        "Database operation failed; Server=test-host;Database=test-db;" +
                        "Uid=fake-user;Pwd=fake-password; token=fake-token; " +
                        "@EmployeeNo=I19T-FAKE-VALUE");

                    ApplicationErrorLogger.LogError(
                        "Database Integration",
                        "A parameterized test database operation failed.",
                        failure);

                    Assert.That(
                        ApplicationErrorLogger.Flush(TimeSpan.FromSeconds(2)),
                        Is.True);
                }

                string log = ReadAllLogs(configured) + ReadAllLogs(fallback);

                Assert.Multiple(delegate
                {
                    Assert.That(log, Does.Contain("Source: Database Integration"));
                    Assert.That(log, Does.Contain("InvalidOperationException"));
                    Assert.That(log, Does.Contain("redacted").IgnoreCase);
                    Assert.That(log, Does.Not.Contain("fake-user"));
                    Assert.That(log, Does.Not.Contain("fake-password"));
                    Assert.That(log, Does.Not.Contain("fake-token"));
                    Assert.That(log, Does.Not.Contain("I19T-FAKE-VALUE"));
                });
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        #endregion

        #region Helpers

        private static void InsertUser(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string employeeNo,
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
    'Synthetic Rollback User',
    @PasswordHash,
    'User',
    'TEST-ONLY',
    b'1',
    UTC_TIMESTAMP()
);";

            using (MySqlCommand command = new MySqlCommand(
                sql,
                connection,
                transaction))
            {
                command.Parameters.AddWithValue("@EmployeeNo", employeeNo);
                command.Parameters.AddWithValue("@PasswordHash", passwordHash);
                command.ExecuteNonQuery();
            }
        }

        private static string ReadAllLogs(string directory)
        {
            if (!Directory.Exists(directory))
                return string.Empty;

            string content = string.Empty;

            foreach (string file in Directory.GetFiles(
                directory,
                ApplicationErrorLogger.CurrentLogFileName + "*"))
            {
                content += File.ReadAllText(file);
            }

            return content;
        }

        #endregion
    }

    /// <summary>
    /// Provides the explicit read-only post-run cleanup gate used by the runner.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Database)]
    [Category(TestCategories.DatabaseCleanup)]
    [NonParallelizable]
    public sealed class TestDatabaseCleanupVerificationTests
    {
        #region Cleanup Test

        /// <summary>Confirms no row using the reserved Issue #19 prefix remains.</summary>
        [Test]
        public void CleanupVerification_ZeroSyntheticRowsRemain()
        {
            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Require();
            TestDatabaseSchema.EnsureCurrent(configuration);

            int count = TestDatabaseRunContext.CountAllSyntheticRows(configuration);
            TestContext.Progress.WriteLine(
                "Database cleanup verification: synthetic rows remaining=" + count + ".");

            Assert.That(count, Is.Zero);
        }

        #endregion
    }
}
