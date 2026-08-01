#region Namespaces

using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

#endregion

#region Assembly Information

[assembly: AssemblyTitle("VisualInspectionTrainingSystem")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("VisualInspectionTrainingSystem")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

#endregion

#region Testability

// Grants the permanent regression project access to internal implementation seams
// without expanding the application's public API surface.
[assembly: InternalsVisibleTo("VisualInspectionTrainingSystem.Tests")]

#endregion

#region Interoperability

[assembly: ComVisible(false)]

#endregion

#region WPF Theme Resources

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly)]

#endregion

#region Version Information

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

#endregion
