#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.NativeDeployment
{
    /// <summary>
    /// Verifies the managed and native files required by the net462 deployment.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.NativeDeployment)]
    [NonParallelizable]
    public sealed class NativeDeploymentTests
    {
        #region Constants

        private static readonly string[] RequiredManagedFiles =
        {
            "VisualInpsectionTrainingSystem.exe",
            "Wpf.Ui.dll",
            "Wpf.Ui.Abstractions.dll",
            "Wpf.Ui.Violeta.dll",
            "LiveChartsCore.dll",
            "LiveChartsCore.SkiaSharpView.dll",
            "LiveChartsCore.SkiaSharpView.WPF.dll",
            "SkiaSharp.dll",
            "SkiaSharp.HarfBuzz.dll",
            "HarfBuzzSharp.dll",
            "MySql.Data.dll",
            "PdfSharp-wpf.dll",
            "DocumentFormat.OpenXml.dll",
            "DocumentFormat.OpenXml.Framework.dll",
            "BCrypt-Net-Next.dll",
            "log4net.dll",
            "netstandard.dll"
        };

        #endregion

        #region Managed Deployment Tests

        /// <summary>Confirms Debug and Release contain every required loadable managed assembly.</summary>
        [TestCase("Debug")]
        [TestCase("Release")]
        public void Configuration_ContainsRequiredManagedAssemblies(string configuration)
        {
            // Arrange
            string output = GetProductionOutput(configuration);
            List<string> missing = new List<string>();

            // Act
            foreach (string fileName in RequiredManagedFiles)
            {
                string path = Path.Combine(output, fileName);

                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    missing.Add(fileName);
                    continue;
                }

                AssemblyName.GetAssemblyName(path);
            }

            // Assert
            Assert.That(missing, Is.Empty);
        }

        /// <summary>Confirms runtime configuration identifies the genuine net462 target contract.</summary>
        [TestCase("Debug")]
        [TestCase("Release")]
        public void Configuration_DeclaresNet462Runtime(string configuration)
        {
            // Arrange
            string configPath = Path.Combine(
                GetProductionOutput(configuration),
                "VisualInpsectionTrainingSystem.exe.config");

            // Act
            string content = File.ReadAllText(configPath);

            // Assert
            Assert.That(content, Does.Contain(".NETFramework,Version=v4.6.2"));
        }

        #endregion

        #region Native Asset Tests

        /// <summary>Confirms both supported Windows architectures have SkiaSharp and HarfBuzz assets.</summary>
        [TestCase("Debug", "x86")]
        [TestCase("Debug", "x64")]
        [TestCase("Release", "x86")]
        [TestCase("Release", "x64")]
        public void Configuration_ContainsRequiredNativeAssets(
            string configuration,
            string architecture)
        {
            // Arrange
            string nativeDirectory = Path.Combine(
                GetProductionOutput(configuration),
                architecture);
            string skia = Path.Combine(nativeDirectory, "libSkiaSharp.dll");
            string harfBuzz = Path.Combine(nativeDirectory, "libHarfBuzzSharp.dll");

            // Act and Assert
            Assert.That(File.Exists(skia), Is.True);
            Assert.That(new FileInfo(skia).Length, Is.GreaterThan(1000));
            Assert.That(File.Exists(harfBuzz), Is.True);
            Assert.That(new FileInfo(harfBuzz).Length, Is.GreaterThan(1000));
        }

        /// <summary>Loads the native libraries matching the current test process and releases them safely.</summary>
        [Test]
        public void CurrentProcess_LoadsMatchingNativeAssets()
        {
            // Arrange
            string architecture = Environment.Is64BitProcess ? "x64" : "x86";
            string nativeDirectory = Path.Combine(
                GetProductionOutput("Debug"),
                architecture);
            string[] libraries =
            {
                Path.Combine(nativeDirectory, "libSkiaSharp.dll"),
                Path.Combine(nativeDirectory, "libHarfBuzzSharp.dll")
            };

            // Act and Assert
            foreach (string library in libraries)
            {
                IntPtr handle = LoadLibrary(library);

                try
                {
                    Assert.That(
                        handle,
                        Is.Not.EqualTo(IntPtr.Zero),
                        "Native load failed for " + Path.GetFileName(library));
                }
                finally
                {
                    if (handle != IntPtr.Zero)
                    {
                        FreeLibrary(handle);
                    }
                }
            }
        }

        #endregion

        #region Project Contract Tests

        /// <summary>Confirms target and HintPath declarations remain portable and repository-relative.</summary>
        [Test]
        public void Project_RemainsNet462AnyCpuWithoutDeveloperSpecificHintPaths()
        {
            // Arrange
            string projectPath = Path.Combine(
                GetRepositoryRoot(),
                "VisualInspectionTrainingSystem.csproj");
            string content = File.ReadAllText(projectPath);

            // Act and Assert
            Assert.That(content, Does.Contain("<TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>"));
            Assert.That(content, Does.Contain("<LangVersion>7.3</LangVersion>"));
            Assert.That(content, Does.Contain("<PlatformTarget>AnyCPU</PlatformTarget>"));
            Assert.That(content, Does.Contain("packages\\log4net.3.3.2\\lib\\net462\\log4net.dll"));
            Assert.That(content, Does.Not.Contain("C:\\Users\\"));
            Assert.That(content, Does.Not.Contain("D:\\"));
        }

        #endregion

        #region Native Methods

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);

        #endregion

        #region Paths

        private static string GetProductionOutput(string configuration)
        {
            return Path.Combine(
                GetRepositoryRoot(),
                "bin",
                configuration);
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
