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

        public UserRepository()
        {
            _database = new MySqlService();
        }

        #endregion

        #region Get User By Employee Number

        /// <summary>
        /// Returns a user by Employee Number.
        /// </summary>
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
                return null;

            DataRow row = table.Rows[0];

            return new User
            {
                UserID = Convert.ToInt32(row["UserID"]),
                EmployeeNo = row["EmployeeNo"].ToString(),
                FullName = row["FullName"].ToString(),
                PasswordHash = row["PasswordHash"].ToString(),
                Role = row["Role"].ToString(),
                Department = row["Department"].ToString(),
                IsActive = Convert.ToBoolean(row["IsActive"]),
                CreatedDate = Convert.ToDateTime(row["CreatedDate"])
            };
        }

        #endregion
    }
}