#region Namespaces

using NUnit.Framework;
using System;
using System.Linq;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Database
{
    /// <summary>
    /// Verifies production dashboard, reports, and trainee-history reads against
    /// deterministic rows in the permanently marked test-only schema.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Database)]
    [NonParallelizable]
    public sealed class AnalyticsAndHistoryDatabaseIntegrationTests : DatabaseTestFixtureBase
    {
        #region NUnit Lifecycle

        /// <summary>Clears the process-wide authenticated identity after every test.</summary>
        [TearDown]
        public void ClearAuthenticatedSession()
        {
            SessionService.Logout();
        }

        #endregion

        #region Dashboard Tests

        /// <summary>
        /// Confirms the six-answer reference data produces the agreed daily metrics,
        /// with pending answers excluded from reviewed accuracy.
        /// </summary>
        [Test]
        public void Dashboard_ControlledSixAnswerDataset_ReturnsExpectedMetrics()
        {
            DateTime dayStart = DateTime.Today;
            string employeeNo = SeedTrainee("D");
            int sessionId = Run.InsertSession(
                employeeNo,
                dayStart.AddHours(9),
                dayStart.AddHours(9).AddMinutes(10),
                6,
                Run.RunId + "-D");

            InsertControlledSixAnswers(sessionId, "D");

            DashboardSnapshot snapshot;

            using (MySqlService database = Run.CreateDatabaseService())
            {
                snapshot = new DashboardRepository(database).GetSnapshot(
                    dayStart,
                    dayStart.AddDays(1),
                    dayStart.AddDays(-6),
                    dayStart.AddDays(1),
                    20);
            }

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Metrics.TodaysTraining, Is.EqualTo(1));
                Assert.That(snapshot.Metrics.TimeSpentSeconds, Is.EqualTo(600));
                Assert.That(snapshot.Metrics.GoodCount, Is.EqualTo(3));
                Assert.That(snapshot.Metrics.NgCount, Is.EqualTo(3));
                Assert.That(snapshot.Metrics.ReviewedAnswers, Is.EqualTo(4));
                Assert.That(snapshot.Metrics.PendingAnswers, Is.EqualTo(2));
                Assert.That(snapshot.Metrics.CorrectReviewedAnswers, Is.EqualTo(2));
                Assert.That(snapshot.Metrics.WrongReviewedAnswers, Is.EqualTo(2));
                Assert.That(snapshot.Metrics.AverageReviewedAccuracy, Is.EqualTo(50m));
                Assert.That(snapshot.RecentSessions.Count, Is.EqualTo(1));
            });
        }

        /// <summary>
        /// Confirms local half-open boundaries, incomplete sessions, malformed
        /// durations, and an empty day retain their defined semantics.
        /// </summary>
        [Test]
        public void Dashboard_BoundariesAndMalformedDurations_AreHandledSafely()
        {
            DateTime dayStart = DateTime.Today.AddDays(3);
            DateTime dayEnd = dayStart.AddDays(1);
            string employeeNo = SeedTrainee("B");

            Run.InsertSession(
                employeeNo,
                dayStart,
                dayStart.AddMinutes(5),
                1,
                Run.RunId + "-B1");
            Run.InsertSession(
                employeeNo,
                dayStart.AddHours(2),
                null,
                1,
                Run.RunId + "-B2");
            Run.InsertSession(
                employeeNo,
                dayStart.AddHours(3),
                dayStart.AddHours(2),
                1,
                Run.RunId + "-B3");
            Run.InsertSession(
                employeeNo,
                dayEnd,
                dayEnd.AddMinutes(7),
                1,
                Run.RunId + "-B4");

            DateTime pendingDay = dayEnd.AddDays(2);
            int pendingSession = Run.InsertSession(
                employeeNo,
                pendingDay.AddHours(1),
                pendingDay.AddHours(1).AddMinutes(1),
                3,
                Run.RunId + "-B5");
            Run.InsertAnswer(pendingSession, 1, Run.ImageHash("B5A"),
                " good ", null, null, null, null);
            Run.InsertAnswer(pendingSession, 2, Run.ImageHash("B5B"),
                "NG", string.Empty, null, null, null);
            Run.InsertAnswer(pendingSession, 3, Run.ImageHash("B5C"),
                "UNKNOWN", "   ", null, null, null);

            DashboardMetrics selected;
            DashboardMetrics pendingOnly;
            DashboardMetrics empty;

            using (MySqlService database = Run.CreateDatabaseService())
            {
                DashboardRepository repository = new DashboardRepository(database);
                selected = repository.GetMetrics(dayStart, dayEnd);
                pendingOnly = repository.GetMetrics(
                    pendingDay,
                    pendingDay.AddDays(1));
                empty = repository.GetMetrics(dayEnd.AddDays(3), dayEnd.AddDays(4));
            }

            Assert.Multiple(() =>
            {
                Assert.That(selected.TodaysTraining, Is.EqualTo(2));
                Assert.That(selected.TimeSpentSeconds, Is.EqualTo(300));
                Assert.That(selected.AverageReviewedAccuracy, Is.Null);
                Assert.That(selected.GoodCount, Is.Zero);
                Assert.That(selected.NgCount, Is.Zero);
                Assert.That(pendingOnly.TodaysTraining, Is.EqualTo(1));
                Assert.That(pendingOnly.GoodCount, Is.EqualTo(1));
                Assert.That(pendingOnly.NgCount, Is.EqualTo(1));
                Assert.That(pendingOnly.ReviewedAnswers, Is.Zero);
                Assert.That(pendingOnly.PendingAnswers, Is.EqualTo(3));
                Assert.That(pendingOnly.WrongReviewedAnswers, Is.Zero);
                Assert.That(pendingOnly.AverageReviewedAccuracy, Is.Null);
                Assert.That(empty.TodaysTraining, Is.Zero);
                Assert.That(empty.TimeSpentSeconds, Is.Zero);
                Assert.That(empty.AverageReviewedAccuracy, Is.Null);
            });
        }

        #endregion

        #region Reports Tests

        /// <summary>
        /// Confirms display and export snapshots report identical normalized totals
        /// and deterministic rows for a controlled custom period.
        /// </summary>
        [Test]
        public void Reports_DisplayAndExportSnapshots_AgreeForControlledPeriod()
        {
            DateTime selectedDay = DateTime.Today;
            string employeeNo = SeedTrainee("R");
            int sessionId = Run.InsertSession(
                employeeNo,
                selectedDay.AddHours(8),
                selectedDay.AddHours(8).AddMinutes(10),
                6,
                Run.RunId + "-R");

            InsertControlledSixAnswers(sessionId, "R");

            ReportSnapshot display;
            ReportSnapshot export;
            ReportSnapshot today;
            ReportSnapshot thisWeek;

            using (MySqlService database = Run.CreateDatabaseService())
            {
                ReportRepository repository = new ReportRepository(database);
                ReportPeriod period = ReportPeriod.CreateCustomInclusive(
                    selectedDay,
                    selectedDay);
                display = repository.GetDisplaySnapshot(period);
                export = repository.GetExportSnapshot(period);
                today = repository.GetDisplaySnapshot(
                    ReportPeriod.CreateDaily(selectedDay));
                thisWeek = repository.GetDisplaySnapshot(
                    ReportPeriod.CreateWeekly(selectedDay));
            }

            Assert.Multiple(() =>
            {
                Assert.That(display.Summary.SessionCount, Is.EqualTo(1));
                Assert.That(display.Summary.CompletedSessionCount, Is.EqualTo(1));
                Assert.That(display.Summary.ReviewedAnswers, Is.EqualTo(4));
                Assert.That(display.Summary.PendingAnswers, Is.EqualTo(2));
                Assert.That(display.Summary.CorrectAnswers, Is.EqualTo(2));
                Assert.That(display.Summary.WrongAnswers, Is.EqualTo(2));
                Assert.That(display.Summary.AverageReviewedAccuracy, Is.EqualTo(50m));
                Assert.That(display.Sessions.Count, Is.EqualTo(1));
                Assert.That(display.Sessions[0].SessionID, Is.EqualTo(sessionId));
                Assert.That(export.Summary.SessionCount, Is.EqualTo(display.Summary.SessionCount));
                Assert.That(export.Summary.ReviewedAnswers, Is.EqualTo(display.Summary.ReviewedAnswers));
                Assert.That(export.Summary.PendingAnswers, Is.EqualTo(display.Summary.PendingAnswers));
                Assert.That(export.Sessions.Select(row => row.SessionID),
                    Is.EqualTo(display.Sessions.Select(row => row.SessionID)));
                Assert.That(today.Summary.SessionCount, Is.EqualTo(1));
                Assert.That(today.Summary.AverageReviewedAccuracy, Is.EqualTo(50m));
                Assert.That(thisWeek.Summary.SessionCount, Is.EqualTo(1));
                Assert.That(thisWeek.Summary.AverageReviewedAccuracy, Is.EqualTo(50m));
                Assert.That(display.IsDisplayLimited, Is.False);
                Assert.That(export.IsExportLimitExceeded, Is.False);
            });
        }

        /// <summary>Confirms a genuinely empty report range returns zeros and N/A accuracy.</summary>
        [Test]
        public void Reports_EmptyRange_ReturnsZeroCountsAndUnavailableAccuracy()
        {
            DateTime emptyDay = DateTime.Today.AddYears(5);
            ReportSnapshot snapshot;

            using (MySqlService database = Run.CreateDatabaseService())
            {
                snapshot = new ReportRepository(database).GetDisplaySnapshot(
                    ReportPeriod.CreateDaily(emptyDay));
            }

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Summary.SessionCount, Is.Zero);
                Assert.That(snapshot.Summary.CompletedSessionCount, Is.Zero);
                Assert.That(snapshot.Summary.OpenSessionCount, Is.Zero);
                Assert.That(snapshot.Summary.ReviewedAnswers, Is.Zero);
                Assert.That(snapshot.Summary.PendingAnswers, Is.Zero);
                Assert.That(snapshot.Summary.AverageReviewedAccuracy, Is.Null);
                Assert.That(snapshot.Sessions, Is.Empty);
            });
        }

        #endregion

        #region Training History Tests

        /// <summary>
        /// Confirms the service uses the authenticated trainee identity and cannot
        /// return another trainee's completed session or detail rows.
        /// </summary>
        [Test]
        public void TrainingHistory_UsesCurrentIdentityAndBlocksForeignDetail()
        {
            string ownEmployee = SeedTrainee("H1");
            string foreignEmployee = SeedTrainee("H2");
            DateTime start = DateTime.Today.AddHours(7);
            int ownSession = Run.InsertSession(
                ownEmployee,
                start,
                start.AddMinutes(10),
                2,
                Run.RunId + "-H1");
            int foreignSession = Run.InsertSession(
                foreignEmployee,
                start.AddMinutes(1),
                start.AddMinutes(11),
                1,
                Run.RunId + "-H2");

            Run.InsertAnswer(
                ownSession,
                1,
                Run.ImageHash("H1A"),
                " good ",
                "GOOD",
                true,
                "Administrator",
                ownEmployee);
            Run.InsertAnswer(
                ownSession,
                2,
                Run.ImageHash("H1B"),
                "NG",
                null,
                null,
                null,
                null);
            Run.InsertAnswer(
                foreignSession,
                1,
                Run.ImageHash("H2A"),
                "NG",
                "NG",
                true,
                "Administrator",
                foreignEmployee);

            SessionService.Login(new User
            {
                EmployeeNo = ownEmployee,
                FullName = "Synthetic Test User",
                Role = UserRoles.User,
                IsActive = true
            });

            TrainingHistoryPage page;
            TrainingHistorySessionDetail ownDetail;
            TrainingHistorySessionDetail foreignDetail;

            using (MySqlService database = Run.CreateDatabaseService())
            {
                TrainingHistoryService service = new TrainingHistoryService(
                    new TrainingHistoryRepository(database));
                page = service.GetHistoryPage(new TrainingHistoryQuery
                {
                    ReviewFilter = TrainingHistoryReviewFilter.All,
                    Offset = 0,
                    Limit = 20
                });
                ownDetail = service.GetSessionDetail(ownSession);
                foreignDetail = service.GetSessionDetail(foreignSession);
            }

            Assert.Multiple(() =>
            {
                Assert.That(page.Sessions.Select(item => item.SessionID),
                    Is.EqualTo(new[] { ownSession }));
                Assert.That(page.Sessions[0].ReviewedAnswers, Is.EqualTo(1));
                Assert.That(page.Sessions[0].PendingAnswers, Is.EqualTo(1));
                Assert.That(page.Sessions[0].ReviewedAccuracy, Is.EqualTo(100m));
                Assert.That(ownDetail, Is.Not.Null);
                Assert.That(ownDetail.Answers.Count, Is.EqualTo(2));
                Assert.That(foreignDetail, Is.Null);
            });
        }

        #endregion

        #region Fixture Helpers

        /// <summary>Creates one active run-owned trainee.</summary>
        private string SeedTrainee(string suffix)
        {
            string employeeNo = Run.Employee(suffix);
            Run.InsertUser(
                suffix,
                "I19-Analytics-Only!42",
                UserRoles.User,
                true);
            return employeeNo;
        }

        /// <summary>
        /// Inserts three GOOD and three NG selections: four reviewed, two pending,
        /// two correct, and two wrong after normalized GOOD/NG evaluation.
        /// </summary>
        private void InsertControlledSixAnswers(int sessionId, string suffix)
        {
            Run.InsertAnswer(sessionId, 1, Run.ImageHash(suffix + "1"),
                "GOOD", "GOOD", true, "Administrator", Run.Employee(suffix));
            Run.InsertAnswer(sessionId, 2, Run.ImageHash(suffix + "2"),
                " ng ", "NG", true, "Administrator", Run.Employee(suffix));
            Run.InsertAnswer(sessionId, 3, Run.ImageHash(suffix + "3"),
                "NG", "GOOD", false, "Administrator", Run.Employee(suffix));
            Run.InsertAnswer(sessionId, 4, Run.ImageHash(suffix + "4"),
                "GOOD", "NG", false, "Administrator", Run.Employee(suffix));
            Run.InsertAnswer(sessionId, 5, Run.ImageHash(suffix + "5"),
                "GOOD", null, null, null, null);
            Run.InsertAnswer(sessionId, 6, Run.ImageHash(suffix + "6"),
                "NG", " MAYBE ", null, null, null);
        }

        #endregion
    }
}
