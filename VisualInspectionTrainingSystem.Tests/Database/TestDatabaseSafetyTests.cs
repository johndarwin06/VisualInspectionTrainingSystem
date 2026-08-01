#region Namespaces

using MySql.Data.MySqlClient;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Database
{
    /// <summary>
    /// Exercises the opt-in test-only MySQL boundary without touching normal application data.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Database)]
    [NonParallelizable]
    public sealed class TestDatabaseSafetyTests
    {
        #region Connection Tests

        /// <summary>Confirms the connected schema is exactly the separately declared test schema.</summary>
        [Test]
        public void DedicatedSchema_ConnectsOnlyToDeclaredTestDatabase()
        {
            // Arrange
            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Require();

            // Act
            using (MySqlConnection connection = configuration.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(
                "SELECT DATABASE();",
                connection))
            {
                string actualSchema = Convert.ToString(command.ExecuteScalar());

                // Assert
                Assert.That(
                    actualSchema,
                    Is.EqualTo(configuration.SchemaName).IgnoreCase);
                Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
            }
        }

        /// <summary>Confirms a dedicated schema has the existing application tables but never creates them.</summary>
        [Test]
        public void DedicatedSchema_ContainsRequiredApplicationTables()
        {
            // Arrange
            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Require();
            string[] requiredTables =
            {
                "tbl_user",
                "tbl_training_session",
                "tbl_quiz_answer"
            };

            // Act
            using (MySqlConnection connection = configuration.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(@"
SELECT COUNT(*)
FROM information_schema.tables
WHERE table_schema = @SchemaName
  AND table_name = @TableName;", connection))
            {
                command.Parameters.Add("@SchemaName", MySqlDbType.VarChar);
                command.Parameters.Add("@TableName", MySqlDbType.VarChar);

                foreach (string tableName in requiredTables)
                {
                    command.Parameters["@SchemaName"].Value =
                        configuration.SchemaName;
                    command.Parameters["@TableName"].Value = tableName;

                    int count = Convert.ToInt32(command.ExecuteScalar());

                    // Assert
                    Assert.That(count, Is.EqualTo(1), tableName);
                }
            }
        }

        #endregion

        #region Transaction Tests

        /// <summary>Confirms rollback removes test-only rows and closes the transaction safely.</summary>
        [Test]
        public void TemporaryTransaction_RollbackLeavesZeroRows()
        {
            // Arrange
            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Require();
            string temporaryTable = "tmp_i17_" + Guid.NewGuid().ToString("N");

            using (MySqlConnection connection = configuration.OpenConnection())
            {
                try
                {
                    ExecuteNonQuery(
                        connection,
                        null,
                        "CREATE TEMPORARY TABLE `" + temporaryTable +
                        "` (Marker VARCHAR(64) NOT NULL);");

                    using (MySqlTransaction transaction = connection.BeginTransaction(
                        IsolationLevel.RepeatableRead))
                    {
                        using (MySqlCommand insert = new MySqlCommand(
                            "INSERT INTO `" + temporaryTable +
                            "` (Marker) VALUES (@Marker);",
                            connection,
                            transaction))
                        {
                            insert.Parameters.AddWithValue(
                                "@Marker",
                                "I17-" + Guid.NewGuid().ToString("N"));
                            Assert.That(insert.ExecuteNonQuery(), Is.EqualTo(1));
                        }

                        // Act
                        transaction.Rollback();
                    }

                    // Assert
                    using (MySqlCommand count = new MySqlCommand(
                        "SELECT COUNT(*) FROM `" + temporaryTable + "`;",
                        connection))
                    {
                        Assert.That(Convert.ToInt32(count.ExecuteScalar()), Is.Zero);
                    }
                }
                finally
                {
                    ExecuteNonQuery(
                        connection,
                        null,
                        "DROP TEMPORARY TABLE IF EXISTS `" + temporaryTable + "`;");
                }
            }
        }

        /// <summary>Confirms SQL-like input remains data when supplied through a parameter.</summary>
        [Test]
        public void ParameterizedPayload_DoesNotExecuteInjectedSql()
        {
            // Arrange
            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Require();
            const string payload = "x'; DROP TABLE tbl_user; --";

            // Act
            using (MySqlConnection connection = configuration.OpenConnection())
            using (MySqlCommand command = new MySqlCommand(
                "SELECT @Payload;",
                connection))
            {
                command.Parameters.AddWithValue("@Payload", payload);
                string result = Convert.ToString(command.ExecuteScalar());

                // Assert
                Assert.That(result, Is.EqualTo(payload));
            }
        }

        #endregion

        #region Helpers

        private static void ExecuteNonQuery(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string sql)
        {
            using (MySqlCommand command = new MySqlCommand(
                sql,
                connection,
                transaction))
            {
                command.ExecuteNonQuery();
            }
        }

        #endregion
    }
}
