#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.Repositories
{
    /// <summary>
    /// Provides authentication and transactional administrator access to tbl_users.
    /// Password values are excluded from every user-management projection.
    /// </summary>
    public class UserRepository
    {
        #region Constants

        public const int EmployeeNumberMaximumLength = 20;
        public const int FullNameMaximumLength = 100;
        public const int DepartmentMaximumLength = 50;
        public const int PasswordHashMaximumLength = 255;

        private const string AuthorizationMessage =
            "Administrator authorization is required for User Management.";

        private const string DuplicateEmployeeMessage =
            "An account with that Employee Number already exists.";

        private const string UserNotFoundMessage =
            "The selected user is no longer available. Refresh User Management and try again.";

        private const string SelfDisableMessage =
            "You cannot disable your own active administrator session.";

        private const string SelfDemotionMessage =
            "You cannot change your own active administrator role.";

        private const string FinalAdministratorMessage =
            "The final active administrator cannot be disabled or changed to User.";

        private const string ConcurrentChangeMessage =
            "The selected user changed in another operation. Refresh User Management and try again.";

        private const string StorageMessage =
            "The user-management change could not be completed. No changes were saved.";

        private const string LoadMessage =
            "The user list could not be loaded. Please try again or contact support if the problem continues.";

        private const string UnsafeSchemaMessage =
            "User Management cannot start because existing Employee Number data is not safe for a uniqueness guarantee.";

        #endregion

        #region Fields

        private readonly MySqlService _database;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes database access using the configured MySQL service.
        /// </summary>
        public UserRepository()
        {
            _database = new MySqlService();
        }

        #endregion

        #region Authentication Methods

        /// <summary>
        /// Returns a user by Employee Number for authentication.
        /// </summary>
        /// <param name="employeeNo">The employee number to find.</param>
        /// <returns>The matching user, or null when no user exists.</returns>
        public User GetByEmployeeNo(string employeeNo)
        {
            string validatedEmployeeNo = ValidateLookupEmployeeNo(employeeNo);

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
                    return null;

                return MapAuthenticationUser(table.Rows[0]);
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Updates the stored password value for compatibility with existing callers.
        /// New user-management password changes use the authorized transactional method.
        /// </summary>
        /// <param name="employeeNo">The employee number to update.</param>
        /// <param name="passwordHash">The BCrypt password hash to store.</param>
        public void UpdatePasswordHash(
            string employeeNo,
            string passwordHash)
        {
            string validatedEmployeeNo = ValidateLookupEmployeeNo(employeeNo);
            ValidatePasswordHash(passwordHash);

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

        /// <summary>
        /// Upgrades a legacy password only when the stored value has not changed concurrently.
        /// </summary>
        /// <param name="employeeNo">Employee number to update.</param>
        /// <param name="expectedStoredPassword">Legacy value that was just authenticated.</param>
        /// <param name="passwordHash">Replacement BCrypt hash.</param>
        /// <returns>True when the expected legacy value was replaced; otherwise false.</returns>
        public bool TryUpgradeLegacyPassword(
            string employeeNo,
            string expectedStoredPassword,
            string passwordHash)
        {
            string validatedEmployeeNo = ValidateLookupEmployeeNo(employeeNo);

            if (expectedStoredPassword == null)
                throw new ArgumentNullException(nameof(expectedStoredPassword));

            ValidatePasswordHash(passwordHash);

            const string sql = @"
UPDATE tbl_users
SET PasswordHash = @PasswordHash
WHERE EmployeeNo = @EmployeeNo
  AND PasswordHash = @ExpectedStoredPassword
LIMIT 1;";

            try
            {
                int affectedRows = _database.ExecuteNonQuery(
                    sql,
                    new MySqlParameter("@PasswordHash", passwordHash),
                    new MySqlParameter("@EmployeeNo", validatedEmployeeNo),
                    new MySqlParameter("@ExpectedStoredPassword", expectedStoredPassword));

                return affectedRows == 1;
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        #endregion

        #region User Management Schema

        /// <summary>
        /// Validates the existing user schema and adds a unique Employee Number index only when safe.
        /// </summary>
        public void EnsureManagementSchema()
        {
            try
            {
                _database.OpenConnection();
                MySqlConnection connection = _database.GetConnection();

                if (!UserTableExists(connection))
                {
                    throw new UserManagementException(
                        UserManagementErrorCode.Storage,
                        StorageMessage);
                }

                if (HasUniqueEmployeeNumberIndex(connection))
                    return;

                if (CountUnsafeEmployeeNumberRows(connection) != 0)
                {
                    throw new UserManagementException(
                        UserManagementErrorCode.Storage,
                        UnsafeSchemaMessage);
                }

                try
                {
                    using (MySqlCommand command = new MySqlCommand(
                               "ALTER TABLE tbl_users ADD UNIQUE INDEX UX_tbl_users_EmployeeNo (EmployeeNo);",
                               connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch (MySqlException ex)
                {
                    if (!HasUniqueEmployeeNumberIndex(connection))
                    {
                        throw new UserManagementException(
                            UserManagementErrorCode.Storage,
                            UnsafeSchemaMessage,
                            ex);
                    }
                }
            }
            catch (UserManagementException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Storage,
                    StorageMessage,
                    ex);
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        #endregion

        #region User Management Queries

        /// <summary>
        /// Returns the safe administrator user list without password values.
        /// </summary>
        /// <param name="actorEmployeeNo">Current administrator Employee Number.</param>
        /// <returns>Deterministically ordered users with an empty PasswordHash property.</returns>
        public IList<User> GetAllForManagement(string actorEmployeeNo)
        {
            string actor = ValidateLookupEmployeeNo(actorEmployeeNo);
            EnsureManagementSchema();

            const string sql = @"
SELECT
    UserID,
    EmployeeNo,
    FullName,
    Role,
    Department,
    IsActive,
    CreatedDate
FROM tbl_users
ORDER BY EmployeeNo, UserID;";

            try
            {
                _database.OpenConnection();
                MySqlConnection connection = _database.GetConnection();
                EnsureAuthorizedAdministrator(connection, null, actor);

                List<User> users = new List<User>();

                using (MySqlCommand command = new MySqlCommand(sql, connection))
                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        users.Add(MapManagementUser(reader));
                }

                return users;
            }
            catch (UserManagementException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Storage,
                    LoadMessage,
                    ex);
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        #endregion

        #region User Management Mutations

        /// <summary>
        /// Creates one active user through the serialized management transaction.
        /// </summary>
        public User CreateUser(
            string actorEmployeeNo,
            string employeeNo,
            string fullName,
            string department,
            string role,
            string passwordHash)
        {
            string actor = ValidateLookupEmployeeNo(actorEmployeeNo);
            string normalizedEmployeeNo = ValidateManagedEmployeeNo(employeeNo);
            string validatedFullName = ValidateText(
                fullName,
                FullNameMaximumLength,
                "Full Name");
            string validatedDepartment = ValidateText(
                department,
                DepartmentMaximumLength,
                "Department");
            string canonicalRole = ValidateRole(role);
            ValidatePasswordHash(passwordHash);

            return ExecuteMutation(
                delegate(
                    MySqlConnection connection,
                    MySqlTransaction transaction,
                    IList<LockedUser> users)
                {
                    EnsureAuthorizedAdministrator(users, actor);

                    if (users.Any(user => EmployeeNumbersEqual(
                        user.EmployeeNo,
                        normalizedEmployeeNo)))
                    {
                        throw new UserManagementException(
                            UserManagementErrorCode.DuplicateEmployeeNumber,
                            DuplicateEmployeeMessage);
                    }

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
    @FullName,
    @PasswordHash,
    @Role,
    @Department,
    1,
    CURRENT_TIMESTAMP
);";

                    int userId;

                    using (MySqlCommand command = new MySqlCommand(
                               sql,
                               connection,
                               transaction))
                    {
                        command.Parameters.AddWithValue(
                            "@EmployeeNo",
                            normalizedEmployeeNo);
                        command.Parameters.AddWithValue(
                            "@FullName",
                            validatedFullName);
                        command.Parameters.AddWithValue(
                            "@PasswordHash",
                            passwordHash);
                        command.Parameters.AddWithValue(
                            "@Role",
                            canonicalRole);
                        command.Parameters.AddWithValue(
                            "@Department",
                            validatedDepartment);
                        command.ExecuteNonQuery();
                        userId = Convert.ToInt32(command.LastInsertedId);
                    }

                    return LoadManagementUser(
                        connection,
                        transaction,
                        userId);
                });
        }

        /// <summary>
        /// Creates one inactive canonical trainee account without accepting a caller-supplied role or activation state.
        /// The database uniqueness guarantee and serialized transaction make concurrent duplicate registration deterministic.
        /// </summary>
        /// <param name="employeeNo">Normalized employee number requested by the registrant.</param>
        /// <param name="fullName">Validated full name.</param>
        /// <param name="department">Validated department.</param>
        /// <param name="passwordHash">BCrypt hash created by the trusted registration service.</param>
        /// <returns>An inactive safe account projection with no password hash.</returns>
        public User RegisterInactiveTrainee(
            string employeeNo,
            string fullName,
            string department,
            string passwordHash)
        {
            string normalizedEmployeeNo = ValidateManagedEmployeeNo(employeeNo);
            string validatedFullName = ValidateText(
                fullName,
                FullNameMaximumLength,
                "Full Name");
            string validatedDepartment = ValidateText(
                department,
                DepartmentMaximumLength,
                "Department");
            ValidatePasswordHash(passwordHash);

            return ExecuteMutation(
                delegate(
                    MySqlConnection connection,
                    MySqlTransaction transaction,
                    IList<LockedUser> users)
                {
                    if (users.Any(user => EmployeeNumbersEqual(
                        user.EmployeeNo,
                        normalizedEmployeeNo)))
                    {
                        throw new UserManagementException(
                            UserManagementErrorCode.DuplicateEmployeeNumber,
                            DuplicateEmployeeMessage);
                    }

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
    @FullName,
    @PasswordHash,
    @Role,
    @Department,
    0,
    CURRENT_TIMESTAMP
);";

                    int userId;

                    using (MySqlCommand command = new MySqlCommand(
                               sql,
                               connection,
                               transaction))
                    {
                        command.Parameters.AddWithValue(
                            "@EmployeeNo",
                            normalizedEmployeeNo);
                        command.Parameters.AddWithValue(
                            "@FullName",
                            validatedFullName);
                        command.Parameters.AddWithValue(
                            "@PasswordHash",
                            passwordHash);
                        command.Parameters.AddWithValue(
                            "@Role",
                            UserRoles.User);
                        command.Parameters.AddWithValue(
                            "@Department",
                            validatedDepartment);
                        command.ExecuteNonQuery();
                        userId = Convert.ToInt32(command.LastInsertedId);
                    }

                    return LoadManagementUser(
                        connection,
                        transaction,
                        userId);
                });
        }

        /// <summary>
        /// Disables or reactivates a user with stale-state, self, and final-administrator protection.
        /// </summary>
        public void SetUserActive(
            string actorEmployeeNo,
            int targetUserId,
            bool expectedIsActive,
            bool newIsActive)
        {
            string actor = ValidateLookupEmployeeNo(actorEmployeeNo);
            ValidateUserId(targetUserId);

            ExecuteMutation(
                delegate(
                    MySqlConnection connection,
                    MySqlTransaction transaction,
                    IList<LockedUser> users)
                {
                    LockedUser administrator = EnsureAuthorizedAdministrator(
                        users,
                        actor);
                    LockedUser target = FindTargetUser(users, targetUserId);

                    if (target.IsActive != expectedIsActive)
                        ThrowConcurrentChange();

                    if (target.IsActive == newIsActive)
                        return true;

                    if (!newIsActive &&
                        target.IsActive &&
                        string.Equals(target.Role, UserRoles.Admin, StringComparison.Ordinal) &&
                        CountActiveAdministrators(users) <= 1)
                    {
                        throw new UserManagementException(
                            UserManagementErrorCode.FinalAdministrator,
                            FinalAdministratorMessage);
                    }

                    if (!newIsActive && target.UserID == administrator.UserID)
                    {
                        throw new UserManagementException(
                            UserManagementErrorCode.SelfProtection,
                            SelfDisableMessage);
                    }

                    const string sql = @"
UPDATE tbl_users
SET IsActive = @IsActive
WHERE UserID = @UserID
  AND IsActive = @ExpectedIsActive
LIMIT 1;";

                    using (MySqlCommand command = new MySqlCommand(
                               sql,
                               connection,
                               transaction))
                    {
                        command.Parameters.AddWithValue("@IsActive", newIsActive);
                        command.Parameters.AddWithValue("@UserID", targetUserId);
                        command.Parameters.AddWithValue(
                            "@ExpectedIsActive",
                            expectedIsActive);

                        if (command.ExecuteNonQuery() != 1)
                            ThrowConcurrentChange();
                    }

                    return true;
                });
        }

        /// <summary>
        /// Changes a user's canonical role with stale-state, self, and final-administrator protection.
        /// </summary>
        public void SetUserRole(
            string actorEmployeeNo,
            int targetUserId,
            string expectedRole,
            string newRole)
        {
            string actor = ValidateLookupEmployeeNo(actorEmployeeNo);
            ValidateUserId(targetUserId);
            string expectedCanonicalRole = ValidateRole(expectedRole);
            string newCanonicalRole = ValidateRole(newRole);

            ExecuteMutation(
                delegate(
                    MySqlConnection connection,
                    MySqlTransaction transaction,
                    IList<LockedUser> users)
                {
                    LockedUser administrator = EnsureAuthorizedAdministrator(
                        users,
                        actor);
                    LockedUser target = FindTargetUser(users, targetUserId);

                    if (!string.Equals(
                        target.Role,
                        expectedCanonicalRole,
                        StringComparison.Ordinal))
                    {
                        ThrowConcurrentChange();
                    }

                    if (string.Equals(
                        target.Role,
                        newCanonicalRole,
                        StringComparison.Ordinal))
                    {
                        return true;
                    }

                    bool demotingAdministrator =
                        string.Equals(target.Role, UserRoles.Admin, StringComparison.Ordinal) &&
                        !string.Equals(newCanonicalRole, UserRoles.Admin, StringComparison.Ordinal);

                    if (demotingAdministrator &&
                        target.IsActive &&
                        CountActiveAdministrators(users) <= 1)
                    {
                        throw new UserManagementException(
                            UserManagementErrorCode.FinalAdministrator,
                            FinalAdministratorMessage);
                    }

                    if (demotingAdministrator &&
                        target.UserID == administrator.UserID)
                    {
                        throw new UserManagementException(
                            UserManagementErrorCode.SelfProtection,
                            SelfDemotionMessage);
                    }

                    const string sql = @"
UPDATE tbl_users
SET Role = @Role
WHERE UserID = @UserID
  AND Role = @ExpectedRole
LIMIT 1;";

                    using (MySqlCommand command = new MySqlCommand(
                               sql,
                               connection,
                               transaction))
                    {
                        command.Parameters.AddWithValue("@Role", newCanonicalRole);
                        command.Parameters.AddWithValue("@UserID", targetUserId);
                        command.Parameters.AddWithValue(
                            "@ExpectedRole",
                            expectedCanonicalRole);

                        if (command.ExecuteNonQuery() != 1)
                            ThrowConcurrentChange();
                    }

                    return true;
                });
        }

        /// <summary>
        /// Replaces a user's password with an existing BCrypt hash inside an authorized transaction.
        /// </summary>
        public void ResetPassword(
            string actorEmployeeNo,
            int targetUserId,
            string passwordHash)
        {
            string actor = ValidateLookupEmployeeNo(actorEmployeeNo);
            ValidateUserId(targetUserId);
            ValidatePasswordHash(passwordHash);

            ExecuteMutation(
                delegate(
                    MySqlConnection connection,
                    MySqlTransaction transaction,
                    IList<LockedUser> users)
                {
                    EnsureAuthorizedAdministrator(users, actor);
                    FindTargetUser(users, targetUserId);

                    const string sql = @"
UPDATE tbl_users
SET PasswordHash = @PasswordHash
WHERE UserID = @UserID
LIMIT 1;";

                    using (MySqlCommand command = new MySqlCommand(
                               sql,
                               connection,
                               transaction))
                    {
                        command.Parameters.AddWithValue(
                            "@PasswordHash",
                            passwordHash);
                        command.Parameters.AddWithValue("@UserID", targetUserId);

                        if (command.ExecuteNonQuery() != 1)
                            ThrowConcurrentChange();
                    }

                    return true;
                });
        }

        #endregion

        #region Transaction Helpers

        /// <summary>
        /// Serializes management mutations by locking every current user in UserID order.
        /// </summary>
        private T ExecuteMutation<T>(
            Func<MySqlConnection, MySqlTransaction, IList<LockedUser>, T> mutation)
        {
            if (mutation == null)
                throw new ArgumentNullException(nameof(mutation));

            EnsureManagementSchema();

            MySqlTransaction transaction = null;

            try
            {
                _database.OpenConnection();
                MySqlConnection connection = _database.GetConnection();
                transaction = connection.BeginTransaction(IsolationLevel.Serializable);
                IList<LockedUser> users = LockAllUsers(connection, transaction);
                T result = mutation(connection, transaction, users);
                transaction.Commit();
                return result;
            }
            catch (UserManagementException)
            {
                RollbackTransaction(transaction);
                throw;
            }
            catch (MySqlException ex)
            {
                RollbackTransaction(transaction);

                if (ex.Number == 1062)
                {
                    throw new UserManagementException(
                        UserManagementErrorCode.DuplicateEmployeeNumber,
                        DuplicateEmployeeMessage,
                        ex);
                }

                if (ex.Number == 1205 || ex.Number == 1213)
                {
                    throw new UserManagementException(
                        UserManagementErrorCode.ConcurrentChange,
                        ConcurrentChangeMessage,
                        ex);
                }

                throw new UserManagementException(
                    UserManagementErrorCode.Storage,
                    StorageMessage,
                    ex);
            }
            catch (Exception ex)
            {
                RollbackTransaction(transaction);
                throw new UserManagementException(
                    UserManagementErrorCode.Storage,
                    StorageMessage,
                    ex);
            }
            finally
            {
                if (transaction != null)
                    transaction.Dispose();

                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Locks all users in deterministic order so concurrent administrator mutations cannot lose the final administrator.
        /// </summary>
        private static IList<LockedUser> LockAllUsers(
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
SELECT UserID, EmployeeNo, Role, IsActive
FROM tbl_users
ORDER BY UserID
FOR UPDATE;";

            List<LockedUser> users = new List<LockedUser>();

            using (MySqlCommand command = new MySqlCommand(
                       sql,
                       connection,
                       transaction))
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    users.Add(new LockedUser
                    {
                        UserID = ReadRequiredInt(reader, "UserID"),
                        EmployeeNo = ReadRequiredString(reader, "EmployeeNo"),
                        Role = UserRoles.Normalize(ReadOptionalString(reader, "Role")),
                        IsActive = ReadFailClosedBoolean(reader, "IsActive")
                    });
                }
            }

            return users;
        }

        /// <summary>
        /// Rolls back an incomplete transaction without replacing the initiating failure.
        /// </summary>
        private static void RollbackTransaction(MySqlTransaction transaction)
        {
            if (transaction == null)
                return;

            try
            {
                transaction.Rollback();
            }
            catch
            {
                // The original safe failure remains authoritative.
            }
        }

        #endregion

        #region Authorization Helpers

        private static LockedUser EnsureAuthorizedAdministrator(
            IList<LockedUser> users,
            string actorEmployeeNo)
        {
            LockedUser administrator = users.FirstOrDefault(
                user => EmployeeNumbersEqual(user.EmployeeNo, actorEmployeeNo));

            if (administrator == null ||
                !administrator.IsActive ||
                !string.Equals(
                    administrator.Role,
                    UserRoles.Admin,
                    StringComparison.Ordinal))
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Unauthorized,
                    AuthorizationMessage);
            }

            return administrator;
        }

        private static void EnsureAuthorizedAdministrator(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string actorEmployeeNo)
        {
            const string sql = @"
SELECT UserID, EmployeeNo, Role, IsActive
FROM tbl_users
WHERE EmployeeNo = @EmployeeNo
LIMIT 1;";

            LockedUser administrator = null;

            using (MySqlCommand command = new MySqlCommand(
                       sql,
                       connection,
                       transaction))
            {
                command.Parameters.AddWithValue("@EmployeeNo", actorEmployeeNo);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        administrator = new LockedUser
                        {
                            UserID = ReadRequiredInt(reader, "UserID"),
                            EmployeeNo = ReadRequiredString(reader, "EmployeeNo"),
                            Role = UserRoles.Normalize(ReadOptionalString(reader, "Role")),
                            IsActive = ReadFailClosedBoolean(reader, "IsActive")
                        };
                    }
                }
            }

            if (administrator == null ||
                !administrator.IsActive ||
                !string.Equals(
                    administrator.Role,
                    UserRoles.Admin,
                    StringComparison.Ordinal))
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Unauthorized,
                    AuthorizationMessage);
            }
        }

        private static LockedUser FindTargetUser(
            IList<LockedUser> users,
            int userId)
        {
            LockedUser target = users.FirstOrDefault(user => user.UserID == userId);

            if (target == null)
            {
                throw new UserManagementException(
                    UserManagementErrorCode.UserNotFound,
                    UserNotFoundMessage);
            }

            return target;
        }

        private static int CountActiveAdministrators(IList<LockedUser> users)
        {
            return users.Count(
                user => user.IsActive &&
                        string.Equals(
                            user.Role,
                            UserRoles.Admin,
                            StringComparison.Ordinal));
        }

        private static void ThrowConcurrentChange()
        {
            throw new UserManagementException(
                UserManagementErrorCode.ConcurrentChange,
                ConcurrentChangeMessage);
        }

        #endregion

        #region Schema Helpers

        private static bool UserTableExists(MySqlConnection connection)
        {
            const string sql = @"
SELECT COUNT(*)
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'tbl_users';";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        private static bool HasUniqueEmployeeNumberIndex(
            MySqlConnection connection)
        {
            const string sql = @"
SELECT COUNT(*)
FROM
(
    SELECT INDEX_NAME
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'tbl_users'
      AND NON_UNIQUE = 0
    GROUP BY INDEX_NAME
    HAVING COUNT(*) = 1
       AND MAX(CASE WHEN COLUMN_NAME = 'EmployeeNo' THEN 1 ELSE 0 END) = 1
) unique_employee_indexes;";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static int CountUnsafeEmployeeNumberRows(
            MySqlConnection connection)
        {
            const string sql = @"
SELECT
    (SELECT COUNT(*)
     FROM tbl_users
     WHERE EmployeeNo IS NULL OR TRIM(EmployeeNo) = '')
    +
    (SELECT COUNT(*)
     FROM
     (
         SELECT UPPER(TRIM(EmployeeNo)) AS NormalizedEmployeeNo
         FROM tbl_users
         GROUP BY UPPER(TRIM(EmployeeNo))
         HAVING COUNT(*) > 1
     ) duplicate_groups);";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        #endregion

        #region Mapping

        private static User MapAuthenticationUser(DataRow row)
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

        private static User MapManagementUser(MySqlDataReader reader)
        {
            return new User
            {
                UserID = ReadRequiredInt(reader, "UserID"),
                EmployeeNo = ReadRequiredString(reader, "EmployeeNo"),
                FullName = ReadOptionalString(reader, "FullName"),
                PasswordHash = string.Empty,
                Role = UserRoles.Normalize(ReadOptionalString(reader, "Role")) ?? "Invalid",
                Department = ReadOptionalString(reader, "Department"),
                IsActive = ReadFailClosedBoolean(reader, "IsActive"),
                CreatedDate = ReadOptionalDate(reader, "CreatedDate", DateTime.MinValue)
            };
        }

        private static User LoadManagementUser(
            MySqlConnection connection,
            MySqlTransaction transaction,
            int userId)
        {
            const string sql = @"
SELECT UserID, EmployeeNo, FullName, Role, Department, IsActive, CreatedDate
FROM tbl_users
WHERE UserID = @UserID
LIMIT 1;";

            using (MySqlCommand command = new MySqlCommand(
                       sql,
                       connection,
                       transaction))
            {
                command.Parameters.AddWithValue("@UserID", userId);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        ThrowConcurrentChange();

                    return MapManagementUser(reader);
                }
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// Normalizes an Employee Number for newly managed accounts.
        /// </summary>
        public static string NormalizeManagedEmployeeNo(string employeeNo)
        {
            if (string.IsNullOrWhiteSpace(employeeNo))
                return null;

            return employeeNo.Trim().ToUpperInvariant();
        }

        private static string ValidateLookupEmployeeNo(string employeeNo)
        {
            if (employeeNo == null)
                throw new ArgumentNullException(nameof(employeeNo));

            if (string.IsNullOrWhiteSpace(employeeNo))
            {
                throw new ArgumentException(
                    "Employee Number must not be empty.",
                    nameof(employeeNo));
            }

            return employeeNo.Trim();
        }

        private static string ValidateManagedEmployeeNo(string employeeNo)
        {
            string normalized = NormalizeManagedEmployeeNo(employeeNo);

            if (normalized == null)
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Validation,
                    "Employee Number is required.");
            }

            if (normalized.Length > EmployeeNumberMaximumLength)
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Validation,
                    "Employee Number must be 20 characters or fewer.");
            }

            foreach (char character in normalized)
            {
                if (!char.IsLetterOrDigit(character) &&
                    character != '-' &&
                    character != '_')
                {
                    throw new UserManagementException(
                        UserManagementErrorCode.Validation,
                        "Employee Number may contain only letters, numbers, hyphens, and underscores.");
                }
            }

            return normalized;
        }

        private static string ValidateText(
            string value,
            int maximumLength,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Validation,
                    fieldName + " is required.");
            }

            string trimmed = value.Trim();

            if (trimmed.Length > maximumLength)
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Validation,
                    fieldName + " is too long.");
            }

            if (trimmed.Any(char.IsControl))
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Validation,
                    fieldName + " contains unsupported characters.");
            }

            return trimmed;
        }

        private static string ValidateRole(string role)
        {
            string canonicalRole = UserRoles.Normalize(role);

            if (canonicalRole == null)
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Validation,
                    "Select a supported role: Admin or User.");
            }

            return canonicalRole;
        }

        private static void ValidatePasswordHash(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash) ||
                passwordHash.Length > PasswordHashMaximumLength ||
                !(passwordHash.StartsWith("$2a$", StringComparison.Ordinal) ||
                  passwordHash.StartsWith("$2b$", StringComparison.Ordinal) ||
                  passwordHash.StartsWith("$2x$", StringComparison.Ordinal) ||
                  passwordHash.StartsWith("$2y$", StringComparison.Ordinal)))
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Validation,
                    "A valid BCrypt password hash is required.");
            }
        }

        private static void ValidateUserId(int userId)
        {
            if (userId <= 0)
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Validation,
                    UserNotFoundMessage);
            }
        }

        private static bool EmployeeNumbersEqual(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) &&
                   !string.IsNullOrWhiteSpace(second) &&
                   string.Equals(
                       first.Trim(),
                       second.Trim(),
                       StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Conversion Helpers

        private static int ReadRequiredInt(DataRow row, string columnName)
        {
            object value = row[columnName];

            if (value == null || value == DBNull.Value)
                throw new InvalidOperationException(columnName + " is required.");

            return Convert.ToInt32(value);
        }

        private static int ReadRequiredInt(
            MySqlDataReader reader,
            string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                throw new InvalidOperationException(columnName + " is required.");

            return Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static string ReadRequiredString(DataRow row, string columnName)
        {
            object value = row[columnName];

            if (value == null ||
                value == DBNull.Value ||
                string.IsNullOrWhiteSpace(value.ToString()))
            {
                throw new InvalidOperationException(columnName + " is required.");
            }

            return value.ToString();
        }

        private static string ReadRequiredString(
            MySqlDataReader reader,
            string columnName)
        {
            string value = ReadOptionalString(reader, columnName);

            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(columnName + " is required.");

            return value;
        }

        private static string ReadOptionalString(DataRow row, string columnName)
        {
            object value = row[columnName];
            return value == null || value == DBNull.Value
                ? string.Empty
                : value.ToString();
        }

        private static string ReadOptionalString(
            MySqlDataReader reader,
            string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal)
                ? string.Empty
                : reader.GetValue(ordinal).ToString();
        }

        private static bool ReadFailClosedBoolean(DataRow row, string columnName)
        {
            object value = row[columnName];
            return ConvertFailClosedBoolean(value);
        }

        private static bool ReadFailClosedBoolean(
            MySqlDataReader reader,
            string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal)
                ? false
                : ConvertFailClosedBoolean(reader.GetValue(ordinal));
        }

        private static bool ConvertFailClosedBoolean(object value)
        {
            if (value == null || value == DBNull.Value)
                return false;

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
            catch (OverflowException)
            {
                return false;
            }
        }

        private static DateTime ReadOptionalDate(
            DataRow row,
            string columnName,
            DateTime defaultValue)
        {
            object value = row[columnName];
            return value == null || value == DBNull.Value
                ? defaultValue
                : Convert.ToDateTime(value);
        }

        private static DateTime ReadOptionalDate(
            MySqlDataReader reader,
            string columnName,
            DateTime defaultValue)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal)
                ? defaultValue
                : Convert.ToDateTime(reader.GetValue(ordinal));
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Carries only fields needed for authorized transactional guard checks.
        /// </summary>
        private sealed class LockedUser
        {
            public int UserID { get; set; }

            public string EmployeeNo { get; set; }

            public string Role { get; set; }

            public bool IsActive { get; set; }
        }

        #endregion
    }
}
