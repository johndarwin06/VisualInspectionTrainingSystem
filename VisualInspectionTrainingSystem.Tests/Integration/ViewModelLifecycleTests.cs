#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Integration
{
    /// <summary>
    /// Covers repeatable refresh, safe failures, bounded cancellation, and late-publish protection.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Integration)]
    [NonParallelizable]
    public sealed class ViewModelLifecycleTests
    {
        #region Dashboard Tests

        /// <summary>Confirms dashboard refresh replaces rows and preserves the five accepted metrics.</summary>
        [Test]
        public async Task Dashboard_RepeatedRefreshReplacesRowsAndMetrics()
        {
            // Arrange
            StubDashboardRepository repository = CreateDashboardRepository();
            DashboardViewModel viewModel = new DashboardViewModel(
                repository,
                false);

            try
            {
                // Act
                await viewModel.RefreshAsync();
                await viewModel.RefreshAsync();

                // Assert
                Assert.That(viewModel.RecentSessions, Has.Count.EqualTo(2));
                Assert.That(viewModel.RecentSessions[0].SessionID, Is.EqualTo(2));
                Assert.That(viewModel.RecentSessions[1].SessionID, Is.EqualTo(1));
                Assert.That(viewModel.TodaysTrainingText, Is.EqualTo("1"));
                Assert.That(viewModel.AverageReviewedAccuracyText, Is.EqualTo("50.00%"));
                Assert.That(viewModel.TimeSpentText, Is.EqualTo("0h 10m 00s"));
                Assert.That(viewModel.GoodCountText, Is.EqualTo("3"));
                Assert.That(viewModel.NgCountText, Is.EqualTo("3"));
                Assert.That(repository.CallCount, Is.EqualTo(2));
            }
            finally
            {
                viewModel.Dispose();
            }
        }

        /// <summary>Confirms dashboard failures clear stale values and expose no database details.</summary>
        [Test]
        public async Task Dashboard_FailureUsesFixedMessageAndClearsState()
        {
            // Arrange
            StubDashboardRepository repository = CreateDashboardRepository();
            repository.Failure = new InvalidOperationException(
                "Server=secret;Password=credential;path=C:\\private");
            DashboardViewModel viewModel = new DashboardViewModel(
                repository,
                false);

            try
            {
                // Act
                await viewModel.RefreshAsync();

                // Assert
                Assert.That(
                    viewModel.StatusMessage,
                    Is.EqualTo(
                        "Dashboard data could not be loaded. Please try again. " +
                        "Contact support if the problem continues."));
                Assert.That(viewModel.StatusMessage, Does.Not.Contain("secret"));
                Assert.That(viewModel.RecentSessions, Is.Empty);
                Assert.That(viewModel.AverageReviewedAccuracyText, Is.EqualTo("N/A"));
            }
            finally
            {
                viewModel.Dispose();
            }
        }

        /// <summary>Confirms closing during a stalled dashboard read blocks late publication.</summary>
        [Test]
        public void Dashboard_DisposeDuringStalledReadPreventsLatePublish()
        {
            // Arrange
            StubDashboardRepository repository = CreateDashboardRepository();
            repository.BlockRead = true;
            DashboardViewModel viewModel = new DashboardViewModel(
                repository,
                false);
            Task refresh = viewModel.RefreshAsync();

            try
            {
                Assert.That(repository.Entered.Wait(5000), Is.True);

                // Act
                viewModel.Dispose();
                repository.Release.Set();

                // Assert
                Assert.That(repository.Completed.Wait(5000), Is.True);
                Assert.That(refresh.Wait(5000), Is.True);
                Assert.That(viewModel.RecentSessions, Is.Empty);
                Assert.That(viewModel.RefreshCommand.CanExecute(null), Is.False);
            }
            finally
            {
                repository.Release.Set();
                viewModel.Dispose();
            }
        }

        #endregion

        #region Report Tests

        /// <summary>Confirms repeated report refresh replaces rows instead of appending duplicates.</summary>
        [Test]
        public async Task Reports_RepeatedRefreshReplacesRows()
        {
            // Arrange
            StubReportRepository repository = new StubReportRepository();
            ReportsViewModel viewModel = new ReportsViewModel(
                repository,
                new StubReportExportService());

            try
            {
                Assert.That(
                    SpinWait.SpinUntil(() => !viewModel.IsLoading, 5000),
                    Is.True);
                Assert.That(viewModel.Sessions, Has.Count.EqualTo(2));

                // Act
                await viewModel.RefreshAsync();

                // Assert
                Assert.That(viewModel.Sessions, Has.Count.EqualTo(2));
                Assert.That(viewModel.Sessions[0].SessionID, Is.EqualTo(2));
                Assert.That(viewModel.Sessions[1].SessionID, Is.EqualTo(1));
                Assert.That(repository.DisplayCallCount, Is.EqualTo(2));
            }
            finally
            {
                viewModel.Dispose();
            }
        }

        /// <summary>Confirms repository details are replaced by the fixed non-sensitive report failure.</summary>
        [Test]
        public void Reports_DatabaseFailureUsesFixedMessage()
        {
            // Arrange
            StubReportRepository repository = new StubReportRepository
            {
                DisplayException = new InvalidOperationException(
                    "Server=secret;Password=credential;database path")
            };
            ReportsViewModel viewModel = new ReportsViewModel(
                repository,
                new StubReportExportService());

            try
            {
                // Act
                bool completed = SpinWait.SpinUntil(
                    () => !viewModel.IsLoading,
                    5000);

                // Assert
                Assert.That(completed, Is.True);
                Assert.That(
                    viewModel.StatusMessage,
                    Is.EqualTo(
                        "Reports could not be loaded. Please try again. " +
                        "Contact support if the problem continues."));
                Assert.That(viewModel.StatusMessage, Does.Not.Contain("secret"));
                Assert.That(viewModel.StatusMessage, Does.Not.Contain("Password"));
                Assert.That(viewModel.Sessions, Is.Empty);
            }
            finally
            {
                viewModel.Dispose();
            }
        }

        /// <summary>Confirms closing during a stalled report read returns and blocks late UI publication.</summary>
        [Test]
        public void Reports_DisposeDuringStalledReadPreventsLatePublish()
        {
            // Arrange
            StubReportRepository repository = new StubReportRepository
            {
                BlockDisplayRead = true
            };
            ReportsViewModel viewModel = new ReportsViewModel(
                repository,
                new StubReportExportService());

            try
            {
                Assert.That(repository.DisplayEntered.Wait(5000), Is.True);

                // Act
                viewModel.Dispose();
                repository.DisplayRelease.Set();

                // Assert
                Assert.That(repository.DisplayCompleted.Wait(5000), Is.True);
                Assert.That(viewModel.Sessions, Is.Empty);
                Assert.That(viewModel.RefreshCommand.CanExecute(null), Is.False);
            }
            finally
            {
                repository.DisplayRelease.Set();
                viewModel.Dispose();
            }
        }

        #endregion

        #region Training History Tests

        /// <summary>Confirms current-user history refresh replaces rows and preserves identity-free queries.</summary>
        [Test]
        public void TrainingHistory_RepeatedRefreshDoesNotDuplicateRows()
        {
            // Arrange
            StubTrainingHistoryService service =
                new StubTrainingHistoryService();
            TrainingHistoryViewModel viewModel =
                new TrainingHistoryViewModel(service, false);

            try
            {
                // Act
                viewModel.RefreshCommand.Execute(null);
                Assert.That(
                    SpinWait.SpinUntil(() => !viewModel.IsLoading, 5000),
                    Is.True);
                viewModel.RefreshCommand.Execute(null);
                Assert.That(
                    SpinWait.SpinUntil(
                        () => !viewModel.IsLoading && service.PageCallCount == 2,
                        5000),
                    Is.True);

                // Assert
                Assert.That(viewModel.Sessions, Has.Count.EqualTo(2));
                Assert.That(viewModel.Sessions[0].SessionID, Is.EqualTo(2));
                Assert.That(viewModel.Sessions[1].SessionID, Is.EqualTo(1));
                Assert.That(service.LastQuery, Is.Not.Null);
                Assert.That(service.LastQuery.Offset, Is.Zero);
                Assert.That(service.LastQuery.Limit, Is.EqualTo(50));
            }
            finally
            {
                viewModel.Dispose();
            }
        }

        /// <summary>Confirms history load failures use one fixed non-sensitive message.</summary>
        [Test]
        public void TrainingHistory_FailureUsesFixedMessage()
        {
            // Arrange
            StubTrainingHistoryService service = new StubTrainingHistoryService
            {
                Failure = new InvalidOperationException(
                    "Access denied for user secret at 127.0.0.1")
            };
            TrainingHistoryViewModel viewModel =
                new TrainingHistoryViewModel(service, false);

            try
            {
                // Act
                viewModel.RefreshCommand.Execute(null);
                bool completed = SpinWait.SpinUntil(
                    () => !viewModel.IsLoading,
                    5000);

                // Assert
                Assert.That(completed, Is.True);
                Assert.That(
                    viewModel.StatusMessage,
                    Is.EqualTo(
                        "Training history could not be loaded. Please try again. " +
                        "Contact support if the problem continues."));
                Assert.That(viewModel.StatusMessage, Does.Not.Contain("Access denied"));
                Assert.That(viewModel.Sessions, Is.Empty);
            }
            finally
            {
                viewModel.Dispose();
            }
        }

        #endregion

        #region Test Doubles

        private sealed class StubDashboardRepository : DashboardRepository
        {
            private int _callCount;

            public bool BlockRead { get; set; }

            public Exception Failure { get; set; }

            public DashboardSnapshot Snapshot { get; set; }

            public ManualResetEventSlim Entered { get; set; }

            public ManualResetEventSlim Release { get; set; }

            public ManualResetEventSlim Completed { get; set; }

            public int CallCount
            {
                get { return _callCount; }
            }

            public override DashboardSnapshot GetSnapshot(
                DateTime dayStart,
                DateTime dayEnd,
                DateTime trendStart,
                DateTime trendEnd,
                int recentSessionLimit)
            {
                Interlocked.Increment(ref _callCount);
                Entered.Set();

                try
                {
                    if (BlockRead && !Release.Wait(5000))
                    {
                        throw new TimeoutException("Bounded test read timed out.");
                    }

                    if (Failure != null)
                    {
                        throw Failure;
                    }

                    return Snapshot;
                }
                finally
                {
                    Completed.Set();
                }
            }
        }

        private sealed class StubReportRepository : IReportRepository
        {
            private int _displayCallCount;

            public StubReportRepository()
            {
                DisplayEntered = new ManualResetEventSlim(false);
                DisplayRelease = new ManualResetEventSlim(false);
                DisplayCompleted = new ManualResetEventSlim(false);
            }

            public bool BlockDisplayRead { get; set; }

            public Exception DisplayException { get; set; }

            public int DisplayCallCount
            {
                get { return _displayCallCount; }
            }

            public ManualResetEventSlim DisplayEntered { get; private set; }

            public ManualResetEventSlim DisplayRelease { get; private set; }

            public ManualResetEventSlim DisplayCompleted { get; private set; }

            public ReportSnapshot GetDisplaySnapshot(ReportPeriod period)
            {
                Interlocked.Increment(ref _displayCallCount);
                DisplayEntered.Set();

                try
                {
                    if (BlockDisplayRead && !DisplayRelease.Wait(5000))
                    {
                        throw new TimeoutException("Bounded test read timed out.");
                    }

                    if (DisplayException != null)
                    {
                        throw DisplayException;
                    }

                    return CreateReportSnapshot(period);
                }
                finally
                {
                    DisplayCompleted.Set();
                }
            }

            public ReportSnapshot GetExportSnapshot(ReportPeriod period)
            {
                return CreateReportSnapshot(period);
            }
        }

        private sealed class StubReportExportService : IReportExportService
        {
            public void ExportCsv(
                ReportSnapshot snapshot,
                string filePath,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            public void ExportExcel(
                ReportSnapshot snapshot,
                string filePath,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            public void ExportPdf(
                ReportSnapshot snapshot,
                string filePath,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private sealed class StubTrainingHistoryService : ITrainingHistoryService
        {
            public Exception Failure { get; set; }

            public int PageCallCount { get; private set; }

            public TrainingHistoryQuery LastQuery { get; private set; }

            public TrainingHistoryPage GetHistoryPage(TrainingHistoryQuery query)
            {
                PageCallCount++;
                LastQuery = query;

                if (Failure != null)
                {
                    throw Failure;
                }

                return new TrainingHistoryPage(
                    new[]
                    {
                        CreateHistorySession(2),
                        CreateHistorySession(1)
                    },
                    false);
            }

            public TrainingHistorySessionDetail GetSessionDetail(int sessionId)
            {
                return null;
            }

            public AnalyticsChartData GetProgressChartData(int dayCount)
            {
                return new AnalyticsChartData
                {
                    RangeStartInclusive = DateTime.Today.AddDays(-(dayCount - 1)),
                    RangeEndExclusive = DateTime.Today.AddDays(1)
                };
            }
        }

        #endregion

        #region Fixtures

        private static StubDashboardRepository CreateDashboardRepository()
        {
            StubDashboardRepository repository =
                (StubDashboardRepository)FormatterServices.GetUninitializedObject(
                    typeof(StubDashboardRepository));
            repository.Entered = new ManualResetEventSlim(false);
            repository.Release = new ManualResetEventSlim(false);
            repository.Completed = new ManualResetEventSlim(false);
            repository.Snapshot = CreateDashboardSnapshot();
            return repository;
        }

        private static DashboardSnapshot CreateDashboardSnapshot()
        {
            DashboardSnapshot snapshot = new DashboardSnapshot
            {
                DayStartInclusive = new DateTime(2026, 8, 1),
                DayEndExclusive = new DateTime(2026, 8, 2),
                GeneratedAtLocal = new DateTime(2026, 8, 1, 12, 0, 0),
                Metrics = new DashboardMetrics
                {
                    TodaysTraining = 1,
                    AverageReviewedAccuracy = 50m,
                    TimeSpentSeconds = 600,
                    GoodCount = 3,
                    NgCount = 3,
                    ReviewedAnswers = 4,
                    CorrectReviewedAnswers = 2,
                    WrongReviewedAnswers = 2,
                    PendingAnswers = 2
                }
            };
            snapshot.RecentSessions.Add(new DashboardSessionSummary
            {
                SessionID = 2,
                StartTime = new DateTime(2026, 8, 1, 10, 0, 0)
            });
            snapshot.RecentSessions.Add(new DashboardSessionSummary
            {
                SessionID = 1,
                StartTime = new DateTime(2026, 8, 1, 9, 0, 0)
            });
            return snapshot;
        }

        private static ReportSnapshot CreateReportSnapshot(ReportPeriod period)
        {
            ReportSnapshot snapshot = new ReportSnapshot
            {
                Period = period,
                Summary = new ReportSummary
                {
                    SessionCount = 2,
                    ReviewedAnswers = 4,
                    CorrectAnswers = 2,
                    WrongAnswers = 2,
                    PendingAnswers = 2,
                    AverageReviewedAccuracy = 50m
                },
                GeneratedAtLocal = new DateTime(2026, 8, 1, 12, 0, 0)
            };
            snapshot.Sessions.Add(new ReportSessionRow
            {
                SessionID = 2,
                StartTime = new DateTime(2026, 8, 1, 10, 0, 0)
            });
            snapshot.Sessions.Add(new ReportSessionRow
            {
                SessionID = 1,
                StartTime = new DateTime(2026, 8, 1, 9, 0, 0)
            });
            return snapshot;
        }

        private static TrainingHistorySessionSummary CreateHistorySession(int sessionId)
        {
            return new TrainingHistorySessionSummary
            {
                SessionID = sessionId,
                StartTime = new DateTime(2026, 8, 1, 8 + sessionId, 0, 0),
                EndTime = new DateTime(2026, 8, 1, 8 + sessionId, 10, 0),
                TotalQuestions = 10,
                AnswerCount = 10
            };
        }

        #endregion
    }
}
