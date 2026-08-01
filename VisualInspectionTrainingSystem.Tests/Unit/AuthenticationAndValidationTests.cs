#region Namespaces

using NUnit.Framework;
using System;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Unit
{
    /// <summary>
    /// Covers credential primitives, registration validation, roles, and session fail-closed behavior.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Unit)]
    [NonParallelizable]
    public sealed class AuthenticationAndValidationTests
    {
        #region Lifecycle

        /// <summary>Clears shared authentication state before and after every test.</summary>
        [SetUp]
        public void SetUp()
        {
            SessionService.Logout();
        }

        /// <summary>Prevents one test session from leaking into another.</summary>
        [TearDown]
        public void TearDown()
        {
            SessionService.Logout();
        }

        #endregion

        #region Password Tests

        /// <summary>Confirms BCrypt hashes verify only the matching password.</summary>
        [Test]
        public void PasswordHash_RoundTripAcceptsOnlyMatchingCredential()
        {
            // Arrange
            PasswordHashService service = new PasswordHashService();
            const string password = "SafePassword-17";

            // Act
            string hash = service.HashPassword(password);

            // Assert
            Assert.That(service.IsBCryptHash(hash), Is.True);
            Assert.That(service.VerifyPassword(password, hash), Is.True);
            Assert.That(service.VerifyPassword("WrongPassword-17", hash), Is.False);
            Assert.That(hash, Does.Not.Contain(password));
        }

        /// <summary>Confirms null and malformed stored credentials fail closed.</summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("plaintext")]
        [TestCase("$2b$invalid")]
        public void PasswordHash_MalformedStoredValueFailsClosed(string storedValue)
        {
            // Arrange
            PasswordHashService service = new PasswordHashService();

            // Act
            bool verified = service.VerifyPassword("credential", storedValue);

            // Assert
            Assert.That(verified, Is.False);
        }

        /// <summary>Confirms null passwords cannot be hashed accidentally.</summary>
        [Test]
        public void PasswordHash_NullPlaintextIsRejected()
        {
            // Arrange
            PasswordHashService service = new PasswordHashService();

            // Act and Assert
            Assert.That(
                () => service.HashPassword(null),
                Throws.TypeOf<ArgumentNullException>());
        }

        #endregion

        #region Registration Validation Tests

        /// <summary>Confirms employee numbers are trimmed and preserve safe characters.</summary>
        [Test]
        public void EmployeeNumber_ValidValueIsNormalized()
        {
            // Arrange
            const string value = "  I17_TEST-01  ";

            // Act
            string normalized = UserManagementService.ValidateEmployeeNo(value);

            // Assert
            Assert.That(normalized, Is.EqualTo("I17_TEST-01"));
        }

        /// <summary>Confirms unsafe employee-number input is rejected as validation.</summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("employee 1")]
        [TestCase("employee' OR 1=1 --")]
        public void EmployeeNumber_InvalidValueIsRejected(string value)
        {
            // Arrange, Act and Assert
            UserManagementException exception = Assert.Throws<UserManagementException>(
                () => UserManagementService.ValidateEmployeeNo(value));

            Assert.That(
                exception.ErrorCode,
                Is.EqualTo(UserManagementErrorCode.Validation));
        }

        /// <summary>Confirms supported roles normalize without granting unknown roles.</summary>
        [TestCase(" admin ", UserRoles.Admin)]
        [TestCase("USER", UserRoles.User)]
        public void Role_SupportedValueIsCanonical(string value, string expected)
        {
            // Arrange and Act
            string role = UserManagementService.ValidateRole(value);

            // Assert
            Assert.That(role, Is.EqualTo(expected));
        }

        /// <summary>Confirms unsupported roles cannot enter an authenticated authorization path.</summary>
        [TestCase(null)]
        [TestCase("Supervisor")]
        [TestCase("Administrator")]
        public void Role_UnsupportedValueIsRejected(string value)
        {
            // Arrange, Act and Assert
            UserManagementException exception = Assert.Throws<UserManagementException>(
                () => UserManagementService.ValidateRole(value));

            Assert.That(
                exception.ErrorCode,
                Is.EqualTo(UserManagementErrorCode.Validation));
        }

        /// <summary>Confirms password confirmation and BCrypt byte limits are enforced.</summary>
        [TestCase("short", "short")]
        [TestCase("ValidPassword1", "DifferentPassword1")]
        [TestCase("Password\nControl", "Password\nControl")]
        public void PasswordValidation_InvalidPairIsRejected(
            string password,
            string confirmation)
        {
            // Arrange, Act and Assert
            UserManagementException exception = Assert.Throws<UserManagementException>(
                () => UserManagementService.ValidatePasswordPair(
                    password,
                    confirmation));

            Assert.That(
                exception.ErrorCode,
                Is.EqualTo(UserManagementErrorCode.Validation));
        }

        /// <summary>Confirms a valid password is returned unchanged for immediate hashing.</summary>
        [Test]
        public void PasswordValidation_ValidPairIsAccepted()
        {
            // Arrange
            const string password = "Accepted Password 17";

            // Act
            string validated = UserManagementService.ValidatePasswordPair(
                password,
                password);

            // Assert
            Assert.That(validated, Is.EqualTo(password));
        }

        #endregion

        #region Session Authorization Tests

        /// <summary>Confirms inactive and unsupported-role sessions never receive administrator access.</summary>
        [TestCase(false, UserRoles.Admin)]
        [TestCase(true, UserRoles.User)]
        [TestCase(true, "Supervisor")]
        public void SessionAuthorization_NonCanonicalAdministratorFailsClosed(
            bool isActive,
            string role)
        {
            // Arrange
            User user = new User
            {
                EmployeeNo = "I17AUTH",
                IsActive = isActive,
                Role = role
            };

            // Act
            SessionService.Login(user);

            // Assert
            Assert.That(SessionService.IsLoggedIn, Is.True);
            Assert.That(SessionService.IsCurrentUserAdministrator, Is.False);
            Assert.That(UserManagementService.IsCurrentSessionAdministrator, Is.False);
        }

        /// <summary>Confirms only an active canonical administrator is authorized in memory.</summary>
        [Test]
        public void SessionAuthorization_ActiveAdministratorIsRecognized()
        {
            // Arrange
            User user = new User
            {
                EmployeeNo = "I17ADMIN",
                IsActive = true,
                Role = " admin "
            };

            // Act
            SessionService.Login(user);

            // Assert
            Assert.That(SessionService.IsCurrentUserAdministrator, Is.True);
            Assert.That(UserManagementService.IsCurrentSessionAdministrator, Is.True);

            SessionService.Logout();
            Assert.That(SessionService.IsLoggedIn, Is.False);
            Assert.That(SessionService.CurrentUser, Is.Null);
        }

        #endregion
    }
}
