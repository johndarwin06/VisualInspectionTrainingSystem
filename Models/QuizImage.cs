#region Namespaces

using System;
using System.IO;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents one inspection image used during training.
    /// The stable identity is derived from exact file bytes and is independent of the transient catalog ID.
    /// </summary>
    public class QuizImage
    {
        #region Constructor

        /// <summary>
        /// Creates active image metadata with the current local creation time.
        /// </summary>
        public QuizImage()
        {
            CreatedDate = DateTime.Now;
            IsActive = true;
        }

        #endregion

        #region Identity

        /// <summary>
        /// Gets or sets the transient catalog identity retained for compatibility and display.
        /// </summary>
        public int ImageID { get; set; }

        /// <summary>
        /// Gets or sets the normalized lowercase SHA-256 hash of the exact image bytes.
        /// </summary>
        public string ImageHash { get; set; }

        /// <summary>
        /// Gets whether this image has a valid stable SHA-256 identity.
        /// </summary>
        public bool HasStableIdentity
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ImageHash) &&
                       ImageHash.Length == 64;
            }
        }

        /// <summary>
        /// Gets the legacy filename-based key used for display only.
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
        /// Gets or sets the safe image file name.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the configured local image path.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets the file extension used for display.
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
        /// Gets or sets the training category.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets optional administrator remarks.
        /// </summary>
        public string Remarks { get; set; }

        #endregion

        #region Status

        /// <summary>
        /// Gets or sets whether the image can participate in training.
        /// </summary>
        public bool IsActive { get; set; }

        #endregion

        #region Audit

        /// <summary>
        /// Gets or sets the local import date.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        #endregion

        #region Display

        /// <summary>
        /// Gets the filename shown by the application.
        /// </summary>
        public string DisplayName
        {
            get
            {
                return ImageKey + Extension;
            }
        }

        #endregion
    }
}
