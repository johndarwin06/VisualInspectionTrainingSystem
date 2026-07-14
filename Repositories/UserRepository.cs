#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Data;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.Repositories
{
    /// <summary>
    /// Provides database access for tbl_users.
    /// </summary>
    public class UserRepository
    {
        #region Fields

        private readonly MySqlService _database;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the user repository.
        /// </summary>
        public UserRepository()
        {
            _database = new MySqlService();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a user by Employee Number.
        /// </summary>
        /// <param name="employeeNo">The employee number to find.</param>
        /// <returns>The matching user, or null when no user exists.</returns>
        public User GetByEmployeeNo(string employeeNo)
        {
            string validatedEmployeeNo = ValidateEmployeeNo(employeeNo);

            const string sql = @"
SELECT
    UserID,
    EmployeeNo,
    FullName,
    PasswordHash,
    Role,
    Department,
    IsActive,
    CreatedDate
FROM tbl_users
WHERE EmployeeNo = @EmployeeNo
LIMIT 1;";

            try
            {
                DataTable table = _database.ExecuteDataTable(
                    sql,
                    new MySqlParameter("@EmployeeNo", validatedEmployeeNo));

                if (table.Rows.Count == 0)
                {
                    return null;
                }

                return MapUser(table.Rows[0]);
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Updates the stored password hash for a user.
        /// </summary>
        /// <param name="employeeNo">The employee number to update.</param>
        /// <param name="passwordHash">The BCrypt password hash to store.</param>
        public void UpdatePasswordHash(
            string employeeNo,
            string passwordHash)
        {
            string validatedEmployeeNo = ValidateEmployeeNo(employeeNo);

            if (passwordHash == null)
                throw new ArgumentNullException(nameof(passwordHash));

            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException(
                    "Password hash must not be empty.",
                    nameof(passwordHash));
            }

            const string sql = @"
UPDATE tbl_users
SET PasswordHash = @PasswordHash
WHERE EmployeeNo = @EmployeeNo
LIMIT 1;";

            try
            {
                int affectedRows = _database.ExecuteNonQuery(
                    sql,
                    new MySqlParameter("@PasswordHash", passwordHash),
                    new MySqlParameter("@EmployeeNo", validatedEmployeeNo));

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException(
                        "Password hash update did not affect exactly one user.");
                }
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        #endregion

        #region Mapping

        /// <summary>
        /// Maps a database row to a user model.
        /// </summary>
        private static User MapUser(DataRow row)
        {
            if (row == null)
                throw new ArgumentNullException(nameof(row));

            return new User
            {
                UserID = ReadRequiredInt(row, "UserID"),
                EmployeeNo = ReadRequiredString(row, "EmployeeNo"),
                FullName = ReadOptionalString(row, "FullName"),
                PasswordHash = ReadOptionalString(row, "PasswordHash"),
                Role = ReadOptionalString(row, "Role"),
                Department = ReadOptionalString(row, "Department"),
                IsActive = ReadFailClosedBoolean(row, "IsActive"),
                CreatedDate = ReadOptionalDate(row, "CreatedDate", DateTime.MinValue)
            };
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates an employee number for user lookup and updates.
        /// </summary>
        private static string ValidateEmployeeNo(string employeeNo)
        {
            if (employeeNo == null)
                throw new ArgumentNullException(nameof(employeeNo));

            if (string.IsNullOrWhiteSpace(employeeNo))
            {
                throw new ArgumentException(
                    "EmployeeNo must not be empty.",
                    nameof(employeeNo));
            }

            return employeeNo.Trim();
        }

        #endregion

        #region Conversion Helpers

        /// <summary>
        /// Reads a required integer column.
        /// </summary>
        private static int ReadRequiredInt(
            DataRow row,
            string columnName)
        {
            object value = row[columnName];

            if (value == null ||
                value == DBNull.Value)
            {
                throw new InvalidOperationException(
                    columnName + " is required.");
            }

            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Reads a required string column.
        /// </summary>
        private static string ReadRequiredString(
            DataRow row,
            string columnName)
        {
            object value = row[columnName];

            if (value == null ||
                value == DBNull.Value ||
                string.IsNullOrWhiteSpace(value.ToString()))
            {
                throw new InvalidOperationException(
                    columnName + " is required.");
            }

            return value.ToString();
        }

        /// <summary>
        /// Reads an optional string column.
        /// </summary>
        private static string ReadOptionalString(
            DataRow row,
            string columnName)
        {
            object value = row[columnName];

            if (value == null ||
                value == DBNull.Value)
            {
                return string.Empty;
            }

            return value.ToString();
        }

        /// <summary>
        /// Reads an activation flag and treats missing or malformed data as inactive.
        /// </summary>
        private static bool ReadFailClosedBoolean(
            DataRow row,
            string columnName)
        {
            object value = row[columnName];

            if (value == null ||
                value == DBNull.Value)
            {
                return false;
            }

            try
            {
                return Convert.ToBoolean(value);
            }
            catch (FormatException)
            {
                return false;
            }
            catch (InvalidCastException)
            {
                return false;
            }
        }

        /// <summary>
        /// Reads an optional DateTime column.
        /// </summary>
        private static DateTime ReadOptionalDate(
            DataRow row,
            string columnName,
            DateTime defaultValue)
        {
            object value = row[columnName];

            if (value == null ||
                value == DBNull.Value)
            {
                return defaultValue;
            }

            return Convert.ToDateTime(value);
        }

        #endregion
    }
}
