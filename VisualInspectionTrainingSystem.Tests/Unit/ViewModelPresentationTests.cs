#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Unit
{
    /// <summary>
    /// Covers quiz-size presentation, role-aware home state, Result filters, and missing previews.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Unit)]
    [NonParallelizable]
    public sealed class ViewModelPresentationTests
    {
        #region Lifecycle

        /// <summary>Clears shared login state around presentation tests.</summary>
        [SetUp]
        public void SetUp()
        {
            SessionService.Logout();
        }

        /// <summary>Clears shared login state around presentation tests.</summary>
        [TearDown]
        public void TearDown()
        {
            SessionService.Logout();
        }

        #endregion

        #region Home Tests

        /// <summary>Confirms ten questions are the default and twenty is the only alternate size.</summary>
        [Test]
        public void Home_QuizSizeDefaultsAndSelectionAreStable()
        {
            // Arrange and Act
            HomeViewModel viewModel = new HomeViewModel();

            // Assert
            Assert.That(viewModel.QuizSizeOptions, Is.EqualTo(new[] { 10, 20 }));
            Assert.That(viewModel.SelectedQuizSize, Is.EqualTo(10));
            Assert.That(viewModel.IsTenQuestionQuizSelected, Is.True);
            Assert.That(viewModel.IsTwentyQuestionQuizSelected, Is.False);

            viewModel.IsTwentyQuestionQuizSelected = true;
            Assert.That(viewModel.SelectedQuizSize, Is.EqualTo(20));
            Assert.That(viewModel.QuizSizeSummary, Is.EqualTo("20 inspection images selected"));
        }

        /// <summary>Confirms unsupported quiz sizes are rejected before navigation.</summary>
        [Test]
        public void Home_UnsupportedQuizSizeIsRejected()
        {
            // Arrange
            HomeViewModel viewModel = new HomeViewModel();

            // Act and Assert
            Assert.That(
                () => viewModel.SelectedQuizSize = 15,
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(viewModel.SelectedQuizSize, Is.EqualTo(10));
        }

        /// <summary>Confirms administrator navigation is hidden from trainee sessions.</summary>
        [Test]
        public void Home_AdministratorVisibilityIsRoleAware()
        {
            // Arrange
            HomeViewModel viewModel = new HomeViewModel();
            SessionService.Login(new User
            {
                EmployeeNo = "I17USER",
                FullName = "Regression User",
                Role = UserRoles.User,
                IsActive = true
            });

            // Act and Assert
            Assert.That(viewModel.AdminVisibility, Is.EqualTo(Visibility.Collapsed));

            SessionService.Login(new User
            {
                EmployeeNo = "I17ADMIN",
                FullName = "Regression Admin",
                Role = UserRoles.Admin,
                IsActive = true
            });
            Assert.That(viewModel.AdminVisibility, Is.EqualTo(Visibility.Visible));
        }

        #endregion

        #region Result Tests

        /// <summary>Confirms all supported Result filters match the controlled six-answer fixture.</summary>
        [TestCase(ResultViewModel.AllFilter, 6)]
        [TestCase(ResultViewModel.WrongFilter, 2)]
        [TestCase(ResultViewModel.NgFilter, 3)]
        [TestCase(ResultViewModel.PendingFilter, 2)]
        public void Result_FilterReturnsExpectedRows(string filter, int expectedCount)
        {
            // Arrange
            ResultViewModel viewModel = new ResultViewModel(
                CreateControlledSixAnswerDataset());

            try
            {
                // Act
                viewModel.FilterCommand.Execute(filter);

                // Assert
                Assert.That(viewModel.SelectedFilter, Is.EqualTo(filter));
                Assert.That(viewModel.DisplayedAnswers, Has.Count.EqualTo(expectedCount));
                Assert.That(
                    viewModel.FilteredAnswerCountText,
                    Is.EqualTo(expectedCount + " of 6 answers"));
            }
            finally
            {
                viewModel.Dispose();
            }
        }

        /// <summary>Confirms an unavailable image produces a safe status and no preview.</summary>
        [Test]
        public void Result_MissingImagePreviewFailsSafely()
        {
            // Arrange
            ResultViewModel viewModel = new ResultViewModel(
                CreateControlledSixAnswerDataset());

            try
            {
                QuizAnswer answer = viewModel.Answers[0];
                answer.FilePath = "Z:\\VITS-I17-missing\\image.bmp";

                // Act
                viewModel.SelectedAnswer = answer;
                bool completed = SpinWait.SpinUntil(
                    () => !viewModel.IsPreviewLoading,
                    5000);

                // Assert
                Assert.That(completed, Is.True);
                Assert.That(viewModel.SelectedImagePreview, Is.Null);
                Assert.That(viewModel.PreviewStatus, Does.Not.Contain("Z:\\"));
                Assert.That(viewModel.PreviewStatus, Does.Not.Contain("exception"));
            }
            finally
            {
                viewModel.Dispose();
            }
        }

        #endregion

        #region Fixtures

        private static List<QuizAnswer> CreateControlledSixAnswerDataset()
        {
            return new List<QuizAnswer>
            {
                CreateAnswer(QuizAnswerType.Good, QuizAnswerType.Good),
                CreateAnswer(QuizAnswerType.Ng, QuizAnswerType.Ng),
                CreateAnswer(QuizAnswerType.Ng, QuizAnswerType.Good),
                CreateAnswer(QuizAnswerType.Good, QuizAnswerType.Ng),
                CreateAnswer(QuizAnswerType.Good, null),
                CreateAnswer(QuizAnswerType.Ng, null)
            };
        }

        private static QuizAnswer CreateAnswer(
            QuizAnswerType userAnswer,
            QuizAnswerType? correctAnswer)
        {
            return new QuizAnswer
            {
                UserAnswer = userAnswer,
                CorrectAnswer = correctAnswer,
                FileName = "fixture.bmp",
                FilePath = string.Empty,
                IsCorrect = correctAnswer.HasValue &&
                            userAnswer == correctAnswer.Value
            };
        }

        #endregion
    }
}
