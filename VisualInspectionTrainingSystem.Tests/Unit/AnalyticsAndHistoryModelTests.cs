#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Unit
{
    /// <summary>
    /// Covers half-open reporting periods, nullable analytics, and trainee-history display contracts.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Unit)]
    public sealed class AnalyticsAndHistoryModelTests
    {
        #region Report Period Tests

        /// <summary>Confirms daily periods use one local half-open calendar day.</summary>
        [Test]
        public void ReportPeriod_DailyUsesHalfOpenBoundary()
        {
            // Arrange
            DateTime selected = new DateTime(2026, 8, 1, 16, 45, 0);

            // Act
            ReportPeriod period = ReportPeriod.CreateDaily(selected);

            // Assert
            Assert.That(period.StartInclusive, Is.EqualTo(new DateTime(2026, 8, 1)));
            Assert.That(period.EndExclusive, Is.EqualTo(new DateTime(2026, 8, 2)));
            Assert.That(period.EndInclusive, Is.EqualTo(new DateTime(2026, 8, 1)));
            Assert.That(period.ReportTypeText, Is.EqualTo("Daily"));
            Assert.That(period.FileNameToken, Is.EqualTo("20260801-20260801"));
        }

        /// <summary>Confirms weekly reports always use Monday through the following Monday.</summary>
        [TestCase(2026, 8, 2)]
        [TestCase(2026, 8, 3)]
        [TestCase(2026, 8, 7)]
        public void ReportPeriod_WeeklyUsesMondayCalendarBoundary(
            int year,
            int month,
            int day)
        {
            // Arrange
            DateTime selected = new DateTime(year, month, day);

            // Act
            ReportPeriod period = ReportPeriod.CreateWeekly(selected);

            // Assert
            Assert.That(period.StartInclusive.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
            Assert.That(period.EndExclusive, Is.EqualTo(period.StartInclusive.Value.AddDays(7)));
        }

        /// <summary>Confirms rolling, monthly, custom, and all-date factories preserve expected boundaries.</summary>
        [Test]
        public void ReportPeriod_OtherFactoriesUseDeterministicBoundaries()
        {
            // Arrange
            DateTime selected = new DateTime(2026, 8, 15, 10, 0, 0);

            // Act
            ReportPeriod rolling = ReportPeriod.CreateLastSevenDays(selected);
            ReportPeriod monthly = ReportPeriod.CreateMonthly(selected);
            ReportPeriod custom = ReportPeriod.CreateCustomInclusive(
                new DateTime(2026, 8, 2, 15, 0, 0),
                new DateTime(2026, 8, 4, 4, 0, 0));
            ReportPeriod allDates = ReportPeriod.CreateAllDates();

            // Assert
            Assert.That(rolling.StartInclusive, Is.EqualTo(new DateTime(2026, 8, 9)));
            Assert.That(rolling.EndExclusive, Is.EqualTo(new DateTime(2026, 8, 16)));
            Assert.That(monthly.StartInclusive, Is.EqualTo(new DateTime(2026, 8, 1)));
            Assert.That(monthly.EndExclusive, Is.EqualTo(new DateTime(2026, 9, 1)));
            Assert.That(custom.StartInclusive, Is.EqualTo(new DateTime(2026, 8, 2)));
            Assert.That(custom.EndExclusive, Is.EqualTo(new DateTime(2026, 8, 5)));
            Assert.That(allDates.StartInclusive, Is.Null);
            Assert.That(allDates.EndExclusive, Is.Null);
            Assert.That(allDates.DateRangeText, Is.EqualTo("All Dates"));
        }

        /// <summary>Confirms an inverted custom range is rejected before repository access.</summary>
        [Test]
        public void ReportPeriod_InvertedCustomRangeIsRejected()
        {
            // Arrange
            DateTime start = new DateTime(2026, 8, 2);
            DateTime end = new DateTime(2026, 8, 1);

            // Act and Assert
            Assert.That(
                () => ReportPeriod.CreateCustomInclusive(start, end),
                Throws.TypeOf<ArgumentException>());
        }

        #endregion

        #region Dashboard Tests

        /// <summary>Confirms reviewed accuracy is nullable with a zero denominator.</summary>
        [Test]
        public void AnalyticsChartData_EmptyReviewDenominatorReturnsNullAccuracy()
        {
            // Arrange
            AnalyticsChartData data = new AnalyticsChartData
            {
                ReviewedAnswers = 0,
                CorrectReviewedAnswers = 0,
                PendingAnswers = 3
            };

            // Act and Assert
            Assert.That(data.ReviewedAccuracyPercent, Is.Null);
            Assert.That(data.WrongReviewedAnswers, Is.Zero);
            Assert.That(data.IsAvailable, Is.True);
        }

        /// <summary>Confirms dashboard reviewed calculations clamp malformed aggregates safely.</summary>
        [Test]
        public void AnalyticsChartData_ReviewedCalculationsAreBounded()
        {
            // Arrange
            AnalyticsChartData normal = new AnalyticsChartData
            {
                ReviewedAnswers = 4,
                CorrectReviewedAnswers = 2
            };
            AnalyticsChartData malformed = new AnalyticsChartData
            {
                ReviewedAnswers = 1,
                CorrectReviewedAnswers = 2
            };

            // Act and Assert
            Assert.That(normal.ReviewedAccuracyPercent, Is.EqualTo(50m));
            Assert.That(normal.WrongReviewedAnswers, Is.EqualTo(2));
            Assert.That(malformed.WrongReviewedAnswers, Is.Zero);
        }

        /// <summary>Confirms a new dashboard snapshot is safe and non-null.</summary>
        [Test]
        public void DashboardSnapshot_DefaultStateIsEmptyAndUsable()
        {
            // Arrange and Act
            DashboardSnapshot snapshot = new DashboardSnapshot();

            // Assert
            Assert.That(snapshot.Metrics, Is.Not.Null);
            Assert.That(snapshot.RecentSessions, Is.Empty);
            Assert.That(snapshot.ChartData, Is.Not.Null);
            Assert.That(snapshot.ChartData.DailyPoints, Is.Empty);
        }

        /// <summary>Confirms session status derives only from completion state.</summary>
        [Test]
        public void DashboardSession_StatusDistinguishesOpenAndCompleted()
        {
            // Arrange
            DashboardSessionSummary session = new DashboardSessionSummary();

            // Act and Assert
            Assert.That(session.Status, Is.EqualTo("Open"));

            session.EndTime = new DateTime(2026, 8, 1, 12, 0, 0);
            Assert.That(session.Status, Is.EqualTo("Completed"));
        }

        #endregion

        #region Training History Tests

        /// <summary>Confirms pending, partial, and reviewed history states remain distinct.</summary>
        [TestCase(0, 4, "Pending Review")]
        [TestCase(2, 2, "Partially Reviewed")]
        [TestCase(4, 0, "Reviewed")]
        public void TrainingHistorySummary_ReviewStatusUsesSupportedCounts(
            int reviewed,
            int pending,
            string expected)
        {
            // Arrange
            TrainingHistorySessionSummary summary = new TrainingHistorySessionSummary
            {
                AnswerCount = 4,
                ReviewedAnswers = reviewed,
                PendingAnswers = pending
            };

            // Act and Assert
            Assert.That(summary.ReviewStatusText, Is.EqualTo(expected));
        }

        /// <summary>Confirms duration and nullable accuracy labels remain safe.</summary>
        [Test]
        public void TrainingHistorySummary_DisplayValuesHandleValidAndMalformedData()
        {
            // Arrange
            TrainingHistorySessionSummary valid = new TrainingHistorySessionSummary
            {
                StartTime = new DateTime(2026, 8, 1, 10, 0, 0),
                EndTime = new DateTime(2026, 8, 1, 11, 2, 3),
                ReviewedAccuracy = 50m
            };
            TrainingHistorySessionSummary malformed = new TrainingHistorySessionSummary
            {
                StartTime = new DateTime(2026, 8, 1, 11, 0, 0),
                EndTime = new DateTime(2026, 8, 1, 10, 0, 0),
                ReviewedAccuracy = null
            };

            // Act and Assert
            Assert.That(valid.DurationText, Is.EqualTo("1h 02m 03s"));
            Assert.That(valid.ReviewedAccuracyText, Is.EqualTo("50.00%"));
            Assert.That(malformed.DurationText, Is.EqualTo("N/A"));
            Assert.That(malformed.ReviewedAccuracyText, Is.EqualTo("N/A"));
        }

        /// <summary>Confirms pages and details take immutable collection snapshots.</summary>
        [Test]
        public void TrainingHistory_CollectionsDoNotTrackCallerMutation()
        {
            // Arrange
            TrainingHistorySessionSummary summary = new TrainingHistorySessionSummary
            {
                SessionID = 17
            };
            List<TrainingHistorySessionSummary> sessions =
                new List<TrainingHistorySessionSummary> { summary, null };
            List<TrainingHistoryAnswerDetail> answers =
                new List<TrainingHistoryAnswerDetail>
                {
                    new TrainingHistoryAnswerDetail { AnswerID = 1 },
                    null
                };

            // Act
            TrainingHistoryPage page = new TrainingHistoryPage(sessions, true);
            TrainingHistorySessionDetail detail =
                new TrainingHistorySessionDetail(summary, answers);
            sessions.Clear();
            answers.Clear();

            // Assert
            Assert.That(page.Sessions, Has.Count.EqualTo(1));
            Assert.That(page.HasMore, Is.True);
            Assert.That(detail.Answers, Has.Count.EqualTo(1));
            Assert.That(detail.Summary.SessionID, Is.EqualTo(17));
        }

        /// <summary>Confirms answer detail never exposes a missing full image path.</summary>
        [Test]
        public void TrainingHistoryAnswer_DisplayUsesSafeFallbacks()
        {
            // Arrange
            TrainingHistoryAnswerDetail missing = new TrainingHistoryAnswerDetail();
            TrainingHistoryAnswerDetail identified = new TrainingHistoryAnswerDetail
            {
                ShortImageIdentifier = "abc123",
                ElapsedSeconds = 1.25
            };

            // Act and Assert
            Assert.That(missing.ImageDisplayText, Is.EqualTo("Image unavailable"));
            Assert.That(missing.ElapsedTimeText, Is.EqualTo("N/A"));
            Assert.That(identified.ImageDisplayText, Is.EqualTo("Image abc123"));
            Assert.That(identified.ElapsedTimeText, Is.EqualTo("1.25 s"));
        }

        #endregion
    }
}
