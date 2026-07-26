#region Namespaces

using System;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Stores the currently authenticated user and exposes fail-closed session role checks.
    /// </summary>
    public static class SessionService
    {
        #region Properties

        /// <summary>
        /// Gets the currently authenticated user.
        /// </summary>
        public static User CurrentUser { get; private set; }

        /// <summary>
        /// Gets whether any user is currently authenticated.
        /// </summary>
        public static bool IsLoggedIn
        {
            get { return CurrentUser != null; }
        }

        /// <summary>
        /// Gets whether the current active session has the canonical administrator role.
        /// Database operations perform a second authoritative authorization check.
        /// </summary>
        public static bool IsCurrentUserAdministrator
        {
            get
            {
                return CurrentUser != null &&
                       CurrentUser.IsActive &&
                       string.Equals(
                           UserRoles.Normalize(CurrentUser.Role),
                           UserRoles.Admin,
                           StringComparison.Ordinal);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Stores one authenticated user for the current application session.
        /// </summary>
        public static void Login(User user)
        {
            CurrentUser = user;
        }

        /// <summary>
        /// Clears the current authenticated session.
        /// </summary>
        public static void Logout()
        {
            CurrentUser = null;
        }

        #endregion
    }
}
