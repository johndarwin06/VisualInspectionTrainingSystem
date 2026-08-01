#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Integration
{
    /// <summary>
    /// Guards security, ordering, date-boundary, and transaction invariants expressed in repository SQL.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Integration)]
    public sealed class RepositorySourceContractTests
    {
        #region Tests

        /// <summary>Confirms repository commands do not regress to broad SELECT-star projections.</summary>
        [Test]
        public void Repositories_DoNotUseSelectStar()
        {
            // Arrange
            IEnumerable<string> files = Directory.EnumerateFiles(
                Path.Combine(GetRepositoryRoot(), "Repositories"),
                "*.cs",
                SearchOption.TopDirectoryOnly);

            // Act
            List<string> offenders = files
                .Where(file => Regex.IsMatch(
                    File.ReadAllText(file),
                    @"\bSELECT\s+\*",
                    RegexOptions.IgnoreCase))
                .Select(Path.GetFileName)
                .ToList();

            // Assert
            Assert.That(offenders, Is.Empty);
        }

        /// <summary>Confirms dashboard metrics retain supported normalized GOOD/NG semantics.</summary>
        [Test]
        public void DashboardRepository_UsesNormalizedSupportedReviewTruth()
        {
            // Arrange
            string source = ReadRepository("DashboardRepository.cs");

            // Act and Assert
            Assert.That(source, Does.Contain("UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG')"));
            Assert.That(source, Does.Contain("UPPER(TRIM(a.UserAnswer)) IN ('GOOD', 'NG')"));
            Assert.That(source, Does.Contain("StartTime >= @DayStart"));
            Assert.That(source, Does.Contain("StartTime < @DayEnd"));
            Assert.That(
                Regex.IsMatch(
                    source,
                    @"WHERE\s+DATE\s*\(\s*StartTime",
                    RegexOptions.IgnoreCase),
                Is.False);
            Assert.That(source, Does.Contain("IsolationLevel.RepeatableRead"));
        }

        /// <summary>Confirms reports retain consistent snapshots, limits, and deterministic ordering.</summary>
        [Test]
        public void ReportRepository_PreservesSnapshotAndSafeguardContracts()
        {
            // Arrange
            string source = ReadRepository("ReportRepository.cs");

            // Act and Assert
            Assert.That(source, Does.Contain("IsolationLevel.RepeatableRead"));
            Assert.That(source, Does.Contain("ORDER BY s.StartTime DESC, s.SessionID DESC"));
            Assert.That(source, Does.Contain("InteractiveDisplayLimit = 500"));
            Assert.That(source, Does.Contain("MaximumExportSessionCount = 10000"));
            Assert.That(source, Does.Contain("transaction.Commit()"));
            Assert.That(source, Does.Contain("RollbackReadTransaction(transaction, exception)"));
        }

        /// <summary>Confirms trainee history remains current-user scoped and deterministically paged.</summary>
        [Test]
        public void TrainingHistoryRepository_PreservesOwnershipAndPagingContracts()
        {
            // Arrange
            string source = ReadRepository("TrainingHistoryRepository.cs");

            // Act and Assert
            Assert.That(source, Does.Contain("@EmployeeNo"));
            Assert.That(source, Does.Contain("ORDER BY s.StartTime DESC, s.SessionID DESC"));
            Assert.That(source, Does.Contain("LIMIT @Limit OFFSET @Offset"));
            Assert.That(source, Does.Contain("IsolationLevel.RepeatableRead"));
            Assert.That(source, Does.Contain("UPPER(TRIM(a.CorrectAnswer)) IN ('GOOD', 'NG')"));
        }

        /// <summary>Confirms mutable review and user-management operations retain transaction isolation.</summary>
        [Test]
        public void MutationRepositories_PreserveTransactionsAndConcurrencyProtection()
        {
            // Arrange
            string answers = ReadRepository("AnswerRepository.cs");
            string sessions = ReadRepository("SessionRepository.cs");
            string users = ReadRepository("UserRepository.cs");

            // Act and Assert
            Assert.That(answers, Does.Contain("IsolationLevel.RepeatableRead"));
            Assert.That(answers, Does.Contain("private static void RollbackTransaction"));
            Assert.That(sessions, Does.Contain("IsolationLevel.RepeatableRead"));
            Assert.That(sessions, Does.Contain("private static void RollbackTransaction"));
            Assert.That(users, Does.Contain("IsolationLevel.Serializable"));
            Assert.That(users, Does.Contain("RollbackTransaction(transaction)"));
        }

        /// <summary>Confirms repository SQL keeps parameters for externally supplied values.</summary>
        [Test]
        public void Repositories_UseNamedParametersForUserControlledFilters()
        {
            // Arrange
            string dashboard = ReadRepository("DashboardRepository.cs");
            string reports = ReadRepository("ReportRepository.cs");
            string history = ReadRepository("TrainingHistoryRepository.cs");
            string users = ReadRepository("UserRepository.cs");

            // Act and Assert
            Assert.That(dashboard, Does.Contain("@DayStart"));
            Assert.That(dashboard, Does.Contain("@DayEnd"));
            Assert.That(reports, Does.Contain("@StartDate"));
            Assert.That(reports, Does.Contain("@EndDate"));
            Assert.That(history, Does.Contain("@SearchText"));
            Assert.That(users, Does.Contain("@EmployeeNo"));
        }

        #endregion

        #region Helpers

        private static string ReadRepository(string fileName)
        {
            return File.ReadAllText(
                Path.Combine(
                    GetRepositoryRoot(),
                    "Repositories",
                    fileName));
        }

        private static string GetRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);

            while (directory != null)
            {
                if (File.Exists(Path.Combine(
                        directory.FullName,
                        "VisualInspectionTrainingSystem.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "The repository root could not be located from the test output.");
        }

        #endregion
    }
}
