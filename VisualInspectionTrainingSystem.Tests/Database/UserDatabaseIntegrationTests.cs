#region Namespaces

using NUnit.Framework;
using System;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Database
{
    /// <summary>
    /// Exercises real user, registration, authentication, and authorization repositories
    /// against run-owned rows in the dedicated schema.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Database)]
    [NonParallelizable]
    public sealed class UserDatabaseIntegrationTests : DatabaseTestFixtureBase
    {
        #region Constants

        private const string SyntheticPassword = "I19-Test-Only!42";
        private const string ReplacementPassword = "I19-Replaced!84";

        #endregion

        #region Registration And Authentication

        /// <summary>
        /// Confirms registration commits one inactive trainee, duplicate registration is safe,
        /// and authentication succeeds only after administrator activation.
        /// </summary>
        [Test]
        public void Registration_ActivationAndBcryptAuthentication_AreTransactional()
        {
            UserRepository repository = CreateRepository();
            RegistrationService registration = new RegistrationService(
                repository,
                new PasswordHashService());
            AuthenticationService authentication = new AuthenticationService(
                repository,
                new PasswordHashService());
            string employeeNo = Run.Employee("R");

            User registered = registration.Register(
                employeeNo,
                "Synthetic Registered Trainee",
                "TEST-ONLY",
                SyntheticPassword,
                SyntheticPassword);

            Assert.Multiple(delegate
            {
                Assert.That(registered.EmployeeNo, Is.EqualTo(employeeNo));
                Assert.That(registered.Role, Is.EqualTo(UserRoles.User));
                Assert.That(registered.IsActive, Is.False);
                Assert.That(registered.PasswordHash, Is.Empty);
                Assert.That(
                    authentication.Login(employeeNo, SyntheticPassword),
                    Is.Null);
            });

            UserManagementException duplicate = Assert.Throws<UserManagementException>(
                delegate
                {
                    registration.Register(
                        employeeNo,
                        "Duplicate Synthetic User",
                        "TEST-ONLY",
                        SyntheticPassword,
                        SyntheticPassword);
                });

            Assert.Multiple(delegate
            {
                Assert.That(
                    duplicate.ErrorCode,
                    Is.EqualTo(UserManagementErrorCode.DuplicateEmployeeNumber));
                Assert.That(duplicate.Message, Does.Not.Contain(employeeNo));
                Assert.That(duplicate.Message, Does.Not.Contain("MySql"));
                Assert.That(duplicate.Message, Does.Not.Contain("INSERT"));
            });

            User administrator = SeedAdministrator(repository);

            try
            {
                SessionService.Login(administrator);
                UserManagementService management = new UserManagementService(
                    repository,
                    new PasswordHashService());
                management.SetUserActive(
                    registered.UserID,
                    false,
                    true);
            }
            finally
            {
                SessionService.Logout();
            }

            User authenticated = authentication.Login(
                employeeNo,
                SyntheticPassword);

            Assert.Multiple(delegate
            {
                Assert.That(authenticated, Is.Not.Null);
                Assert.That(authenticated.EmployeeNo, Is.EqualTo(employeeNo));
                Assert.That(authenticated.Role, Is.EqualTo(UserRoles.User));
                Assert.That(authenticated.IsActive, Is.True);
                Assert.That(
                    authentication.Login(employeeNo, "I19-Wrong-Only!"),
                    Is.Null);
            });
        }

        /// <summary>Confirms null activity state and unsupported role data fail authentication closed.</summary>
        [Test]
        public void Authentication_NullActivityAndUnsupportedRole_FailClosed()
        {
            PasswordHashService passwordHash = new PasswordHashService();
            UserRepository repository = CreateRepository();
            AuthenticationService authentication = new AuthenticationService(
                repository,
                passwordHash);
            string hash = passwordHash.HashPassword(SyntheticPassword);
            string nullActivityEmployee = Run.Employee("N");

            Run.InsertUser("N", hash, UserRoles.User, null);

            Assert.That(
                authentication.Login(nullActivityEmployee, SyntheticPassword),
                Is.Null);

            string invalidRoleEmployee = Run.Employee("I");

            using (MySql.Data.MySqlClient.MySqlConnection connection = Run.OpenConnection())
            using (MySql.Data.MySqlClient.MySqlCommand command =
                new MySql.Data.MySqlClient.MySqlCommand(@"
INSERT INTO tbl_users
(
    EmployeeNo,
    FullName,
    PasswordHash,
    Role,
    Department,
    IsActive,
    CreatedDate
)
VALUES
(
    @EmployeeNo,
    'Synthetic Invalid Role',
    @PasswordHash,
    NULL,
    'TEST-ONLY',
    b'1',
    UTC_TIMESTAMP()
);", connection))
            {
                command.Parameters.AddWithValue("@EmployeeNo", invalidRoleEmployee);
                command.Parameters.AddWithValue("@PasswordHash", hash);
                command.ExecuteNonQuery();
            }

            Assert.That(
                authentication.Login(invalidRoleEmployee, SyntheticPassword),
                Is.Null);
        }

        #endregion

        #region Administrator Management

        /// <summary>
        /// Confirms creation, duplicate handling, activation, deactivation, role changes,
        /// password reset, and safe projections through real transactions.
        /// </summary>
        [Test]
        public void AdministratorManagement_LifecycleAndDuplicateRules_AreEnforced()
        {
            UserRepository repository = CreateRepository();
            User administrator = SeedAdministrator(repository);
            UserManagementService management = new UserManagementService(
                repository,
                new PasswordHashService());
            AuthenticationService authentication = new AuthenticationService(
                repository,
                new PasswordHashService());

            try
            {
                SessionService.Login(administrator);

                User created = management.CreateUser(
                    Run.Employee("U"),
                    "Synthetic Managed User",
                    "TEST-ONLY",
                    UserRoles.User,
                    SyntheticPassword,
                    SyntheticPassword);

                Assert.Multiple(delegate
                {
                    Assert.That(created.IsActive, Is.True);
                    Assert.That(created.Role, Is.EqualTo(UserRoles.User));
                    Assert.That(created.PasswordHash, Is.Empty);
                });

                UserManagementException duplicate =
                    Assert.Throws<UserManagementException>(delegate
                    {
                        management.CreateUser(
                            Run.Employee("U"),
                            "Duplicate Managed User",
                            "TEST-ONLY",
                            UserRoles.User,
                            SyntheticPassword,
                            SyntheticPassword);
                    });

                Assert.That(
                    duplicate.ErrorCode,
                    Is.EqualTo(UserManagementErrorCode.DuplicateEmployeeNumber));

                management.SetUserActive(created.UserID, true, false);
                Assert.That(
                    authentication.Login(created.EmployeeNo, SyntheticPassword),
                    Is.Null);

                management.SetUserActive(created.UserID, false, true);
                management.SetUserRole(
                    created.UserID,
                    UserRoles.User,
                    UserRoles.Admin);

                Assert.That(
                    repository.GetByEmployeeNo(created.EmployeeNo).Role,
                    Is.EqualTo(UserRoles.Admin));

                management.SetUserRole(
                    created.UserID,
                    UserRoles.Admin,
                    UserRoles.User);
                management.ResetPassword(
                    created.UserID,
                    ReplacementPassword,
                    ReplacementPassword);

                Assert.Multiple(delegate
                {
                    Assert.That(
                        authentication.Login(created.EmployeeNo, SyntheticPassword),
                        Is.Null);
                    Assert.That(
                        authentication.Login(created.EmployeeNo, ReplacementPassword),
                        Is.Not.Null);
                });
            }
            finally
            {
                SessionService.Logout();
            }
        }

        /// <summary>Confirms a trainee session cannot execute administrator database operations.</summary>
        [Test]
        public void TraineeAuthorization_CannotAccessAdministratorMutations()
        {
            PasswordHashService passwordHash = new PasswordHashService();
            string hash = passwordHash.HashPassword(SyntheticPassword);
            Run.InsertUser("T", hash, UserRoles.User, true);
            UserRepository repository = CreateRepository();
            User trainee = repository.GetByEmployeeNo(Run.Employee("T"));
            UserManagementService management = new UserManagementService(
                repository,
                passwordHash);

            try
            {
                SessionService.Login(trainee);

                UserManagementException failure =
                    Assert.Throws<UserManagementException>(delegate
                    {
                        management.CreateUser(
                            Run.Employee("X"),
                            "Unauthorized Synthetic User",
                            "TEST-ONLY",
                            UserRoles.User,
                            SyntheticPassword,
                            SyntheticPassword);
                    });

                Assert.Multiple(delegate
                {
                    Assert.That(
                        failure.ErrorCode,
                        Is.EqualTo(UserManagementErrorCode.Unauthorized));
                    Assert.That(failure.Message, Does.Not.Contain("SELECT"));
                    Assert.That(failure.Message, Does.Not.Contain(trainee.EmployeeNo));
                });
            }
            finally
            {
                SessionService.Logout();
            }
        }

        #endregion

        #region Helpers

        private UserRepository CreateRepository()
        {
            return new UserRepository(Run.CreateDatabaseService());
        }

        private User SeedAdministrator(UserRepository repository)
        {
            PasswordHashService passwordHash = new PasswordHashService();
            string hash = passwordHash.HashPassword(SyntheticPassword);
            Run.InsertUser("A", hash, UserRoles.Admin, true);
            return repository.GetByEmployeeNo(Run.Employee("A"));
        }

        #endregion
    }
}
