#region Namespaces

using System;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Validates public registration and delegates one inactive canonical trainee insert to the repository.
    /// No public API accepts a requested role or activation state.
    /// </summary>
    public sealed class RegistrationService
    {
        #region Fields

        private readonly UserRepository _repository;
        private readonly PasswordHashService _passwordHashService;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes registration with production dependencies.
        /// </summary>
        public RegistrationService()
            : this(
                  new UserRepository(),
                  new PasswordHashService())
        {
        }

        /// <summary>
        /// Initializes registration with explicit dependencies for focused verification.
        /// </summary>
        internal RegistrationService(
            UserRepository repository,
            PasswordHashService passwordHashService)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
            _passwordHashService = passwordHashService
                ?? throw new ArgumentNullException(nameof(passwordHashService));
        }

        #endregion

        #region Registration

        /// <summary>
        /// Creates one inactive account with the canonical trainee role after validating all public fields.
        /// </summary>
        /// <param name="employeeNo">Requested employee number.</param>
        /// <param name="fullName">Registrant full name.</param>
        /// <param name="department">Registrant department.</param>
        /// <param name="password">Plain password retained only until hashing.</param>
        /// <param name="confirmPassword">Exact password confirmation.</param>
        /// <returns>A safe inactive trainee projection with no password value.</returns>
        public User Register(
            string employeeNo,
            string fullName,
            string department,
            string password,
            string confirmPassword)
        {
            string validatedEmployeeNo =
                UserManagementService.ValidateEmployeeNo(employeeNo);
            string validatedFullName =
                UserManagementService.ValidateFullName(fullName);
            string validatedDepartment =
                UserManagementService.ValidateDepartment(department);
            string validatedPassword =
                UserManagementService.ValidatePasswordPair(
                    password,
                    confirmPassword);
            string passwordHash = _passwordHashService.HashPassword(
                validatedPassword);
            validatedPassword = null;

            User registered = _repository.RegisterInactiveTrainee(
                validatedEmployeeNo,
                validatedFullName,
                validatedDepartment,
                passwordHash);

            passwordHash = null;

            if (registered == null ||
                registered.IsActive ||
                !string.Equals(
                    registered.Role,
                    UserRoles.User,
                    StringComparison.Ordinal))
            {
                throw new UserManagementException(
                    UserManagementErrorCode.Storage,
                    "Registration could not be completed. Please try again or contact support if the problem continues.");
            }

            registered.PasswordHash = string.Empty;

            ApplicationErrorLogger.LogInformation(
                "Registration",
                "An inactive trainee registration was created for administrator review.");

            return registered;
        }

        #endregion
    }
}
