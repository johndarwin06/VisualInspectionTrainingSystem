#region Namespaces

using NUnit.Framework;
using System;
using VisualInspectionTrainingSystem.Repositories;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Unit
{
    /// <summary>
    /// Verifies database-test isolation rules without opening a network connection.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Unit)]
    public sealed class TestDatabaseConfigurationTests
    {
        #region Constants

        private const string ProductionConnection =
            "Server=localhost;Port=3306;Database=visualinspectionquiz;User ID=production_placeholder;SslMode=Disabled;";

        private const string SafeTestConnection =
            "Server=127.0.0.1;Port=3306;Database=visual_inspection_training_test;User ID=test_runner_placeholder;SslMode=Disabled;Pooling=true;Connection Timeout=60;Default Command Timeout=90;";

        #endregion

        #region Validation Tests

        /// <summary>Rejects incomplete configuration without attempting a connection.</summary>
        [TestCase(null, TestDatabaseConfiguration.RequiredSchemaPrefix)]
        [TestCase("", TestDatabaseConfiguration.RequiredSchemaPrefix)]
        [TestCase(SafeTestConnection, null)]
        [TestCase(SafeTestConnection, "")]
        public void Validate_IncompleteConfiguration_FailsClosed(
            string connectionString,
            string schema)
        {
            Assert.That(
                delegate
                {
                    TestDatabaseConfiguration.Validate(
                        connectionString,
                        schema,
                        ProductionConnection);
                },
                Throws.TypeOf<InvalidOperationException>());
        }

        /// <summary>Rejects malformed candidate and production identities.</summary>
        [Test]
        public void Validate_MalformedConnectionIdentity_FailsClosed()
        {
            Assert.Multiple(delegate
            {
                Assert.That(
                    delegate
                    {
                        TestDatabaseConfiguration.Validate(
                            "not a connection string",
                            TestDatabaseConfiguration.RequiredSchemaPrefix,
                            ProductionConnection);
                    },
                    Throws.TypeOf<InvalidOperationException>());

                Assert.That(
                    delegate
                    {
                        TestDatabaseConfiguration.Validate(
                            SafeTestConnection,
                            TestDatabaseConfiguration.RequiredSchemaPrefix,
                            "not a connection string");
                    },
                    Throws.TypeOf<InvalidOperationException>());
            });
        }

        /// <summary>Requires the separately declared schema to match exactly.</summary>
        [Test]
        public void Validate_DeclaredSchemaMismatch_FailsClosed()
        {
            Assert.That(
                delegate
                {
                    TestDatabaseConfiguration.Validate(
                        SafeTestConnection,
                        TestDatabaseConfiguration.RequiredSchemaPrefix + "_other",
                        ProductionConnection);
                },
                Throws.TypeOf<InvalidOperationException>());
        }

        /// <summary>Rejects system, default, ambiguous, and production-looking schema names.</summary>
        [TestCase("mysql")]
        [TestCase("information_schema")]
        [TestCase("performance_schema")]
        [TestCase("sys")]
        [TestCase("test")]
        [TestCase("visualinspectionquiz")]
        [TestCase("visual_inspection_training_test_prod")]
        [TestCase("visual_inspection_training_test_live")]
        public void Validate_UnsafeSchemaName_FailsClosed(string schema)
        {
            string connection =
                "Server=localhost;Port=3306;Database=" +
                schema +
                ";User ID=test_runner_placeholder;SslMode=Disabled;";

            Assert.That(
                delegate
                {
                    TestDatabaseConfiguration.Validate(
                        connection,
                        schema,
                        ProductionConnection);
                },
                Throws.TypeOf<InvalidOperationException>());
        }

        /// <summary>Rejects the normal application schema even when separately declared.</summary>
        [Test]
        public void Validate_ProductionTarget_FailsClosed()
        {
            const string sameTarget =
                "Server=127.0.0.1;Port=3306;Database=visualinspectionquiz;User ID=production_placeholder;SslMode=Disabled;";

            Assert.That(
                delegate
                {
                    TestDatabaseConfiguration.Validate(
                        sameTarget,
                        "visualinspectionquiz",
                        ProductionConnection);
                },
                Throws.TypeOf<InvalidOperationException>());
        }

        /// <summary>Rejects reuse of the production account on the same database endpoint.</summary>
        [Test]
        public void Validate_ProductionAccountReuse_FailsClosed()
        {
            const string sharedAccountTestConnection =
                "Server=127.0.0.1;Port=3306;Database=visual_inspection_training_test;" +
                "User ID=production_placeholder;SslMode=Disabled;";

            Assert.That(
                delegate
                {
                    TestDatabaseConfiguration.Validate(
                        sharedAccountTestConnection,
                        TestDatabaseConfiguration.RequiredSchemaPrefix,
                        ProductionConnection);
                },
                Throws.TypeOf<InvalidOperationException>());
        }

        /// <summary>Accepts only the exact test prefix and returns credential-free metadata.</summary>
        [Test]
        public void Validate_DedicatedSchema_ReturnsSafeMetadata()
        {
            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Validate(
                    SafeTestConnection,
                    TestDatabaseConfiguration.RequiredSchemaPrefix,
                    ProductionConnection);

            string metadata = configuration.GetSafeMetadata();

            Assert.Multiple(delegate
            {
                Assert.That(
                    configuration.SchemaName,
                    Is.EqualTo(TestDatabaseConfiguration.RequiredSchemaPrefix));
                Assert.That(configuration.IsLocalEndpoint, Is.True);
                Assert.That(metadata, Does.Contain("Endpoint=Local"));
                Assert.That(metadata, Does.Contain("Port=3306"));
                Assert.That(metadata, Does.Contain("DedicatedAccountConfigured=True"));
                Assert.That(metadata, Does.Not.Contain("test_runner_placeholder"));
                Assert.That(metadata, Does.Not.Contain("production_placeholder"));
                Assert.That(metadata, Does.Not.Contain("Password"));
            });
        }

        /// <summary>Accepts an isolated suffix composed only of safe identifier characters.</summary>
        [Test]
        public void Validate_IsolatedSchemaSuffix_IsSupported()
        {
            const string schema = "visual_inspection_training_test_ci01";
            string connection =
                "Server=db-test.example.invalid;Port=3307;Database=" +
                schema +
                ";User ID=test_runner_placeholder;SslMode=Required;";

            TestDatabaseConfiguration configuration =
                TestDatabaseConfiguration.Validate(
                    connection,
                    schema,
                    ProductionConnection);

            Assert.That(configuration.SchemaName, Is.EqualTo(schema));
        }

        /// <summary>Allows only canonical active values and fails closed for malformed database values.</summary>
        [Test]
        public void IsActiveConversion_NullAndMalformedValues_FailClosed()
        {
            Assert.Multiple(delegate
            {
                Assert.That(UserRepository.ConvertFailClosedBoolean(true), Is.True);
                Assert.That(UserRepository.ConvertFailClosedBoolean(1), Is.True);
                Assert.That(UserRepository.ConvertFailClosedBoolean("1"), Is.True);
                Assert.That(UserRepository.ConvertFailClosedBoolean("true"), Is.True);
                Assert.That(UserRepository.ConvertFailClosedBoolean(false), Is.False);
                Assert.That(UserRepository.ConvertFailClosedBoolean(0), Is.False);
                Assert.That(UserRepository.ConvertFailClosedBoolean(null), Is.False);
                Assert.That(UserRepository.ConvertFailClosedBoolean(DBNull.Value), Is.False);
                Assert.That(UserRepository.ConvertFailClosedBoolean(2), Is.False);
                Assert.That(UserRepository.ConvertFailClosedBoolean(-1), Is.False);
                Assert.That(UserRepository.ConvertFailClosedBoolean("yes"), Is.False);
                Assert.That(UserRepository.ConvertFailClosedBoolean("invalid"), Is.False);
            });
        }

        #endregion
    }
}
