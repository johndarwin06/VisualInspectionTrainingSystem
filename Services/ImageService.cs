#region Namespaces

using System;
using System.Collections.Generic;
using System.IO;
using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Loads quiz images from a folder.
    /// Responsible only for reading image files.
    /// </summary>
    public class ImageService
    {
        #region Fields

        private readonly Random _random;

        #endregion

        #region Constructor

        public ImageService()
        {
            _random = new Random();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads all BMP images from a folder.
        /// </summary>
        /// <param name="folderPath">Folder containing quiz images.</param>
        /// <param name="shuffle">Shuffle images before returning.</param>
        /// <returns>List of quiz images.</returns>
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
                    $"Image folder not found.\n\n{folderPath}");

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

        #endregion

        #region Private Methods

        /// <summary>
        /// Randomizes the image order using the
        /// Fisher-Yates shuffle algorithm.
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