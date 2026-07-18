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
    /// Loads quiz image metadata and creates detached WPF bitmap instances.
    /// </summary>
    public class ImageService
    {
        #region Fields

        private readonly Random _random;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates the image service and its quiz-order randomizer.
        /// </summary>
        public ImageService()
        {
            _random = new Random();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads all BMP image metadata from a folder.
        /// </summary>
        /// <param name="folderPath">Folder containing quiz images.</param>
        /// <param name="shuffle">Whether to shuffle images before returning.</param>
        /// <returns>Quiz image metadata.</returns>
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
