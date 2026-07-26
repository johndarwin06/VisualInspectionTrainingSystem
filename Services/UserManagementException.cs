#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Identifies safe user-management failure categories without exposing database details.
    /// </summary>
    public enum UserManagementErrorCode
    {
        Validation,
        Unauthorized,
        DuplicateEmployeeNumber,
        UserNotFound,
        SelfProtection,
        FinalAdministrator,
        ConcurrentChange,
        Storage
    }

    /// <summary>
    /// Carries a fixed non-sensitive user-management failure message.
    /// </summary>
    public sealed class UserManagementException : InvalidOperationException
    {
        #region Constructor

        /// <summary>
        /// Creates a safe categorized failure.
        /// </summary>
        /// <param name="errorCode">Stable failure category.</param>
        /// <param name="message">Non-sensitive user-facing message.</param>
        /// <param name="innerException">Optional technical exception retained for logging.</param>
        public UserManagementException(
            UserManagementErrorCode errorCode,
            string message,
            Exception innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the stable safe failure category.
        /// </summary>
        public UserManagementErrorCode ErrorCode { get; private set; }

        #endregion
    }
}
