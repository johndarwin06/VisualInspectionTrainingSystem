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

        #endregion

        #region Constructor

        public AuthenticationService()
        {
            _repository = new UserRepository();
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

            // Sprint 1
            // Plain text comparison.
            // Sprint 4 -> BCrypt

            if (user.PasswordHash != password)
                return null;

            SessionService.Login(user);

            return user;
        }

        #endregion
    }
}