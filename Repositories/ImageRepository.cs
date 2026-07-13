#region Namespaces

using System;
using System.Collections.Generic;
using System.IO;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Repositories
{
    /// <summary>
    /// Repository responsible for loading quiz images.
    /// Currently loads images from a folder.
    /// Later this repository will load from MySQL.
    /// </summary>
    public class ImageRepository
    {
        #region Fields

        private readonly Random _random;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the image repository.
        /// </summary>
        public ImageRepository()
        {
            _random = new Random();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads all BMP images.
        /// </summary>
        public List<QuizImage> GetImages(
            string folderPath,
            bool shuffle = true)
        {
            string validatedFolderPath = ValidateFolderPath(folderPath);

            List<QuizImage> images = new List<QuizImage>();

            string[] files =
                Directory.GetFiles(
                    validatedFolderPath,
                    "*.bmp",
                    SearchOption.TopDirectoryOnly);

            Array.Sort(
                files,
                StringComparer.OrdinalIgnoreCase);

            int id = 1;

            foreach (string file in files)
            {
                images.Add(new QuizImage
                {
                    ImageID = id++,
                    FileName = Path.GetFileName(file),
                    FilePath = file,
                    Category = "General",
                    Remarks = string.Empty,
                    IsActive = true
                });
            }

            if (shuffle)
            {
                Shuffle(images);
            }

            return images;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates and normalizes the image folder path.
        /// </summary>
        private static string ValidateFolderPath(string folderPath)
        {
            if (folderPath == null)
                throw new ArgumentNullException(nameof(folderPath));

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException(
                    "Image folder path must not be empty.",
                    nameof(folderPath));
            }

            if (folderPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                throw new ArgumentException(
                    "Image folder path contains invalid characters.",
                    nameof(folderPath));
            }

            string fullPath = Path.GetFullPath(folderPath);

            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException(fullPath);

            return fullPath;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Shuffles loaded images in memory.
        /// </summary>
        private void Shuffle(List<QuizImage> images)
        {
            if (images == null)
                throw new ArgumentNullException(nameof(images));

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
