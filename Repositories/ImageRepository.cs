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
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException(nameof(folderPath));

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException(folderPath);

            List<QuizImage> images = new List<QuizImage>();

            string[] files =
                Directory.GetFiles(
                    folderPath,
                    "*.bmp",
                    SearchOption.TopDirectoryOnly);

            int id = 1;

            foreach (string file in files)
            {
                images.Add(new QuizImage
                {
                    ImageID = id++,
                    FileName = Path.GetFileName(file),
                    FilePath = file,
                    Category = "General",
                    Remarks = "",
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

        #region Private Methods

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