#region Namespaces

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Loads quiz image metadata, creates quiz samples, computes stable identities, and decodes detached bitmaps.
    /// </summary>
    public class ImageService
    {
        #region Constants

        /// <summary>
        /// Default number of questions offered to trainees.
        /// </summary>
        public const int DefaultQuizSize = 10;

        /// <summary>
        /// Extended number of questions offered to trainees.
        /// </summary>
        public const int ExtendedQuizSize = 20;

        private const int HashBufferSize = 81920;

        #endregion

        #region Static Fields

        private static readonly object HashCacheSyncRoot = new object();

        private static readonly Dictionary<string, HashCacheEntry> HashCache =
            new Dictionary<string, HashCacheEntry>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Fields

        private readonly Random _random;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates the image service and its quiz-order randomizer.
        /// </summary>
        public ImageService()
            : this(new Random())
        {
        }

        /// <summary>
        /// Creates the image service with a supplied random source for deterministic verification.
        /// </summary>
        /// <param name="random">Random source used by Fisher-Yates ordering.</param>
        internal ImageService(Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            _random = random;
        }

        #endregion

        #region Public Metadata Methods

        /// <summary>
        /// Loads all BMP image metadata from a folder without reading image bytes.
        /// </summary>
        /// <param name="folderPath">Folder containing quiz images.</param>
        /// <param name="shuffle">Whether to shuffle images before returning.</param>
        /// <returns>Complete quiz image metadata for the folder.</returns>
        public List<QuizImage> LoadImages(
            string folderPath,
            bool shuffle = true)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException(
                    "Folder path cannot be empty.",
                    nameof(folderPath));
            }

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException(
                    "The configured image folder was not found.");
            }

            string[] files = Directory.GetFiles(
                folderPath,
                "*.bmp",
                SearchOption.TopDirectoryOnly);

            Array.Sort(
                files,
                StringComparer.OrdinalIgnoreCase);

            List<QuizImage> images = new List<QuizImage>();
            int imageId = 1;

            foreach (string file in files)
            {
                QuizImage image = new QuizImage
                {
                    ImageID = imageId++,
                    FileName = Path.GetFileName(file),
                    FilePath = file,
                    Category = "General",
                    Remarks = string.Empty,
                    IsActive = true
                };

                string cachedHash;

                if (TryGetCachedHash(file, out cachedHash))
                {
                    image.ImageHash = cachedHash;
                }

                images.Add(image);
            }

            if (shuffle)
                Shuffle(images);

            return images;
        }

        /// <summary>
        /// Loads a randomized unique metadata sample for one trainee quiz.
        /// </summary>
        /// <param name="folderPath">Folder containing quiz images.</param>
        /// <param name="requestedCount">Supported requested size of 10 or 20.</param>
        /// <returns>At most the requested number of unique image metadata rows.</returns>
        public List<QuizImage> LoadQuizImages(
            string folderPath,
            int requestedCount)
        {
            ValidateQuizSize(requestedCount);

            List<QuizImage> candidates = LoadImages(
                folderPath,
                false);

            return CreateUniqueQuizSample(
                candidates,
                requestedCount);
        }

        /// <summary>
        /// Loads and hashes only the selected quiz images on a worker thread.
        /// </summary>
        /// <param name="folderPath">Folder containing quiz images.</param>
        /// <param name="requestedCount">Supported requested size of 10 or 20.</param>
        /// <param name="cancellationToken">Token observed during enumeration and hashing.</param>
        /// <returns>A selected quiz sample with stable lowercase SHA-256 identities.</returns>
        public Task<List<QuizImage>> LoadQuizImagesWithHashesAsync(
            string folderPath,
            int requestedCount,
            CancellationToken cancellationToken)
        {
            ValidateQuizSize(requestedCount);

            return Task.Run(
                delegate
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    List<QuizImage> images = LoadQuizImages(
                        folderPath,
                        requestedCount);

                    PopulateHashes(
                        images,
                        cancellationToken);

                    return images;
                },
                cancellationToken);
        }

        /// <summary>
        /// Loads and hashes the complete administrator catalog on a worker thread.
        /// </summary>
        /// <param name="folderPath">Folder containing quiz images.</param>
        /// <param name="shuffle">Whether the returned catalog should be shuffled.</param>
        /// <param name="cancellationToken">Token observed during enumeration and hashing.</param>
        /// <returns>Complete metadata with stable lowercase SHA-256 identities.</returns>
        public Task<List<QuizImage>> LoadImagesWithHashesAsync(
            string folderPath,
            bool shuffle,
            CancellationToken cancellationToken)
        {
            return Task.Run(
                delegate
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    List<QuizImage> images = LoadImages(
                        folderPath,
                        shuffle);

                    PopulateHashes(
                        images,
                        cancellationToken);

                    return images;
                },
                cancellationToken);
        }

        /// <summary>
        /// Returns whether a value is a supported trainee quiz size.
        /// </summary>
        /// <param name="requestedCount">Requested number of questions.</param>
        /// <returns>True only for 10 or 20.</returns>
        public static bool IsSupportedQuizSize(int requestedCount)
        {
            return requestedCount == DefaultQuizSize ||
                   requestedCount == ExtendedQuizSize;
        }

        #endregion

        #region Public Hash Methods

        /// <summary>
        /// Computes or retrieves the normalized lowercase SHA-256 identity for exact file bytes.
        /// </summary>
        /// <param name="filePath">Existing image file path.</param>
        /// <param name="cancellationToken">Token observed before and while the file is read.</param>
        /// <returns>A task producing exactly 64 lowercase hexadecimal characters.</returns>
        public Task<string> ComputeImageHashAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            ValidateFilePath(filePath);

            return Task.Run(
                delegate
                {
                    return ComputeImageHash(
                        filePath,
                        cancellationToken);
                },
                cancellationToken);
        }

        /// <summary>
        /// Normalizes and validates a stored SHA-256 identity.
        /// </summary>
        /// <param name="imageHash">Hash text to normalize.</param>
        /// <returns>The lowercase hash.</returns>
        public static string NormalizeImageHash(string imageHash)
        {
            if (string.IsNullOrWhiteSpace(imageHash))
                throw new ArgumentException("Image hash is required.", nameof(imageHash));

            string normalized = imageHash.Trim().ToLowerInvariant();

            if (normalized.Length != 64)
            {
                throw new ArgumentException(
                    "Image hash must contain 64 hexadecimal characters.",
                    nameof(imageHash));
            }

            for (int index = 0; index < normalized.Length; index++)
            {
                char character = normalized[index];
                bool isHex = character >= '0' && character <= '9' ||
                             character >= 'a' && character <= 'f';

                if (!isHex)
                {
                    throw new ArgumentException(
                        "Image hash must contain only hexadecimal characters.",
                        nameof(imageHash));
                }
            }

            return normalized;
        }

        #endregion

        #region Public Bitmap Methods

        /// <summary>
        /// Decodes one bitmap on a worker thread and releases its source file before completion.
        /// </summary>
        /// <param name="filePath">Bitmap file path.</param>
        /// <param name="cancellationToken">Token that rejects canceled work before or after decoding.</param>
        /// <returns>A task that produces a frozen, detached bitmap.</returns>
        public Task<BitmapImage> LoadBitmapAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            ValidateFilePath(filePath);

            return Task.Run(
                delegate
                {
                    return LoadBitmap(
                        filePath,
                        cancellationToken);
                },
                cancellationToken);
        }

        #endregion

        #region Quiz Sampling

        /// <summary>
        /// Rejects unsupported quiz sizes before folder access or quiz execution.
        /// </summary>
        private static void ValidateQuizSize(int requestedCount)
        {
            if (!IsSupportedQuizSize(requestedCount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedCount),
                    requestedCount,
                    "Quiz size must be 10 or 20.");
            }
        }

        /// <summary>
        /// Removes case-insensitive duplicate paths, shuffles once, and takes a bounded sample.
        /// </summary>
        private List<QuizImage> CreateUniqueQuizSample(
            IEnumerable<QuizImage> candidates,
            int requestedCount)
        {
            ValidateQuizSize(requestedCount);

            List<QuizImage> uniqueImages = new List<QuizImage>();
            HashSet<string> uniquePaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            if (candidates != null)
            {
                foreach (QuizImage image in candidates)
                {
                    if (image == null ||
                        string.IsNullOrWhiteSpace(image.FilePath) ||
                        !uniquePaths.Add(image.FilePath))
                    {
                        continue;
                    }

                    uniqueImages.Add(image);
                }
            }

            Shuffle(uniqueImages);

            int selectedCount = Math.Min(
                requestedCount,
                uniqueImages.Count);

            if (selectedCount == uniqueImages.Count)
                return uniqueImages;

            return uniqueImages.GetRange(
                0,
                selectedCount);
        }

        #endregion

        #region Stable Identity

        /// <summary>
        /// Populates every selected image hash and observes cancellation between files.
        /// </summary>
        private static void PopulateHashes(
            IEnumerable<QuizImage> images,
            CancellationToken cancellationToken)
        {
            if (images == null)
                return;

            foreach (QuizImage image in images)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (image == null)
                    continue;

                image.ImageHash = ComputeImageHash(
                    image.FilePath,
                    cancellationToken);
            }
        }

        /// <summary>
        /// Computes a stable hash or returns a cache entry that still matches file metadata.
        /// </summary>
        private static string ComputeImageHash(
            string filePath,
            CancellationToken cancellationToken)
        {
            ValidateFilePath(filePath);
            cancellationToken.ThrowIfCancellationRequested();

            string fullPath = Path.GetFullPath(filePath);
            FileInfo fileInfo = new FileInfo(fullPath);

            if (!fileInfo.Exists)
                throw new FileNotFoundException("The inspection image is unavailable.");

            HashCacheEntry cachedEntry;

            lock (HashCacheSyncRoot)
            {
                if (HashCache.TryGetValue(fullPath, out cachedEntry) &&
                    cachedEntry.Matches(fileInfo))
                {
                    return cachedEntry.Hash;
                }
            }

            string hash = ComputeFileHash(
                fullPath,
                cancellationToken);

            fileInfo.Refresh();

            HashCacheEntry currentEntry = new HashCacheEntry(
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc.Ticks,
                hash);

            lock (HashCacheSyncRoot)
            {
                HashCache[fullPath] = currentEntry;
            }

            return hash;
        }

        /// <summary>
        /// Reads a file in bounded buffers and computes lowercase SHA-256 text.
        /// </summary>
        private static string ComputeFileHash(
            string fullPath,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[HashBufferSize];

            using (FileStream stream = new FileStream(
                       fullPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       HashBufferSize,
                       FileOptions.SequentialScan))
            using (SHA256 sha256 = SHA256.Create())
            {
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    sha256.TransformBlock(
                        buffer,
                        0,
                        bytesRead,
                        null,
                        0);
                }

                cancellationToken.ThrowIfCancellationRequested();
                sha256.TransformFinalBlock(new byte[0], 0, 0);

                return ToLowerHex(sha256.Hash);
            }
        }

        /// <summary>
        /// Returns a still-valid cached hash without reading file contents.
        /// </summary>
        private static bool TryGetCachedHash(
            string filePath,
            out string imageHash)
        {
            imageHash = null;

            try
            {
                string fullPath = Path.GetFullPath(filePath);
                FileInfo fileInfo = new FileInfo(fullPath);
                HashCacheEntry cachedEntry;

                if (!fileInfo.Exists)
                    return false;

                lock (HashCacheSyncRoot)
                {
                    if (!HashCache.TryGetValue(fullPath, out cachedEntry) ||
                        !cachedEntry.Matches(fileInfo))
                    {
                        return false;
                    }
                }

                imageHash = cachedEntry.Hash;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Converts bytes to lowercase invariant hexadecimal text.
        /// </summary>
        private static string ToLowerHex(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            StringBuilder builder = new StringBuilder(bytes.Length * 2);

            for (int index = 0; index < bytes.Length; index++)
            {
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        #endregion

        #region Bitmap Helpers

        /// <summary>
        /// Reads, fully materializes, and freezes one WPF bitmap without retaining a file handle.
        /// </summary>
        private static BitmapImage LoadBitmap(
            string filePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] imageBytes = File.ReadAllBytes(filePath);

            using (MemoryStream stream = new MemoryStream(imageBytes, false))
            {
                BitmapImage bitmap = new BitmapImage();

                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();

                if (bitmap.CanFreeze)
                    bitmap.Freeze();

                cancellationToken.ThrowIfCancellationRequested();

                return bitmap;
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates a required local image path without exposing it in exception text.
        /// </summary>
        private static void ValidateFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "Image path cannot be empty.",
                    nameof(filePath));
            }
        }

        #endregion

        #region Ordering Helpers

        /// <summary>
        /// Randomizes image order using the Fisher-Yates algorithm.
        /// </summary>
        private void Shuffle(List<QuizImage> images)
        {
            for (int index = images.Count - 1; index > 0; index--)
            {
                int swapIndex = _random.Next(index + 1);
                QuizImage temporary = images[index];

                images[index] = images[swapIndex];
                images[swapIndex] = temporary;
            }
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Associates a content hash with the file metadata used to validate cache freshness.
        /// </summary>
        private sealed class HashCacheEntry
        {
            public HashCacheEntry(
                long length,
                long lastWriteTicks,
                string hash)
            {
                Length = length;
                LastWriteTicks = lastWriteTicks;
                Hash = hash;
            }

            public long Length { get; private set; }

            public long LastWriteTicks { get; private set; }

            public string Hash { get; private set; }

            public bool Matches(FileInfo fileInfo)
            {
                return fileInfo != null &&
                       Length == fileInfo.Length &&
                       LastWriteTicks == fileInfo.LastWriteTimeUtc.Ticks;
            }
        }

        #endregion
    }
}
