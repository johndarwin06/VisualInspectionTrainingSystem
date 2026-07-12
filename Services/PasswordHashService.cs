#region Namespaces

using System;
using System.Security.Cryptography;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Creates and verifies password hashes.
    /// Existing plain-text passwords remain supported by AuthenticationService.
    /// </summary>
    public class PasswordHashService
    {
        #region Constants

        private const string Prefix = "PBKDF2";

        private const string Algorithm = "SHA256";

        private const int Iterations = 100000;

        private const int SaltSize = 16;

        private const int HashSize = 32;

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a versioned PBKDF2 password hash.
        /// </summary>
        public string HashPassword(string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            byte[] salt = new byte[SaltSize];

            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(salt);
            }

            byte[] hash = DeriveHash(
                password,
                salt,
                Iterations);

            return string.Join(
                "$",
                Prefix,
                Algorithm,
                Iterations.ToString(),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        /// <summary>
        /// Returns true when the stored value uses this service's hash format.
        /// </summary>
        public bool IsHashedPassword(string storedPassword)
        {
            if (string.IsNullOrWhiteSpace(storedPassword))
                return false;

            return storedPassword.StartsWith(
                Prefix + "$",
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies a password against a versioned PBKDF2 hash.
        /// </summary>
        public bool VerifyPassword(
            string password,
            string storedPassword)
        {
            if (password == null ||
                string.IsNullOrWhiteSpace(storedPassword))
            {
                return false;
            }

            string[] parts = storedPassword.Split('$');

            if (parts.Length != 5 ||
                parts[0] != Prefix ||
                parts[1] != Algorithm)
            {
                return false;
            }

            int iterations;

            if (!int.TryParse(
                parts[2],
                out iterations) ||
                iterations <= 0)
            {
                return false;
            }

            byte[] salt;

            byte[] expectedHash;

            try
            {
                salt = Convert.FromBase64String(parts[3]);

                expectedHash = Convert.FromBase64String(parts[4]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] actualHash = DeriveHash(
                password,
                salt,
                iterations);

            return FixedTimeEquals(
                actualHash,
                expectedHash);
        }

        #endregion

        #region Helpers

        private static byte[] DeriveHash(
            string password,
            byte[] salt,
            int iterations)
        {
            using (Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256))
            {
                return deriveBytes.GetBytes(HashSize);
            }
        }

        private static bool FixedTimeEquals(
            byte[] left,
            byte[] right)
        {
            if (left == null ||
                right == null ||
                left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;

            for (int index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }

        #endregion
    }
}
