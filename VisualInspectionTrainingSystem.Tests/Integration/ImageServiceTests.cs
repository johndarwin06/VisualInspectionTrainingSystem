#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Integration
{
    /// <summary>
    /// Covers image-folder failure modes, deterministic sampling constraints, hashes, and cancellation.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Integration)]
    [NonParallelizable]
    public sealed class ImageServiceTests
    {
        #region Fields

        private string _temporaryDirectory;

        #endregion

        #region Lifecycle

        /// <summary>Creates one isolated image catalog.</summary>
        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "VITS-I17-Images-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        /// <summary>Removes the isolated image catalog.</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        #endregion

        #region Folder Tests

        /// <summary>Confirms an empty folder is valid and returns no questions.</summary>
        [Test]
        public void EmptyFolder_ReturnsEmptyCatalog()
        {
            // Arrange
            ImageService service = new ImageService(new Random(17));

            // Act
            List<QuizImage> images = service.LoadImages(
                _temporaryDirectory,
                false);

            // Assert
            Assert.That(images, Is.Empty);
        }

        /// <summary>Confirms a missing image folder fails with a fixed non-path message.</summary>
        [Test]
        public void MissingFolder_ThrowsSafeDirectoryError()
        {
            // Arrange
            string missing = Path.Combine(_temporaryDirectory, "missing");
            ImageService service = new ImageService(new Random(17));

            // Act
            DirectoryNotFoundException exception =
                Assert.Throws<DirectoryNotFoundException>(
                    () => service.LoadImages(missing, false));

            // Assert
            Assert.That(
                exception.Message,
                Is.EqualTo("The configured image folder was not found."));
            Assert.That(exception.Message, Does.Not.Contain(missing));
        }

        /// <summary>Confirms fewer available files returns the bounded available set.</summary>
        [Test]
        public void QuizSample_FewerAvailableImagesReturnsEveryUniqueImage()
        {
            // Arrange
            CreateBitmapFixtures(6);
            ImageService service = new ImageService(new Random(17));

            // Act
            List<QuizImage> images = service.LoadQuizImages(
                _temporaryDirectory,
                10);

            // Assert
            Assert.That(images, Has.Count.EqualTo(6));
            Assert.That(
                images.Select(image => image.FilePath),
                Is.Unique);
        }

        /// <summary>Confirms case-insensitive duplicate paths are removed before sampling.</summary>
        [Test]
        public void QuizSample_RemovesCaseInsensitiveDuplicatePaths()
        {
            // Arrange
            ImageService service = new ImageService(new Random(17));
            List<QuizImage> candidates = new List<QuizImage>
            {
                new QuizImage { FilePath = "C:\\Images\\One.bmp" },
                new QuizImage { FilePath = "c:\\images\\ONE.BMP" },
                new QuizImage { FilePath = "C:\\Images\\Two.bmp" },
                null
            };
            MethodInfo method = typeof(ImageService).GetMethod(
                "CreateUniqueQuizSample",
                BindingFlags.Instance | BindingFlags.NonPublic);

            // Act
            List<QuizImage> sample = (List<QuizImage>)method.Invoke(
                service,
                new object[] { candidates, 10 });

            // Assert
            Assert.That(sample, Has.Count.EqualTo(2));
            Assert.That(
                sample.Select(image => image.FilePath.ToUpperInvariant()),
                Is.Unique);
        }

        #endregion

        #region Hash and Cancellation Tests

        /// <summary>Confirms exact bytes produce a stable lowercase SHA-256 identity.</summary>
        [Test]
        public async Task ImageHash_IsStableLowercaseSha256()
        {
            // Arrange
            string path = Path.Combine(_temporaryDirectory, "hash.bmp");
            byte[] bytes = Encoding.UTF8.GetBytes("Issue 17 stable image bytes");
            File.WriteAllBytes(path, bytes);
            string expected;

            using (SHA256 sha = SHA256.Create())
            {
                expected = string.Concat(
                    sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }

            ImageService service = new ImageService(new Random(17));

            // Act
            string first = await service.ComputeImageHashAsync(
                path,
                CancellationToken.None);
            string second = await service.ComputeImageHashAsync(
                path,
                CancellationToken.None);

            // Assert
            Assert.That(first, Is.EqualTo(expected));
            Assert.That(second, Is.EqualTo(expected));
            Assert.That(first, Has.Length.EqualTo(64));
            Assert.That(ImageService.NormalizeImageHash(first.ToUpperInvariant()), Is.EqualTo(first));
        }

        /// <summary>Confirms pre-cancelled hashing completes as cancellation without file mutation.</summary>
        [Test]
        public void ImageHash_PreCancelledRequestIsCancelledPromptly()
        {
            // Arrange
            string path = Path.Combine(_temporaryDirectory, "cancel.bmp");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
            CancellationTokenSource source = new CancellationTokenSource();
            source.Cancel();

            try
            {
                // Act
                Task<string> task = new ImageService(new Random(17))
                    .ComputeImageHashAsync(path, source.Token);

                // Assert
                Assert.That(
                    () => task.GetAwaiter().GetResult(),
                    Throws.InstanceOf<OperationCanceledException>());
                Assert.That(new FileInfo(path).Length, Is.EqualTo(4));
            }
            finally
            {
                source.Dispose();
            }
        }

        #endregion

        #region Fixtures

        private void CreateBitmapFixtures(int count)
        {
            for (int index = 0; index < count; index++)
            {
                File.WriteAllBytes(
                    Path.Combine(
                        _temporaryDirectory,
                        "fixture-" + index + ".bmp"),
                    new[] { (byte)index });
            }
        }

        #endregion
    }
}
