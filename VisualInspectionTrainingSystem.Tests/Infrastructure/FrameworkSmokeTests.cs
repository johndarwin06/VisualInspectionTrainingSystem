#region Namespaces

using NUnit.Framework;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Infrastructure
{
    /// <summary>
    /// Verifies that the permanent net462 test assembly and NUnit adapter are active.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Unit)]
    public sealed class FrameworkSmokeTests
    {
        #region Tests

        /// <summary>
        /// Confirms the test assembly targets the same CLR contract as the application.
        /// </summary>
        [Test]
        public void TestAssembly_UsesFullFrameworkClrContract()
        {
            // Arrange
            string frameworkDescription = typeof(object).Assembly.ImageRuntimeVersion;

            // Act
            bool usesClrFour = frameworkDescription.StartsWith("v4.");

            // Assert
            Assert.That(usesClrFour, Is.True);
            Assert.That(TestCategories.IsKnown(TestCategories.Unit), Is.True);
        }

        #endregion
    }
}
