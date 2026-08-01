#region Namespaces

using MySql.Data.MySqlClient;
using NUnit.Framework;
using System;
using System.Data;
using System.Text.RegularExpressions;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Infrastructure
{
    /// <summary>
    /// Provides the permanent fail-closed boundary for test-only MySQL access.
    /// Credentials remain in memory and are never returned by diagnostic APIs.
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

        /// <summary>Required test-schema name and optional isolated suffix prefix.</summary>
        public const string RequiredSchemaPrefix =
            "visual_inspection_training_test";

        private const string DefaultProductionSchema =
            "visualinspectionquiz";

        private const uint MaximumConnectionTimeoutSeconds = 5U;
        private const uint MaximumCommandTimeoutSeconds = 15U;

        private static readonly Regex TestSchemaPattern = new Regex(
            "^visual_inspection_training_test(?:_[a-z0-9][a-z0-9_]*)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase);

        private static readonly Regex ProductionLookingSchemaPattern = new Regex(
            "(?:^|_)(?:prod|production|live|main|operational)(?:_|$)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase);

        #endregion

        #region Fields

        private readonly string _connectionString;
        private readonly DatabaseSettings _settings;

        #endregion

        #region Constructors

        private TestDatabaseConfiguration(
            string connectionString,
            DatabaseSettings settings,
            string schemaName,
            bool localEndpoint,
            string sslMode)
        {
            _connectionString = connectionString;
            _settings = settings;
            SchemaName = schemaName;
            IsLocalEndpoint = localEndpoint;
            SslMode = sslMode;
        }

        #endregion

        #region Properties

        /// <summary>Gets the validated dedicated schema name.</summary>
        public string SchemaName
        {
            get;
            private set;
        }

        /// <summary>Gets whether the endpoint resolves through a local-host alias.</summary>
        public bool IsLocalEndpoint
        {
            get;
            private set;
        }

        /// <summary>Gets the non-sensitive configured SSL mode.</summary>
        public string SslMode
        {
            get;
            private set;
        }

        #endregion

        #region Factory

        /// <summary>
        /// Loads a validated test configuration or skips only when both variables are absent.
        /// An incomplete or unsafe supplied configuration fails rather than silently skipping.
        /// </summary>
        /// <returns>A configuration proven distinct from production.</returns>
        public static TestDatabaseConfiguration Require()
        {
            string connectionString = ReadEnvironmentSetting(
                ConnectionStringEnvironmentVariable);
            string declaredSchema = ReadEnvironmentSetting(
                SchemaEnvironmentVariable);

            bool hasConnection = !string.IsNullOrWhiteSpace(connectionString);
            bool hasSchema = !string.IsNullOrWhiteSpace(declaredSchema);

            if (!hasConnection && !hasSchema)
            {
                Assert.Ignore(
                    "Database tests are safely disabled because " +
                    ConnectionStringEnvironmentVariable +
                    " and " +
                    SchemaEnvironmentVariable +
                    " are not configured.");
            }

            if (!hasConnection || !hasSchema)
            {
                Assert.Fail(
                    "Database tests refused an incomplete test-only configuration. " +
                    "Both documented environment variables are required.");
            }

            string productionConnectionString;

            try
            {
                productionConnectionString =
                    ConfigurationService.GetMySqlConnectionString();
            }
            catch (Exception)
            {
                Assert.Fail(
                    "Database tests could not prove separation from the normal " +
                    "application database and therefore failed closed.");
                throw;
            }

            try
            {
                return Validate(
                    connectionString,
                    declaredSchema,
                    productionConnectionString);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Validates supplied values without reading process state, enabling deterministic safety tests.
        /// </summary>
        /// <param name="testConnectionString">Candidate test-only connection.</param>
        /// <param name="declaredSchema">Separately declared schema identity.</param>
        /// <param name="productionConnectionString">Normal application connection for comparison.</param>
        /// <returns>A fail-closed validated configuration.</returns>
        internal static TestDatabaseConfiguration Validate(
            string testConnectionString,
            string declaredSchema,
            string productionConnectionString)
        {
            if (string.IsNullOrWhiteSpace(testConnectionString) ||
                string.IsNullOrWhiteSpace(declaredSchema))
            {
                throw new InvalidOperationException(
                    "Both test-only database settings are required.");
            }

            if (string.IsNullOrWhiteSpace(productionConnectionString))
            {
                throw new InvalidOperationException(
                    "Database tests could not prove separation from production.");
            }

            MySqlConnectionStringBuilder test = ParseConnectionString(
                testConnectionString,
                "The test-only MySQL configuration is malformed.");
            MySqlConnectionStringBuilder production = ParseConnectionString(
                productionConnectionString,
                "The production database identity could not be validated safely.");

            string expectedSchema = declaredSchema.Trim();
            string configuredSchema = NormalizeSchema(test.Database);

            ValidateSchemaIdentity(configuredSchema, expectedSchema);
            ValidateProductionSeparation(test, production);

            test.Database = configuredSchema;
            test.Pooling = false;
            test.PersistSecurityInfo = false;
            test.AllowUserVariables = true;
            test.ConnectionTimeout = BoundTimeout(
                test.ConnectionTimeout,
                MaximumConnectionTimeoutSeconds);
            test.DefaultCommandTimeout = BoundTimeout(
                test.DefaultCommandTimeout,
                MaximumCommandTimeoutSeconds);

            DatabaseSettings settings = new DatabaseSettings
            {
                Server = test.Server,
                Port = test.Port,
                Database = configuredSchema,
                Username = test.UserID,
                Password = test.Password,
                SslMode = test.SslMode.ToString(),
                ConnectionTimeoutSeconds = Convert.ToInt32(test.ConnectionTimeout),
                RetryCount = 0,
                RetryDelayMilliseconds = 0
            };

            return new TestDatabaseConfiguration(
                test.ConnectionString,
                settings,
                configuredSchema,
                IsLocalServer(test.Server),
                test.SslMode.ToString());
        }

        #endregion

        #region Connection Creation

        /// <summary>
        /// Opens a new connection and verifies the selected schema and permanent marker.
        /// </summary>
        /// <returns>An open, marker-validated test-only connection.</returns>
        public MySqlConnection OpenConnection()
        {
            MySqlConnection connection = new MySqlConnection(_connectionString);

            try
            {
                connection.Open();
                ValidateLiveSchema(connection);
                TestDatabaseSchema.ValidateMarker(connection, SchemaName);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates the production database service against this already validated schema.
        /// </summary>
        /// <returns>A database service that does not modify global configuration.</returns>
        public MySqlService CreateDatabaseService()
        {
            return new MySqlService(_settings, _connectionString);
        }

        /// <summary>
        /// Returns metadata safe for preflight output without server names, accounts, or secrets.
        /// </summary>
        /// <returns>Non-sensitive endpoint class, schema, port, and SSL mode.</returns>
        public string GetSafeMetadata()
        {
            return "Endpoint=" +
                   (IsLocalEndpoint ? "Local" : "Remote") +
                   "; Port=" +
                   _settings.Port +
                   "; Schema=" +
                   SchemaName +
                   "; SslMode=" +
                   SslMode +
                   "; DedicatedAccountConfigured=True";
        }

        #endregion

        #region Safety Validation

        /// <summary>
        /// Reads a test setting from the current process first and then from the
        /// Windows user environment, allowing secret-free setup without an IDE restart.
        /// </summary>
        private static string ReadEnvironmentSetting(string variableName)
        {
            string value = Environment.GetEnvironmentVariable(variableName);

            if (!string.IsNullOrWhiteSpace(value))
                return value;

            return Environment.GetEnvironmentVariable(
                variableName,
                EnvironmentVariableTarget.User);
        }

        private static MySqlConnectionStringBuilder ParseConnectionString(
            string connectionString,
            string safeError)
        {
            try
            {
                return new MySqlConnectionStringBuilder(connectionString);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(safeError, ex);
            }
        }

        private static void ValidateSchemaIdentity(
            string configuredSchema,
            string expectedSchema)
        {
            if (configuredSchema.Length == 0 || expectedSchema.Length == 0 ||
                !string.Equals(
                    configuredSchema,
                    expectedSchema,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The configured database does not match the separately declared test schema.");
            }

            if (!TestSchemaPattern.IsMatch(configuredSchema) ||
                ProductionLookingSchemaPattern.IsMatch(configuredSchema) ||
                IsSystemSchema(configuredSchema) ||
                string.Equals(
                    configuredSchema,
                    DefaultProductionSchema,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Database tests refused a schema that is not demonstrably test-only.");
            }
        }

        private static void ValidateProductionSeparation(
            MySqlConnectionStringBuilder test,
            MySqlConnectionStringBuilder production)
        {
            string testSchema = NormalizeSchema(test.Database);
            string productionSchema = NormalizeSchema(production.Database);

            if (productionSchema.Length == 0)
            {
                throw new InvalidOperationException(
                    "The production database identity is ambiguous; tests failed closed.");
            }

            if (string.Equals(
                    testSchema,
                    productionSchema,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Database tests refused the normal application schema.");
            }

            bool sameServer = string.Equals(
                NormalizeServer(test.Server),
                NormalizeServer(production.Server),
                StringComparison.OrdinalIgnoreCase);
            bool samePort = test.Port == production.Port;
            bool sameUser = string.Equals(
                (test.UserID ?? string.Empty).Trim(),
                (production.UserID ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);

            if (sameServer && samePort && sameUser)
            {
                throw new InvalidOperationException(
                    "Database tests require a dedicated account distinct from production.");
            }
        }

        private void ValidateLiveSchema(MySqlConnection connection)
        {
            using (MySqlCommand command = new MySqlCommand(
                "SELECT DATABASE();",
                connection))
            {
                string actualSchema = Convert.ToString(command.ExecuteScalar());

                if (!string.Equals(
                        actualSchema,
                        SchemaName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The live database connection selected an unexpected schema.");
                }
            }
        }

        private static string NormalizeSchema(string schema)
        {
            return (schema ?? string.Empty).Trim();
        }

        private static string NormalizeServer(string server)
        {
            string normalized = (server ?? string.Empty).Trim();

            if (IsLocalServer(normalized))
                return "<LOCAL>";

            return normalized.TrimEnd('.').ToUpperInvariant();
        }

        private static bool IsLocalServer(string server)
        {
            string normalized = (server ?? string.Empty).Trim();

            return string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "::1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, ".", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSystemSchema(string schema)
        {
            return string.Equals(schema, "mysql", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(schema, "information_schema", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(schema, "performance_schema", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(schema, "sys", StringComparison.OrdinalIgnoreCase);
        }

        private static uint BoundTimeout(uint configured, uint maximum)
        {
            if (configured == 0U)
                return maximum;

            return Math.Min(configured, maximum);
        }

        #endregion
    }
}
