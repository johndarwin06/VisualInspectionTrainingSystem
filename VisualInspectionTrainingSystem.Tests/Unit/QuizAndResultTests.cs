#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Unit
{
    /// <summary>
    /// Covers deterministic quiz flow and reviewed-only Result Module semantics.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Unit)]
    public sealed class QuizAndResultTests
    {
        #region Quiz Flow Tests

        /// <summary>Confirms empty quizzes finish safely without a current image.</summary>
        [Test]
        public void QuizEngine_EmptyImageSetCompletesSafely()
        {
            // Arrange and Act
            QuizEngine engine = new QuizEngine(
                CreateUser(),
                new List<QuizImage>());

            // Assert
            Assert.That(engine.TotalQuestions, Is.Zero);
            Assert.That(engine.CurrentQuestion, Is.Zero);
            Assert.That(engine.Progress, Is.EqualTo("0 / 0"));
            Assert.That(engine.CurrentImage, Is.Null);
            Assert.That(engine.CanSubmitAnswer, Is.False);
            Assert.That(engine.IsCompleted(), Is.True);
            Assert.That(engine.Session.Finished, Is.Not.Null);
        }

        /// <summary>Confirms null catalog entries do not become quiz questions.</summary>
        [Test]
        public void QuizEngine_NullImagesAreExcluded()
        {
            // Arrange
            List<QuizImage> images = new List<QuizImage>
            {
                CreateImage(1),
                null,
                CreateImage(2)
            };

            // Act
            QuizEngine engine = new QuizEngine(CreateUser(), images);

            // Assert
            Assert.That(engine.TotalQuestions, Is.EqualTo(2));
            Assert.That(engine.CurrentImage.ImageID, Is.EqualTo(1));
        }

        /// <summary>Confirms progress and completion totals for ten and twenty questions.</summary>
        [TestCase(10)]
        [TestCase(20)]
        public void QuizEngine_SelectedSizeCompletesWithExactTotal(int questionCount)
        {
            // Arrange
            QuizEngine engine = new QuizEngine(
                CreateUser(),
                CreateImages(questionCount));

            // Act
            for (int index = 0; index < questionCount; index++)
            {
                bool accepted = engine.TrySubmitAnswer(
                    index % 2 == 0
                        ? QuizAnswerType.Good
                        : QuizAnswerType.Ng);

                Assert.That(accepted, Is.True);
            }

            // Assert
            Assert.That(engine.Session.Answers, Has.Count.EqualTo(questionCount));
            Assert.That(engine.TotalQuestions, Is.EqualTo(questionCount));
            Assert.That(engine.IsCompleted(), Is.True);
            Assert.That(engine.CanSubmitAnswer, Is.False);
            Assert.That(engine.Session.Finished, Is.Not.Null);
        }

        /// <summary>Confirms a completed question cannot be submitted twice.</summary>
        [Test]
        public void QuizEngine_SubmissionAfterCompletionIsIgnored()
        {
            // Arrange
            QuizEngine engine = new QuizEngine(
                CreateUser(),
                CreateImages(1));

            // Act
            bool first = engine.TrySubmitAnswer(QuizAnswerType.Good);
            bool second = engine.TrySubmitAnswer(QuizAnswerType.Ng);

            // Assert
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(engine.Session.Answers, Has.Count.EqualTo(1));
        }

        /// <summary>Confirms undefined answers never enter the session.</summary>
        [Test]
        public void QuizEngine_UndefinedAnswerIsRejected()
        {
            // Arrange
            QuizEngine engine = new QuizEngine(
                CreateUser(),
                CreateImages(1));

            // Act and Assert
            Assert.That(
                () => engine.TrySubmitAnswer((QuizAnswerType)99),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(engine.Session.Answers, Is.Empty);
            Assert.That(engine.CanSubmitAnswer, Is.True);
        }

        #endregion

        #region Result Semantics Tests

        /// <summary>Confirms the agreed six-answer fixture and reviewed-only calculations.</summary>
        [Test]
        public void Statistics_ControlledSixAnswerDatasetMatchesAcceptedSemantics()
        {
            // Arrange
            List<QuizAnswer> answers = CreateControlledSixAnswerDataset();

            // Act
            ResultStatistics result = new StatisticsService().Calculate(answers);

            // Assert
            Assert.That(result.TotalQuestions, Is.EqualTo(6));
            Assert.That(result.UserGoodAnswers, Is.EqualTo(3));
            Assert.That(result.UserNgAnswers, Is.EqualTo(3));
            Assert.That(result.ReviewedAnswers, Is.EqualTo(4));
            Assert.That(result.PendingReviewAnswers, Is.EqualTo(2));
            Assert.That(result.CorrectReviewedAnswers, Is.EqualTo(2));
            Assert.That(result.WrongReviewedAnswers, Is.EqualTo(2));
            Assert.That(result.ReviewedAccuracyPercentage, Is.EqualTo(50));
            Assert.That(result.ReviewedActualNgAnswers, Is.EqualTo(2));
            Assert.That(result.ReviewedActualGoodAnswers, Is.EqualTo(2));
            Assert.That(result.CorrectlyDetectedNgAnswers, Is.EqualTo(1));
            Assert.That(result.FalseNgAnswers, Is.EqualTo(1));
            Assert.That(result.MissedNgAnswers, Is.EqualTo(1));
        }

        /// <summary>Confirms pending answers never count as wrong.</summary>
        [Test]
        public void Statistics_PendingAnswersAreExcludedFromWrongAndAccuracy()
        {
            // Arrange
            List<QuizAnswer> answers = new List<QuizAnswer>
            {
                CreateAnswer(QuizAnswerType.Good, null, 1),
                CreateAnswer(QuizAnswerType.Ng, null, 2)
            };

            // Act
            ResultStatistics result = new StatisticsService().Calculate(answers);

            // Assert
            Assert.That(result.PendingReviewAnswers, Is.EqualTo(2));
            Assert.That(result.ReviewedAnswers, Is.Zero);
            Assert.That(result.WrongReviewedAnswers, Is.Zero);
            Assert.That(result.ReviewedAccuracyPercentage, Is.Zero);
        }

        /// <summary>Confirms invalid truth remains pending and invalid user input is reviewed wrong.</summary>
        [Test]
        public void Statistics_InvalidEnumsFailClosed()
        {
            // Arrange
            List<QuizAnswer> answers = new List<QuizAnswer>
            {
                CreateAnswer(QuizAnswerType.Good, (QuizAnswerType)99, 1),
                CreateAnswer((QuizAnswerType)99, QuizAnswerType.Good, 2)
            };

            // Act
            ResultStatistics result = new StatisticsService().Calculate(answers);

            // Assert
            Assert.That(result.ReviewedAnswers, Is.EqualTo(1));
            Assert.That(result.PendingReviewAnswers, Is.EqualTo(1));
            Assert.That(result.CorrectReviewedAnswers, Is.Zero);
            Assert.That(result.WrongReviewedAnswers, Is.EqualTo(1));
            Assert.That(result.UserGoodAnswers, Is.EqualTo(1));
            Assert.That(result.UserNgAnswers, Is.Zero);
        }

        /// <summary>Confirms non-finite and negative timings are excluded.</summary>
        [Test]
        public void Statistics_InvalidTimingValuesAreExcluded()
        {
            // Arrange
            List<QuizAnswer> answers = new List<QuizAnswer>
            {
                CreateAnswer(QuizAnswerType.Good, null, 2.5),
                CreateAnswer(QuizAnswerType.Good, null, -1),
                CreateAnswer(QuizAnswerType.Ng, null, double.NaN),
                CreateAnswer(QuizAnswerType.Ng, null, double.PositiveInfinity)
            };

            // Act
            ResultStatistics result = new StatisticsService().Calculate(answers);

            // Assert
            Assert.That(result.ValidTimingAnswers, Is.EqualTo(1));
            Assert.That(result.TotalElapsedSeconds, Is.EqualTo(2.5));
            Assert.That(result.AverageElapsedSeconds, Is.EqualTo(2.5));
            Assert.That(result.FastestElapsedSeconds, Is.EqualTo(2.5));
            Assert.That(result.SlowestElapsedSeconds, Is.EqualTo(2.5));
        }

        /// <summary>Confirms results use a cloned snapshot rather than caller-owned rows.</summary>
        [Test]
        public void Statistics_ResultIsIndependentFromCallerMutations()
        {
            // Arrange
            QuizAnswer source = CreateAnswer(
                QuizAnswerType.Good,
                QuizAnswerType.Good,
                1);
            List<QuizAnswer> answers = new List<QuizAnswer> { source };

            // Act
            ResultStatistics result = new StatisticsService().Calculate(answers);
            source.CorrectAnswer = QuizAnswerType.Ng;
            answers.Clear();

            // Assert
            Assert.That(result.Answers, Has.Count.EqualTo(1));
            Assert.That(result.Answers[0].CorrectAnswer, Is.EqualTo(QuizAnswerType.Good));
            Assert.That(result.CorrectReviewedAnswers, Is.EqualTo(1));
        }

        /// <summary>Confirms review labels and stable image identity remain fail-safe.</summary>
        [Test]
        public void QuizAnswer_DisplayStateDistinguishesPendingAutomaticAndManualReview()
        {
            // Arrange
            QuizAnswer pending = CreateAnswer(QuizAnswerType.Good, null, 1);
            pending.ImageHash = new string('a', 64);
            QuizAnswer automatic = CreateAnswer(
                QuizAnswerType.Ng,
                QuizAnswerType.Ng,
                1);
            automatic.ImageHash = new string('b', 64);
            automatic.ReviewSource = QuizAnswer.AutomaticReviewSource;
            automatic.IsCorrect = true;
            QuizAnswer legacy = CreateAnswer(
                QuizAnswerType.Good,
                QuizAnswerType.Ng,
                1);

            // Act and Assert
            Assert.That(pending.ResultText, Is.EqualTo("Pending Review"));
            Assert.That(pending.ReviewStatusText, Is.EqualTo("Administrator review required"));
            Assert.That(automatic.IsAutoReviewed, Is.True);
            Assert.That(automatic.ResultText, Is.EqualTo("Correct"));
            Assert.That(legacy.HasStableIdentity, Is.False);
            Assert.That(legacy.ReviewStatusText, Is.EqualTo("Stable image identity unavailable"));
        }

        #endregion

        #region Fixtures

        private static User CreateUser()
        {
            return new User
            {
                UserID = 17,
                EmployeeNo = "I17TRAINEE",
                FullName = "Regression Trainee",
                Role = UserRoles.User,
                IsActive = true
            };
        }

        private static List<QuizImage> CreateImages(int count)
        {
            return Enumerable.Range(1, count)
                .Select(CreateImage)
                .ToList();
        }

        private static QuizImage CreateImage(int identity)
        {
            return new QuizImage
            {
                ImageID = identity,
                FileName = "fixture-" + identity + ".bmp",
                FilePath = "fixture-" + identity + ".bmp",
                ImageHash = identity.ToString("x").PadLeft(64, '0')
            };
        }

        private static List<QuizAnswer> CreateControlledSixAnswerDataset()
        {
            return new List<QuizAnswer>
            {
                CreateAnswer(QuizAnswerType.Good, QuizAnswerType.Good, 1),
                CreateAnswer(QuizAnswerType.Ng, QuizAnswerType.Ng, 2),
                CreateAnswer(QuizAnswerType.Ng, QuizAnswerType.Good, 3),
                CreateAnswer(QuizAnswerType.Good, QuizAnswerType.Ng, 4),
                CreateAnswer(QuizAnswerType.Good, null, 5),
                CreateAnswer(QuizAnswerType.Ng, null, 6)
            };
        }

        private static QuizAnswer CreateAnswer(
            QuizAnswerType userAnswer,
            QuizAnswerType? correctAnswer,
            double elapsedSeconds)
        {
            return new QuizAnswer
            {
                UserAnswer = userAnswer,
                CorrectAnswer = correctAnswer,
                ElapsedSeconds = elapsedSeconds,
                IsCorrect = correctAnswer.HasValue &&
                            userAnswer == correctAnswer.Value
            };
        }

        #endregion
    }
}
