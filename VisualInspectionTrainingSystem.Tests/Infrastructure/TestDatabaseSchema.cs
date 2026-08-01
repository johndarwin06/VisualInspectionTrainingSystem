#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Infrastructure
{
    /// <summary>
    /// Owns the version-controlled application schema used only after a permanent
    /// test identity marker has proved that the selected schema is disposable test state.
    /// </summary>
    internal static class TestDatabaseSchema
    {
        #region Constants

        /// <summary>Permanent marker table created only by the reviewed provisioning procedure.</summary>
        public const string MarkerTableName =
            "__vits_test_schema_marker";

        /// <summary>Stable marker row identity.</summary>
        public const string MarkerName =
            "VisualInspectionTrainingSystem.DatabaseTests";

        /// <summary>Non-secret signature proving the schema was intentionally provisioned for tests.</summary>
        public const string MarkerValue =
            "9B42F7D3563C4E8FBE3E9C4AB89C79E57A2C313C93574FB6A1848B7A6B61F52D";

        /// <summary>Current deterministic test schema version.</summary>
        public const int CurrentSchemaVersion = 1;

        private const string CreateUsersSql = @"
CREATE TABLE IF NOT EXISTS tbl_users
(
    UserID INT NOT NULL AUTO_INCREMENT,
    EmployeeNo VARCHAR(20) NOT NULL,
    FullName VARCHAR(100) NULL,
    PasswordHash VARCHAR(255) NULL,
    Role ENUM('Admin', 'User') NULL,
    Department VARCHAR(50) NULL,
    IsActive BIT(1) NULL DEFAULT b'1',
    CreatedDate DATETIME NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (UserID),
    UNIQUE KEY UX_tbl_users_EmployeeNo (EmployeeNo)
) ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";

        private const string CreateSessionsSql = @"
CREATE TABLE IF NOT EXISTS tbl_training_session
(
    SessionID INT NOT NULL AUTO_INCREMENT,
    EmployeeNo VARCHAR(20) NOT NULL,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME NULL,
    TotalQuestions INT NULL DEFAULT 0,
    CorrectAnswers INT NULL DEFAULT 0,
    WrongAnswers INT NULL DEFAULT 0,
    Accuracy DECIMAL(5,2) NULL DEFAULT 0.00,
    DuplicateKey VARCHAR(64) NULL,
    PRIMARY KEY (SessionID),
    UNIQUE KEY UX_tbl_training_session_DuplicateKey (DuplicateKey),
    CONSTRAINT FK_tbl_training_session_EmployeeNo
        FOREIGN KEY (EmployeeNo)
        REFERENCES tbl_users (EmployeeNo)
) ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";

        private const string CreateAnswersSql = @"
CREATE TABLE IF NOT EXISTS tbl_quiz_answer
(
    AnswerID INT NOT NULL AUTO_INCREMENT,
    SessionID INT NOT NULL,
    ImageID INT NOT NULL,
    ImageHash CHAR(64) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
    ImageFileName VARCHAR(255) NULL,
    UserAnswer VARCHAR(10) NULL,
    CorrectAnswer VARCHAR(10) NULL,
    IsCorrect BIT(1) NULL,
    AnswerTime DATETIME NULL,
    ReviewSource VARCHAR(20) NULL,
    ReviewedAt DATETIME NULL,
    ReviewedBy VARCHAR(20) NULL,
    PRIMARY KEY (AnswerID),
    KEY IX_tbl_quiz_answer_ImageHash (ImageHash),
    CONSTRAINT FK_tbl_quiz_answer_SessionID
        FOREIGN KEY (SessionID)
        REFERENCES tbl_training_session (SessionID)
) ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";

        private const string CreateTruthSql = @"
CREATE TABLE IF NOT EXISTS tbl_image_review_truth
(
    ImageHash CHAR(64) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    CorrectAnswer VARCHAR(10) NOT NULL,
    ReviewedAt DATETIME NOT NULL,
    SourceAnswerID INT NULL,
    ReviewerEmployeeNo VARCHAR(20) NULL,
    Version INT NOT NULL DEFAULT 1,
    LastUpdated DATETIME NOT NULL,
    PRIMARY KEY (ImageHash),
    CONSTRAINT CK_tbl_image_review_truth_CorrectAnswer
        CHECK (UPPER(TRIM(CorrectAnswer)) IN ('GOOD', 'NG'))
) ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";

        private static readonly string[] RequiredTableNames =
        {
            MarkerTableName,
            "tbl_image_review_truth",
            "tbl_quiz_answer",
            "tbl_training_session",
            "tbl_users"
        };

        private static readonly IDictionary<string, ColumnContract[]> ColumnContracts =
            CreateColumnContracts();

        #endregion

        #region Schema Lifecycle

        /// <summary>
        /// Creates or upgrades application tables inside an already marked test schema.
        /// It never creates, drops, or selects a database and never alters production settings.
        /// </summary>
        /// <param name="configuration">Marker-validated test configuration.</param>
        public static void EnsureCurrent(TestDatabaseConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            using (MySqlConnection connection = configuration.OpenConnection())
            {
                ExecuteNonQuery(connection, CreateUsersSql);
                ExecuteNonQuery(connection, CreateSessionsSql);
                ExecuteNonQuery(connection, CreateAnswersSql);
                ExecuteNonQuery(connection, CreateTruthSql);
                VerifyContract(connection, configuration.SchemaName);
                UpdateMarkerVersion(connection);
            }
        }

        /// <summary>
        /// Verifies the permanent marker without creating or repairing it.
        /// </summary>
        /// <param name="connection">Open candidate test connection.</param>
        /// <param name="schemaName">Separately validated schema name.</param>
        public static void ValidateMarker(
            MySqlConnection connection,
            string schemaName)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            const string existenceSql = @"
SELECT COUNT(*)
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_NAME = @MarkerTableName;";

            using (MySqlCommand command = new MySqlCommand(
                existenceSql,
                connection))
            {
                command.Parameters.AddWithValue("@SchemaName", schemaName);
                command.Parameters.AddWithValue("@MarkerTableName", MarkerTableName);

                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                {
                    throw new InvalidOperationException(
                        "The selected schema is not marked as a Visual Inspection test database.");
                }
            }

            string markerSql = @"
SELECT MarkerValue, SchemaVersion
FROM `" + MarkerTableName + @"`
WHERE MarkerName = @MarkerName
LIMIT 1;";

            using (MySqlCommand command = new MySqlCommand(markerSql, connection))
            {
                command.Parameters.AddWithValue("@MarkerName", MarkerName);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException(
                            "The test database identity marker row is missing.");
                    }

                    string value = reader.IsDBNull(0)
                        ? string.Empty
                        : reader.GetString(0);
                    int version = reader.IsDBNull(1)
                        ? -1
                        : reader.GetInt32(1);

                    if (!string.Equals(
                            value,
                            MarkerValue,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "The test database identity marker is invalid.");
                    }

                    if (version < 0 || version > CurrentSchemaVersion)
                    {
                        throw new InvalidOperationException(
                            "The test database schema version is unsupported.");
                    }
                }
            }
        }

        /// <summary>
        /// Verifies tables, columns, defaults, indexes, foreign keys, checks, and storage engine.
        /// </summary>
        /// <param name="connection">Open marker-validated connection.</param>
        /// <param name="schemaName">Validated schema identity.</param>
        public static void VerifyContract(
            MySqlConnection connection,
            string schemaName)
        {
            ValidateExactTableSet(connection, schemaName);
            ValidateColumns(connection, schemaName);
            ValidateEngines(connection, schemaName);
            ValidateIndexes(connection, schemaName);
            ValidateForeignKeys(connection, schemaName);
            ValidateTruthCheck(connection, schemaName);
        }

        #endregion

        #region Contract Validation

        private static void ValidateExactTableSet(
            MySqlConnection connection,
            string schemaName)
        {
            const string sql = @"
SELECT TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;";

            List<string> actual = new List<string>();

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@SchemaName", schemaName);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        actual.Add(reader.GetString(0));
                }
            }

            if (!actual.SequenceEqual(
                    RequiredTableNames.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "The dedicated test schema contains an unexpected or missing permanent table.");
            }
        }

        private static void ValidateColumns(
            MySqlConnection connection,
            string schemaName)
        {
            const string sql = @"
SELECT
    COLUMN_NAME,
    COLUMN_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT,
    EXTRA,
    COLLATION_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_NAME = @TableName
ORDER BY ORDINAL_POSITION;";

            foreach (KeyValuePair<string, ColumnContract[]> table in ColumnContracts)
            {
                List<ColumnContract> actual = new List<ColumnContract>();

                using (MySqlCommand command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@SchemaName", schemaName);
                    command.Parameters.AddWithValue("@TableName", table.Key);

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            actual.Add(new ColumnContract(
                                reader.GetString(0),
                                reader.GetString(1),
                                reader.GetString(2),
                                reader.IsDBNull(3)
                                    ? null
                                    : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture),
                                reader.GetString(4),
                                reader.IsDBNull(5) ? null : reader.GetString(5)));
                        }
                    }
                }

                if (actual.Count != table.Value.Length)
                    ThrowColumnDrift(table.Key);

                for (int index = 0; index < table.Value.Length; index++)
                {
                    if (!table.Value[index].Matches(actual[index]))
                        ThrowColumnDrift(table.Key);
                }
            }
        }

        private static void ValidateEngines(
            MySqlConnection connection,
            string schemaName)
        {
            const string sql = @"
SELECT COUNT(*)
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_NAME <> @MarkerTableName
  AND ENGINE = 'InnoDB';";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@SchemaName", schemaName);
                command.Parameters.AddWithValue("@MarkerTableName", MarkerTableName);

                if (Convert.ToInt32(command.ExecuteScalar()) != 4)
                {
                    throw new InvalidOperationException(
                        "The dedicated test schema storage-engine contract has drifted.");
                }
            }
        }

        private static void ValidateIndexes(
            MySqlConnection connection,
            string schemaName)
        {
            RequireIndex(connection, schemaName, MarkerTableName, "PRIMARY", true);
            RequireIndex(connection, schemaName, "tbl_users", "PRIMARY", true);
            RequireIndex(connection, schemaName, "tbl_users", "UX_tbl_users_EmployeeNo", true);
            RequireIndex(connection, schemaName, "tbl_training_session", "PRIMARY", true);
            RequireIndex(connection, schemaName, "tbl_training_session", "UX_tbl_training_session_DuplicateKey", true);
            RequireIndex(connection, schemaName, "tbl_quiz_answer", "PRIMARY", true);
            RequireIndex(connection, schemaName, "tbl_quiz_answer", "IX_tbl_quiz_answer_ImageHash", false);
            RequireIndex(connection, schemaName, "tbl_image_review_truth", "PRIMARY", true);
        }

        private static void RequireIndex(
            MySqlConnection connection,
            string schemaName,
            string tableName,
            string indexName,
            bool unique)
        {
            const string sql = @"
SELECT COUNT(*)
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_NAME = @TableName
  AND INDEX_NAME = @IndexName
  AND NON_UNIQUE = @NonUnique;";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@SchemaName", schemaName);
                command.Parameters.AddWithValue("@TableName", tableName);
                command.Parameters.AddWithValue("@IndexName", indexName);
                command.Parameters.AddWithValue("@NonUnique", unique ? 0 : 1);

                if (Convert.ToInt32(command.ExecuteScalar()) < 1)
                {
                    throw new InvalidOperationException(
                        "The dedicated test schema index contract has drifted.");
                }
            }
        }

        private static void ValidateForeignKeys(
            MySqlConnection connection,
            string schemaName)
        {
            RequireForeignKey(
                connection,
                schemaName,
                "tbl_training_session",
                "EmployeeNo",
                "tbl_users",
                "EmployeeNo");
            RequireForeignKey(
                connection,
                schemaName,
                "tbl_quiz_answer",
                "SessionID",
                "tbl_training_session",
                "SessionID");
        }

        private static void RequireForeignKey(
            MySqlConnection connection,
            string schemaName,
            string tableName,
            string columnName,
            string referencedTable,
            string referencedColumn)
        {
            const string sql = @"
SELECT COUNT(*)
FROM information_schema.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_NAME = @TableName
  AND COLUMN_NAME = @ColumnName
  AND REFERENCED_TABLE_SCHEMA = @SchemaName
  AND REFERENCED_TABLE_NAME = @ReferencedTable
  AND REFERENCED_COLUMN_NAME = @ReferencedColumn;";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@SchemaName", schemaName);
                command.Parameters.AddWithValue("@TableName", tableName);
                command.Parameters.AddWithValue("@ColumnName", columnName);
                command.Parameters.AddWithValue("@ReferencedTable", referencedTable);
                command.Parameters.AddWithValue("@ReferencedColumn", referencedColumn);

                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                {
                    throw new InvalidOperationException(
                        "The dedicated test schema foreign-key contract has drifted.");
                }
            }
        }

        private static void ValidateTruthCheck(
            MySqlConnection connection,
            string schemaName)
        {
            const string sql = @"
SELECT COUNT(*)
FROM information_schema.TABLE_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = @SchemaName
  AND TABLE_NAME = 'tbl_image_review_truth'
  AND CONSTRAINT_NAME = 'CK_tbl_image_review_truth_CorrectAnswer'
  AND CONSTRAINT_TYPE = 'CHECK';";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@SchemaName", schemaName);

                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                {
                    throw new InvalidOperationException(
                        "The dedicated test schema GOOD/NG check constraint has drifted.");
                }
            }
        }

        private static void ThrowColumnDrift(string tableName)
        {
            throw new InvalidOperationException(
                "The dedicated test schema column contract has drifted for " +
                tableName +
                ".");
        }

        #endregion

        #region Schema Commands

        private static void ExecuteNonQuery(
            MySqlConnection connection,
            string sql)
        {
            using (MySqlCommand command = new MySqlCommand(sql, connection))
                command.ExecuteNonQuery();
        }

        private static void UpdateMarkerVersion(MySqlConnection connection)
        {
            string sql = @"
UPDATE `" + MarkerTableName + @"`
SET SchemaVersion = @SchemaVersion
WHERE MarkerName = @MarkerName
  AND MarkerValue = @MarkerValue;";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@SchemaVersion", CurrentSchemaVersion);
                command.Parameters.AddWithValue("@MarkerName", MarkerName);
                command.Parameters.AddWithValue("@MarkerValue", MarkerValue);

                if (command.ExecuteNonQuery() != 1)
                {
                    throw new InvalidOperationException(
                        "The test database marker version could not be updated safely.");
                }
            }
        }

        #endregion

        #region Contract Definitions

        private static IDictionary<string, ColumnContract[]> CreateColumnContracts()
        {
            return new Dictionary<string, ColumnContract[]>(StringComparer.Ordinal)
            {
                {
                    MarkerTableName,
                    new[]
                    {
                        new ColumnContract("MarkerName", "varchar(64)", "NO", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("MarkerValue", "char(64)", "NO", null, string.Empty, "ascii_general_ci"),
                        new ColumnContract("SchemaVersion", "int", "NO", "0", string.Empty, null),
                        new ColumnContract("CreatedUtc", "timestamp", "NO", "CURRENT_TIMESTAMP", "DEFAULT_GENERATED", null)
                    }
                },
                {
                    "tbl_users",
                    new[]
                    {
                        new ColumnContract("UserID", "int", "NO", null, "auto_increment", null),
                        new ColumnContract("EmployeeNo", "varchar(20)", "NO", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("FullName", "varchar(100)", "YES", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("PasswordHash", "varchar(255)", "YES", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("Role", "enum('Admin','User')", "YES", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("Department", "varchar(50)", "YES", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("IsActive", "bit(1)", "YES", "b'1'", string.Empty, null),
                        new ColumnContract("CreatedDate", "datetime", "YES", "CURRENT_TIMESTAMP", "DEFAULT_GENERATED", null)
                    }
                },
                {
                    "tbl_training_session",
                    new[]
                    {
                        new ColumnContract("SessionID", "int", "NO", null, "auto_increment", null),
                        new ColumnContract("EmployeeNo", "varchar(20)", "NO", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("StartTime", "datetime", "NO", null, string.Empty, null),
                        new ColumnContract("EndTime", "datetime", "YES", null, string.Empty, null),
                        new ColumnContract("TotalQuestions", "int", "YES", "0", string.Empty, null),
                        new ColumnContract("CorrectAnswers", "int", "YES", "0", string.Empty, null),
                        new ColumnContract("WrongAnswers", "int", "YES", "0", string.Empty, null),
                        new ColumnContract("Accuracy", "decimal(5,2)", "YES", "0.00", string.Empty, null),
                        new ColumnContract("DuplicateKey", "varchar(64)", "YES", null, string.Empty, "utf8mb4_0900_ai_ci")
                    }
                },
                {
                    "tbl_quiz_answer",
                    new[]
                    {
                        new ColumnContract("AnswerID", "int", "NO", null, "auto_increment", null),
                        new ColumnContract("SessionID", "int", "NO", null, string.Empty, null),
                        new ColumnContract("ImageID", "int", "NO", null, string.Empty, null),
                        new ColumnContract("ImageHash", "char(64)", "YES", null, string.Empty, "ascii_general_ci"),
                        new ColumnContract("ImageFileName", "varchar(255)", "YES", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("UserAnswer", "varchar(10)", "YES", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("CorrectAnswer", "varchar(10)", "YES", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("IsCorrect", "bit(1)", "YES", null, string.Empty, null),
                        new ColumnContract("AnswerTime", "datetime", "YES", null, string.Empty, null),
                        new ColumnContract("ReviewSource", "varchar(20)", "YES", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("ReviewedAt", "datetime", "YES", null, string.Empty, null),
                        new ColumnContract("ReviewedBy", "varchar(20)", "YES", null, string.Empty, "utf8mb4_0900_ai_ci")
                    }
                },
                {
                    "tbl_image_review_truth",
                    new[]
                    {
                        new ColumnContract("ImageHash", "char(64)", "NO", null, string.Empty, "ascii_general_ci"),
                        new ColumnContract("CorrectAnswer", "varchar(10)", "NO", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("ReviewedAt", "datetime", "NO", null, string.Empty, null),
                        new ColumnContract("SourceAnswerID", "int", "YES", null, string.Empty, null),
                        new ColumnContract("ReviewerEmployeeNo", "varchar(20)", "YES", null, string.Empty, "utf8mb4_0900_ai_ci"),
                        new ColumnContract("Version", "int", "NO", "1", string.Empty, null),
                        new ColumnContract("LastUpdated", "datetime", "NO", null, string.Empty, null)
                    }
                }
            };
        }

        private sealed class ColumnContract
        {
            public ColumnContract(
                string name,
                string type,
                string nullable,
                string defaultValue,
                string extra,
                string collation)
            {
                Name = name;
                Type = type;
                Nullable = nullable;
                DefaultValue = defaultValue;
                Extra = extra;
                Collation = collation;
            }

            public string Name { get; private set; }

            public string Type { get; private set; }

            public string Nullable { get; private set; }

            public string DefaultValue { get; private set; }

            public string Extra { get; private set; }

            public string Collation { get; private set; }

            public bool Matches(ColumnContract other)
            {
                return other != null &&
                       EqualsText(Name, other.Name, StringComparison.Ordinal) &&
                       EqualsText(Type, other.Type, StringComparison.OrdinalIgnoreCase) &&
                       EqualsText(Nullable, other.Nullable, StringComparison.Ordinal) &&
                       EqualsText(DefaultValue, other.DefaultValue, StringComparison.OrdinalIgnoreCase) &&
                       EqualsText(Extra, other.Extra, StringComparison.OrdinalIgnoreCase) &&
                       EqualsText(Collation, other.Collation, StringComparison.OrdinalIgnoreCase);
            }

            private static bool EqualsText(
                string left,
                string right,
                StringComparison comparison)
            {
                return string.Equals(
                    left ?? string.Empty,
                    right ?? string.Empty,
                    comparison);
            }
        }

        #endregion
    }
}
