#region Namespaces

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Loads quiz image metadata, creates quiz-specific samples, and decodes detached WPF bitmaps.
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

        #region Public Methods

        /// <summary>
        /// Loads all BMP image metadata from a folder.
        /// </summary>
        /// <param name="folderPath">Folder containing quiz images.</param>
        /// <param name="shuffle">Whether to shuffle images before returning.</param>
        /// <returns>Complete quiz image metadata for the folder.</returns>
        public List<QuizImage> LoadImages(
            string folderPath,
            bool shuffle = true)
        {
            List<QuizImage> images = new List<QuizImage>();

            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException(
                    "Folder path cannot be empty.",
                    nameof(folderPath));

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException(
                    "Image folder not found.\n\n" + folderPath);

            string[] files = Directory.GetFiles(
                folderPath,
                "*.bmp",
                SearchOption.TopDirectoryOnly);
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
        /// Returns whether a value is a supported trainee quiz size.
        /// </summary>
        /// <param name="requestedCount">Requested number of questions.</param>
        /// <returns>True only for 10 or 20.</returns>
        public static bool IsSupportedQuizSize(int requestedCount)
        {
            return requestedCount == DefaultQuizSize ||
                   requestedCount == ExtendedQuizSize;
        }

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
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "Image path cannot be empty.",
                    nameof(filePath));

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
                {
                    bitmap.Freeze();
                }

                cancellationToken.ThrowIfCancellationRequested();

                return bitmap;
            }
        }

        #endregion

        #region Ordering Helpers

        /// <summary>
        /// Randomizes image order using the Fisher-Yates algorithm.
        /// </summary>
        private void Shuffle(List<QuizImage> images)
        {
            for (int i = images.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                QuizImage temp = images[i];

                images[i] = images[j];
                images[j] = temp;
            }
        }

        #endregion
    }
}
