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

        #region Get User By Employee Number

        /// <summary>
        /// Returns a user by Employee Number.
        /// </summary>
        /// <param name="employeeNo">The employee number to find.</param>
        /// <returns>The matching user, or null when no user exists.</returns>
        public User GetByEmployeeNo(string employeeNo)
        {
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

            DataTable table = _database.ExecuteDataTable(
                sql,
                new MySqlParameter("@EmployeeNo", employeeNo));

            if (table.Rows.Count == 0)
            {
                return null;
            }

            DataRow row = table.Rows[0];

            return new User
            {
                UserID = Convert.ToInt32(row["UserID"]),
                EmployeeNo = row["EmployeeNo"].ToString(),
                FullName = row["FullName"].ToString(),
                PasswordHash = row["PasswordHash"] == DBNull.Value
                    ? string.Empty
                    : row["PasswordHash"].ToString(),
                Role = row["Role"].ToString(),
                Department = row["Department"].ToString(),
                IsActive = Convert.ToBoolean(row["IsActive"]),
                CreatedDate = Convert.ToDateTime(row["CreatedDate"])
            };
        }

        #endregion

        #region Password Migration

        /// <summary>
        /// Updates the stored password hash for a user.
        /// </summary>
        /// <param name="employeeNo">The employee number to update.</param>
        /// <param name="passwordHash">The BCrypt password hash to store.</param>
        public void UpdatePasswordHash(
            string employeeNo,
            string passwordHash)
        {
            const string sql = @"
UPDATE tbl_users
SET PasswordHash = @PasswordHash
WHERE EmployeeNo = @EmployeeNo
LIMIT 1;";

            _database.ExecuteNonQuery(
                sql,
                new MySqlParameter("@PasswordHash", passwordHash),
                new MySqlParameter("@EmployeeNo", employeeNo));
        }

        #endregion
    }
}
