#region Namespaces

using System;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Handles user authentication.
    /// </summary>
    public class AuthenticationService
    {
        #region Fields

        private readonly UserRepository _repository;
        private readonly PasswordHashService _passwordHashService;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the authentication service.
        /// </summary>
        public AuthenticationService()
            : this(
                  new UserRepository(),
                  new PasswordHashService())
        {
        }

        internal AuthenticationService(
            UserRepository repository,
            PasswordHashService passwordHashService)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));

            _passwordHashService = passwordHashService
                ?? throw new ArgumentNullException(nameof(passwordHashService));
        }

        #endregion

        #region Login

        /// <summary>
        /// Authenticates a user by employee number and password.
        /// </summary>
        /// <param name="employeeNo">The employee number.</param>
        /// <param name="password">The plain-text password supplied by the user.</param>
        /// <returns>The authenticated user, or null when authentication fails.</returns>
        public User Login(
            string employeeNo,
            string password)
        {
            if (string.IsNullOrWhiteSpace(employeeNo) ||
                password == null)
            {
                return null;
            }

            User user = _repository.GetByEmployeeNo(employeeNo);

            if (user == null ||
                !user.IsActive)
            {
                return null;
            }

            string storedPassword = user.PasswordHash ?? string.Empty;

            if (_passwordHashService.IsBCryptHash(storedPassword))
            {
                if (!_passwordHashService.VerifyPassword(
                    password,
                    storedPassword))
                {
                    return null;
                }

                SessionService.Login(user);

                return user;
            }

            if (!string.Equals(
                storedPassword,
                password,
                StringComparison.Ordinal))
            {
                return null;
            }

            string upgradedHash = _passwordHashService.HashPassword(password);

            _repository.UpdatePasswordHash(
                user.EmployeeNo,
                upgradedHash);

            user.PasswordHash = upgradedHash;

            SessionService.Login(user);

            return user;
        }

        #endregion
    }
}
