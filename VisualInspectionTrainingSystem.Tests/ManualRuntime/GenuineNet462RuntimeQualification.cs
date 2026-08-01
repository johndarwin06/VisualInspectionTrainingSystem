#region Namespaces

using NUnit.Framework;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.ManualRuntime
{
    /// <summary>
    /// Records the external deployment gate that cannot be inferred from a newer in-place CLR.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.ManualRuntime)]
    public sealed class GenuineNet462RuntimeQualification
    {
        #region Manual Gate

        /// <summary>
        /// Remains explicit until a person completes the TESTING.md checklist on a genuine
        /// machine or VM containing only the supported .NET Framework 4.6.2 runtime.
        /// </summary>
        [Test]
        [Explicit("Requires the genuine .NET Framework 4.6.2-only host checklist in TESTING.md.")]
        public void GenuineNet462OnlyHost_RequiresDocumentedManualQualification()
        {
            Assert.Inconclusive(
                "Record the target-host result outside this local automated run.");
        }

        #endregion
    }
}
