#region Namespaces

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

        public AuthenticationService()
        {
            _repository = new UserRepository();

            _passwordHashService = new PasswordHashService();
        }

        #endregion

        #region Login

        public User Login(
            string employeeNo,
            string password)
        {
            User user = _repository.GetByEmployeeNo(employeeNo);

            if (user == null)
                return null;

            if (!user.IsActive)
                return null;

            if (!IsPasswordValid(
                user,
                password))
            {
                return null;
            }

            SessionService.Login(user);

            return user;
        }

        #endregion

        #region Password Verification

        private bool IsPasswordValid(
            User user,
            string password)
        {
            if (user == null ||
                string.IsNullOrEmpty(password))
            {
                return false;
            }

            if (_passwordHashService.IsHashedPassword(user.PasswordHash))
            {
                return _passwordHashService.VerifyPassword(
                    password,
                    user.PasswordHash);
            }

            if (user.PasswordHash != password)
                return false;

            UpgradePlainTextPassword(
                user,
                password);

            return true;
        }

        private void UpgradePlainTextPassword(
            User user,
            string password)
        {
            string passwordHash = _passwordHashService.HashPassword(password);

            _repository.UpdatePasswordHash(
                user.EmployeeNo,
                passwordHash);

            user.PasswordHash = passwordHash;
        }

        #endregion
    }
}
