#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Performance
{
    /// <summary>
    /// Measures disposable quiz-image inventories and enforces sampling, decoding,
    /// identity, file-handle, and active-set correctness independently of timing.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Performance)]
    [NonParallelizable]
    public sealed class ImagePipelinePerformanceTests
    {
        #region Fields

        private string _temporaryDirectory;

        #endregion

        #region Lifecycle

        /// <summary>Creates a unique non-production image directory.</summary>
        [SetUp]
        public void SetUp()
        {
            ImageService.ClearHashCacheForTesting();
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "VITS-I18-Images-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        /// <summary>Removes every temporary image and proves handles were released.</summary>
        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_temporaryDirectory) &&
                Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }

            ImageService.ClearHashCacheForTesting();
            Assert.That(Directory.Exists(_temporaryDirectory), Is.False);
        }

        #endregion

        #region Catalog and Sampling

        /// <summary>
        /// Measures ordered metadata enumeration at representative catalog sizes.
        /// </summary>
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(5000)]
        public void CatalogEnumeration_ScalesAndPreservesEveryMetadataRow(int imageCount)
        {
            CreateBitmapFixtures(imageCount);
            ImageService service = new ImageService(new Random(1800 + imageCount));
            List<QuizImage> latest = null;

            PerformanceSampleSet result = PerformanceMeasurement.Measure(
                "Images.Catalog." + imageCount,
                1,
                7,
                delegate
                {
                    latest = service.LoadImages(_temporaryDirectory, false);
                });

            Assert.Multiple(delegate
            {
                Assert.That(result.Count, Is.EqualTo(7));
                Assert.That(latest, Has.Count.EqualTo(imageCount));
                Assert.That(latest.Select(image => image.FilePath), Is.Unique);
                Assert.That(latest.All(image => image.IsActive), Is.True);
            });
        }

        /// <summary>
        /// Measures 10/20 selection and hashing from a 5,000-image inventory while
        /// enforcing that only the requested active sample is retained by the caller.
        /// </summary>
        [TestCase(ImageService.DefaultQuizSize)]
        [TestCase(ImageService.ExtendedQuizSize)]
        public async Task QuizSelectionAndHashing_LargeCatalogReturnsOnlyRequestedImages(
            int requestedCount)
        {
            CreateBitmapFixtures(5000);
            ImageService service = new ImageService(new Random(1818 + requestedCount));
            List<QuizImage> latest = null;

            PerformanceSampleSet result = await PerformanceMeasurement.MeasureAsync(
                "Images.SelectHash.5000x" + requestedCount,
                1,
                7,
                async delegate
                {
                    latest = await service.LoadQuizImagesWithHashesAsync(
                        _temporaryDirectory,
                        requestedCount,
                        CancellationToken.None);
                });

            Assert.Multiple(delegate
            {
                Assert.That(result.Count, Is.EqualTo(7));
                Assert.That(latest, Has.Count.EqualTo(requestedCount));
                Assert.That(latest.Select(image => image.FilePath), Is.Unique);
                Assert.That(latest.All(image => image.ImageHash != null && image.ImageHash.Length == 64), Is.True);
            });
        }

        #endregion

        #region Decode and Resource Stability

        /// <summary>
        /// Measures detached decoding and proves source files remain deletable.
        /// </summary>
        [Test]
        public async Task BitmapDecode_TwentyImagesReleasesEverySourceHandle()
        {
            CreateBitmapFixtures(20);
            string[] files = Directory.GetFiles(_temporaryDirectory, "*.bmp");
            ImageService service = new ImageService(new Random(1818));
            int decodedCount = 0;

            PerformanceSampleSet result = await PerformanceMeasurement.MeasureAsync(
                "Images.Decode.20",
                1,
                5,
                async delegate
                {
                    decodedCount = 0;

                    foreach (string file in files)
                    {
                        await service.LoadBitmapAsync(file, CancellationToken.None);
                        decodedCount++;
                    }
                });

            foreach (string file in files)
                File.Delete(file);

            Assert.Multiple(delegate
            {
                Assert.That(result.Count, Is.EqualTo(5));
                Assert.That(decodedCount, Is.EqualTo(20));
                Assert.That(Directory.GetFiles(_temporaryDirectory), Is.Empty);
            });
        }

        /// <summary>
        /// Proves unique disposable paths cannot make the process-wide hash cache
        /// exceed its documented least-recently-used capacity.
        /// </summary>
        [Test]
        public async Task HashCache_UniquePathsRemainBounded()
        {
            int imageCount = ImageService.MaximumHashCacheEntries + 904;
            CreateBitmapFixtures(imageCount);
            string[] files = Directory.GetFiles(_temporaryDirectory, "*.bmp");
            ImageService service = new ImageService(new Random(1818));
            int before = ImageService.HashCacheEntryCount;

            foreach (string file in files)
            {
                await service.ComputeImageHashAsync(
                    file,
                    CancellationToken.None);
            }

            int after = ImageService.HashCacheEntryCount;
            int growth = after - before;

            TestContext.Progress.WriteLine(
                "RESOURCE|Images.HashCache|Before={0}|After={1}|Growth={2}|Workload={3}",
                before,
                after,
                growth,
                imageCount);

            Assert.Multiple(delegate
            {
                Assert.That(before, Is.Zero);
                Assert.That(after, Is.EqualTo(ImageService.MaximumHashCacheEntries));
                Assert.That(growth, Is.LessThan(imageCount));
            });
        }

        /// <summary>Confirms a pre-canceled quiz load performs no active selection.</summary>
        [Test]
        public void QuizSelection_PreCanceledTokenStopsBeforeEnumeration()
        {
            CreateBitmapFixtures(100);
            ImageService service = new ImageService(new Random(1818));

            using (CancellationTokenSource cancellation =
                       new CancellationTokenSource())
            {
                cancellation.Cancel();

                Assert.CatchAsync<OperationCanceledException>(
                    async delegate
                    {
                        await service.LoadQuizImagesWithHashesAsync(
                            _temporaryDirectory,
                            ImageService.DefaultQuizSize,
                            cancellation.Token);
                    });
            }

            Assert.That(ImageService.HashCacheEntryCount, Is.Zero);
        }

        /// <summary>Confirms concurrent stable-identity work remains deterministic and bounded.</summary>
        [Test]
        public async Task HashCache_ConcurrentUniqueFilesRemainDeterministic()
        {
            const int imageCount = 200;
            CreateBitmapFixtures(imageCount);
            string[] files = Directory.GetFiles(_temporaryDirectory, "*.bmp");
            ImageService service = new ImageService(new Random(1818));
            Task<string>[] tasks = files
                .Select(
                    file => service.ComputeImageHashAsync(
                        file,
                        CancellationToken.None))
                .ToArray();

            string[] hashes = await Task.WhenAll(tasks);

            Assert.Multiple(delegate
            {
                Assert.That(hashes, Has.Length.EqualTo(imageCount));
                Assert.That(hashes, Is.Unique);
                Assert.That(ImageService.HashCacheEntryCount, Is.EqualTo(imageCount));
                Assert.That(
                    ImageService.HashCacheEntryCount,
                    Is.LessThanOrEqualTo(ImageService.MaximumHashCacheEntries));
            });
        }

        #endregion

        #region Fixtures

        private void CreateBitmapFixtures(int count)
        {
            byte[] bitmap = CreateOnePixelBitmap();

            for (int index = 0; index < count; index++)
            {
                bitmap[54] = (byte)(index & 0xFF);
                bitmap[55] = (byte)((index >> 8) & 0xFF);
                bitmap[56] = (byte)((index >> 16) & 0xFF);

                File.WriteAllBytes(
                    Path.Combine(
                        _temporaryDirectory,
                        "image-" + index.ToString("D6") + ".bmp"),
                    bitmap);
            }
        }

        private static byte[] CreateOnePixelBitmap()
        {
            return new byte[]
            {
                0x42, 0x4D, 58, 0, 0, 0, 0, 0, 0, 0, 54, 0, 0, 0,
                40, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 24, 0,
                0, 0, 0, 0, 4, 0, 0, 0, 0x13, 0x0B, 0, 0, 0x13, 0x0B,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x20, 0x80, 0xE0, 0
            };
        }

        #endregion
    }
}
