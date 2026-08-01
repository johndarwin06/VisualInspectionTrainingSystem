#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Infrastructure
{
    /// <summary>
    /// Defines the stable category names used to select permanent regression tests.
    /// </summary>
    public static class TestCategories
    {
        #region Category Names

        /// <summary>Pure deterministic tests with no external resources.</summary>
        public const string Unit = "Unit";

        /// <summary>Tests spanning multiple application components.</summary>
        public const string Integration = "Integration";

        /// <summary>Tests requiring a real STA WPF dispatcher.</summary>
        public const string Wpf = "WPF";

        /// <summary>Tests requiring an explicitly configured test-only MySQL schema.</summary>
        public const string Database = "Database";

        /// <summary>Tests that validate report export content and cleanup.</summary>
        public const string Export = "Export";

        /// <summary>Tests that inspect deployable managed and native output.</summary>
        public const string NativeDeployment = "NativeDeployment";

        /// <summary>Qualification that must run on a genuine .NET Framework 4.6.2-only host.</summary>
        public const string ManualRuntime = "ManualRuntime";

        #endregion

        #region Validation

        /// <summary>
        /// Returns whether a category is one of the permanent supported values.
        /// </summary>
        /// <param name="category">Category to validate.</param>
        /// <returns>True when the category is recognized.</returns>
        public static bool IsKnown(string category)
        {
            return string.Equals(category, Unit, StringComparison.Ordinal) ||
                   string.Equals(category, Integration, StringComparison.Ordinal) ||
                   string.Equals(category, Wpf, StringComparison.Ordinal) ||
                   string.Equals(category, Database, StringComparison.Ordinal) ||
                   string.Equals(category, Export, StringComparison.Ordinal) ||
                   string.Equals(category, NativeDeployment, StringComparison.Ordinal) ||
                   string.Equals(category, ManualRuntime, StringComparison.Ordinal);
        }

        #endregion
    }
}
