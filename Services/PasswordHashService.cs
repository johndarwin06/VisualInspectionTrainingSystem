#region Namespaces

using System;
using BCryptAlgorithm = BCrypt.Net.BCrypt;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Provides BCrypt password hashing and verification.
    /// </summary>
    public class PasswordHashService
    {
        #region Public Methods

        /// <summary>
        /// Creates a BCrypt hash for the supplied plain-text password.
        /// </summary>
        /// <param name="password">The plain-text password to hash.</param>
        /// <returns>A BCrypt password hash.</returns>
        public string HashPassword(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            return BCryptAlgorithm.HashPassword(password);
        }

        /// <summary>
        /// Verifies a plain-text password against a BCrypt hash.
        /// </summary>
        /// <param name="password">The plain-text password supplied by the user.</param>
        /// <param name="passwordHash">The stored BCrypt hash.</param>
        /// <returns>True when the password matches; otherwise false.</returns>
        public bool VerifyPassword(
            string password,
            string passwordHash)
        {
            if (password == null ||
                string.IsNullOrWhiteSpace(passwordHash) ||
                !IsBCryptHash(passwordHash))
            {
                return false;
            }

            try
            {
                return BCryptAlgorithm.Verify(password, passwordHash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether a stored value appears to be a BCrypt hash.
        /// </summary>
        /// <param name="passwordHash">The stored password value.</param>
        /// <returns>True when the value has a BCrypt prefix; otherwise false.</returns>
        public bool IsBCryptHash(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                return false;
            }

            return passwordHash.StartsWith("$2a$", StringComparison.Ordinal) ||
                   passwordHash.StartsWith("$2b$", StringComparison.Ordinal) ||
                   passwordHash.StartsWith("$2x$", StringComparison.Ordinal) ||
                   passwordHash.StartsWith("$2y$", StringComparison.Ordinal);
        }

        #endregion
    }
}
