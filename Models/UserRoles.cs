#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Defines and validates canonical application user roles.
    /// </summary>
    public static class UserRoles
    {
        #region Constants

        public const string Admin = "Admin";

        public const string User = "User";

        #endregion

        #region Validation

        /// <summary>
        /// Returns the canonical application role for a supported value.
        /// </summary>
        /// <param name="role">Role value to normalize.</param>
        /// <returns>The canonical role, or null when the value is unsupported.</returns>
        public static string Normalize(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return null;

            string trimmed = role.Trim();

            if (string.Equals(trimmed, Admin, StringComparison.OrdinalIgnoreCase))
                return Admin;

            if (string.Equals(trimmed, User, StringComparison.OrdinalIgnoreCase))
                return User;

            return null;
        }

        /// <summary>
        /// Determines whether a value maps to an established application role.
        /// </summary>
        /// <param name="role">Role value to inspect.</param>
        /// <returns>True for Admin or User; otherwise false.</returns>
        public static bool IsSupported(string role)
        {
            return Normalize(role) != null;
        }

        #endregion
    }
}
