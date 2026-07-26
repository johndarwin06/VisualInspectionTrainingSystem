#region Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Validates administrator user-management requests and hashes replacement passwords.
    /// The repository repeats authorization against locked database state.
    /// </summary>
    public class UserManagementService
    {
        #region Constants

        public const int PasswordMinimumLength = 8;
        public const int PasswordMaximumLength = 72;

        private const string AuthorizationMessage =
            "Administrator authorization is required for User Management.";

        #endregion

        #region Fields

        private readonly UserRepository _repository;
        private readonly PasswordHashService _passwordHashService;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the service with production dependencies.
        /// </summary>
        public UserManagementService()
            : this(
                  new UserRepository(),
                  new PasswordHashService())
        {
        }

        /// <summary>
        /// Initializes the service with explicit dependencies for focused verification.
        /// </summary>
        internal UserManagementService(
            UserRepository repository,
            PasswordHashService passwordHashService)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
            _passwordHashService = passwordHashService
                ?? throw new ArgumentNullException(nameof(passwordHashService));
        }

        #endregion

        #region Queries

        /// <summary>
        /// Returns the safe list of users for the current authorized administrator.
        /// </summary>
        public IList<User> GetUsers()
        {
            return _repository.GetAllForManagement(
                GetCurrentAdministratorEmployeeNo());
        }

        #endregion

        #region Mutations

        /// <summary>
        /// Validates and creates one user with a BCrypt password hash.
        /// </summary>
        public User CreateUser(
            string employeeNo,
            string fullName,
            string department,
            string role,
            string password,
            string confirmPassword)
        {
            string validatedEmployeeNo = ValidateEmployeeNo(employeeNo);
            string validatedFullName = ValidateFullName(fullName);
            string validatedDepartment = ValidateDepartment(department);
            string canonicalRole = ValidateRole(role);
            string validatedPassword = ValidatePasswordPair(
                password,
                confirmPassword);
            string passwordHash = _passwordHashService.HashPassword(
                validatedPassword);

            return _repository.CreateUser(
                GetCurrentAdministratorEmployeeNo(),
                validatedEmployeeNo,
                validatedFullName,
                validatedDepartment,
                canonicalRole,
                passwordHash);
        }

        /// <summary>
        /// Disables or reactivates the selected user.
        /// </summary>
        public void SetUserActive(
            int userId,
            bool expectedIsActive,
            bool newIsActive)
        {
            _repository.SetUserActive(
                GetCurrentAdministratorEmployeeNo(),
                userId,
                expectedIsActive,
                newIsActive);
        }

        /// <summary>
        /// Changes the selected user's role for their next authenticated session.
        /// </summary>
        public void SetUserRole(
            int userId,
            string expectedRole,
            string newRole)
        {
            _repository.SetUserRole(
                GetCurrentAdministratorEmployeeNo(),
                userId,
                ValidateRole(expectedRole),
                ValidateRole(newRole));
        }

        /// <summary>
        /// Validates, hashes, and stores a replacement password for the selected user.
        /// </summary>
        public void ResetPassword(
            int userId,
            string password,
            string confirmPassword)
        {
            string validatedPassword = ValidatePasswordPair(
                password,
                confirmPassword);
            string passwordHash = _passwordHashService.HashPassword(
                validatedPassword);

            _repository.ResetPassword(
                GetCurrentAdministratorEmployeeNo(),
                userId,
                passwordHash);
        }

        #endregion

        #region Authorization

        /// <summary>
        /// Gets whether the in-memory session is an active canonical administrator.
        /// Database authorization is still required by every repository operation.
        /// </summary>
        public static bool IsCurrentSessionAdministrator
        {
            get { return SessionService.IsCurrentUserAdministrator; }
        }

        private static string GetCurrentAdministratorEmployeeNo()
        {
            if (!IsCurrentSessionAdministrator ||
                SessionService.CurrentUser == null ||
                string.IsNullOrWhiteSpace(SessionService.CurrentUser.EmployeeNo))
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Unauthorized,
                    AuthorizationMessage);
            }

            return SessionService.CurrentUser.EmployeeNo.Trim();
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates and normalizes an Employee Number for account creation.
        /// </summary>
        public static string ValidateEmployeeNo(string employeeNo)
        {
            string normalized = UserRepository.NormalizeManagedEmployeeNo(employeeNo);

            if (normalized == null)
                ThrowValidation("Employee Number is required.");

            if (normalized.Length > UserRepository.EmployeeNumberMaximumLength)
                ThrowValidation("Employee Number must be 20 characters or fewer.");

            foreach (char character in normalized)
            {
                if (!char.IsLetterOrDigit(character) &&
                    character != '-' &&
                    character != '_')
                {
                    ThrowValidation(
                        "Employee Number may contain only letters, numbers, hyphens, and underscores.");
                }
            }

            return normalized;
        }

        /// <summary>
        /// Returns a canonical Admin or User role.
        /// </summary>
        public static string ValidateRole(string role)
        {
            string canonicalRole = UserRoles.Normalize(role);

            if (canonicalRole == null)
                ThrowValidation("Select a supported role: Admin or User.");

            return canonicalRole;
        }

        /// <summary>
        /// Validates and trims a full name using the live user-schema limit.
        /// </summary>
        /// <param name="fullName">Full name supplied by an administrator or registrant.</param>
        /// <returns>The trimmed validated full name.</returns>
        internal static string ValidateFullName(string fullName)
        {
            return ValidateRequiredText(
                fullName,
                UserRepository.FullNameMaximumLength,
                "Full Name");
        }

        /// <summary>
        /// Validates and trims a department using the live user-schema limit.
        /// </summary>
        /// <param name="department">Department supplied by an administrator or registrant.</param>
        /// <returns>The trimmed validated department.</returns>
        internal static string ValidateDepartment(string department)
        {
            return ValidateRequiredText(
                department,
                UserRepository.DepartmentMaximumLength,
                "Department");
        }

        /// <summary>
        /// Validates a password and exact confirmation without logging either value.
        /// </summary>
        public static string ValidatePasswordPair(
            string password,
            string confirmPassword)
        {
            if (password == null || string.IsNullOrWhiteSpace(password))
                ThrowValidation("Password is required.");

            if (password.Length < PasswordMinimumLength)
            {
                ThrowValidation(
                    "Password must contain at least 8 characters.");
            }

            if (password.Length > PasswordMaximumLength ||
                Encoding.UTF8.GetByteCount(password) > PasswordMaximumLength)
            {
                ThrowValidation(
                    "Password must not exceed 72 UTF-8 bytes.");
            }

            if (password.Any(char.IsControl))
                ThrowValidation("Password contains unsupported control characters.");

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
                ThrowValidation("Password and Confirm Password must match.");

            return password;
        }

        private static string ValidateRequiredText(
            string value,
            int maximumLength,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                ThrowValidation(fieldName + " is required.");

            string trimmed = value.Trim();

            if (trimmed.Length > maximumLength)
                ThrowValidation(fieldName + " is too long.");

            if (trimmed.Any(char.IsControl))
                ThrowValidation(fieldName + " contains unsupported characters.");

            return trimmed;
        }

        private static void ThrowValidation(string message)
        {
            throw new UserManagementException(
                UserManagementErrorCode.Validation,
                message);
        }

        #endregion
    }
}
