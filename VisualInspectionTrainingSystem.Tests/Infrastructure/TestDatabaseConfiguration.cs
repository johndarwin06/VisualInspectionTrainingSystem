#region Namespaces

using MySql.Data.MySqlClient;
using NUnit.Framework;
using System;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Infrastructure
{
    /// <summary>
    /// Provides a fail-closed boundary for database regression tests.
    /// Credentials are read only from environment variables and are never logged.
    /// </summary>
    internal sealed class TestDatabaseConfiguration
    {
        #region Constants

        /// <summary>Environment variable containing the test-only MySQL connection string.</summary>
        public const string ConnectionStringEnvironmentVariable =
            "VITS_TEST_MYSQL_CONNECTION_STRING";

        /// <summary>Environment variable declaring the expected test-only schema.</summary>
        public const string SchemaEnvironmentVariable =
            "VITS_TEST_MYSQL_SCHEMA";

        private const string DefaultProductionSchema =
            "visualinspectionquiz";

        #endregion

        #region Fields

        private readonly string _connectionString;

        #endregion

        #region Constructors

        private TestDatabaseConfiguration(
            string connectionString,
            string schemaName)
        {
            _connectionString = connectionString;
            SchemaName = schemaName;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the validated test-only schema name without exposing credentials.
        /// </summary>
        public string SchemaName
        {
            get;
            private set;
        }

        #endregion

        #region Factory

        /// <summary>
        /// Loads and validates test database settings or skips the current test when absent.
        /// </summary>
        /// <returns>A validated test-only database configuration.</returns>
        public static TestDatabaseConfiguration Require()
        {
            string connectionString = Environment.GetEnvironmentVariable(
                ConnectionStringEnvironmentVariable);
            string declaredSchema = Environment.GetEnvironmentVariable(
                SchemaEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(connectionString) ||
                string.IsNullOrWhiteSpace(declaredSchema))
            {
                Assert.Ignore(
                    "Safe MySQL tests require " +
                    ConnectionStringEnvironmentVariable +
                    " and " +
                    SchemaEnvironmentVariable +
                    ".");
            }

            MySqlConnectionStringBuilder builder;

            try
            {
                builder = new MySqlConnectionStringBuilder(connectionString);
            }
            catch (Exception)
            {
                Assert.Ignore("The test-only MySQL configuration is malformed.");
                throw;
            }

            string configuredSchema = (builder.Database ?? string.Empty).Trim();
            string expectedSchema = declaredSchema.Trim();

            if (configuredSchema.Length == 0 ||
                !string.Equals(
                    configuredSchema,
                    expectedSchema,
                    StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore(
                    "The configured database does not match the declared test schema.");
            }

            if (!LooksLikeTestSchema(configuredSchema) ||
                IsKnownProductionSchema(configuredSchema))
            {
                Assert.Ignore(
                    "Database tests refused a schema that is not demonstrably test-only.");
            }

            builder.Pooling = false;
            builder.ConnectionTimeout = Math.Min(
                builder.ConnectionTimeout == 0 ? 5U : builder.ConnectionTimeout,
                5U);

            return new TestDatabaseConfiguration(
                builder.ConnectionString,
                configuredSchema);
        }

        #endregion

        #region Connection Creation

        /// <summary>
        /// Opens a new connection owned by the caller.
        /// </summary>
        /// <returns>An open connection to the validated test-only schema.</returns>
        public MySqlConnection OpenConnection()
        {
            MySqlConnection connection = new MySqlConnection(_connectionString);

            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        #endregion

        #region Safety Validation

        private static bool LooksLikeTestSchema(string schemaName)
        {
            return schemaName.IndexOf(
                "test",
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsKnownProductionSchema(string schemaName)
        {
            if (string.Equals(
                    schemaName,
                    DefaultProductionSchema,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                MySqlConnectionStringBuilder production =
                    new MySqlConnectionStringBuilder(
                        ConfigurationService.GetMySqlConnectionString());

                return string.Equals(
                    schemaName,
                    production.Database,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
