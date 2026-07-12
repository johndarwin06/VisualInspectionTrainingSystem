#region Namespaces

using System;
using System.IO;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents one inspection image used during training.
    /// This model only contains information about the image itself.
    /// It does not store user answers or quiz results.
    /// </summary>
    public class QuizImage
    {
        #region Constructor

        public QuizImage()
        {
            CreatedDate = DateTime.Now;
            IsActive = true;
        }

        #endregion

        #region Identity

        /// <summary>
        /// Database identity.
        /// Will be assigned by MySQL after importing images.
        /// </summary>
        public int ImageID { get; set; }

        /// <summary>
        /// Unique identifier based on file name.
        /// Example:
        /// CL43B104.bmp
        /// </summary>
        public string ImageKey
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FileName))
                    return string.Empty;

                return Path.GetFileNameWithoutExtension(FileName);
            }
        }

        #endregion

        #region File Information

        /// <summary>
        /// Image file name.
        /// Example:
        /// CL43B104.bmp
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Absolute image path.
        /// Example:
        /// D:\QuizImages\CL43B104.bmp
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// File extension.
        /// Example:
        /// .bmp
        /// </summary>
        public string Extension
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FileName))
                    return string.Empty;

                return Path.GetExtension(FileName);
            }
        }

        #endregion

        #region Category

        /// <summary>
        /// Training category.
        /// Example:
        /// Appearance
        /// Scratch
        /// Crack
        /// Contamination
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Optional remarks from the administrator.
        /// </summary>
        public string Remarks { get; set; }

        #endregion

        #region Status

        /// <summary>
        /// Determines whether this image
        /// can participate in training.
        /// </summary>
        public bool IsActive { get; set; }

        #endregion

        #region Audit

        /// <summary>
        /// Import date.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        #endregion

        #region Display

        /// <summary>
        /// Returns the display name.
        /// </summary>
        public string DisplayName
        {
            get
            {
                return $"{ImageKey}{Extension}";
            }
        }

        #endregion
    }
}