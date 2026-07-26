#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents one application user without exposing credentials in management projections.
    /// </summary>
    public class User
    {
        #region Identity

        /// <summary>
        /// Gets or sets the database user identity.
        /// </summary>
        public int UserID { get; set; }

        /// <summary>
        /// Gets or sets the unique employee number.
        /// </summary>
        public string EmployeeNo { get; set; }

        #endregion

        #region Profile

        /// <summary>
        /// Gets or sets the user's display name.
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// Gets or sets the user's department.
        /// </summary>
        public string Department { get; set; }

        /// <summary>
        /// Gets or sets the canonical application role.
        /// </summary>
        public string Role { get; set; }

        #endregion

        #region Security

        /// <summary>
        /// Gets or sets the stored password value for authentication-only projections.
        /// Management and registration results always leave this value empty.
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// Gets or sets whether authentication is permitted.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets a clear non-schema status for administrator display.
        /// </summary>
        public string ActivationStatus
        {
            get { return IsActive ? "Active" : "Pending Activation"; }
        }

        #endregion

        #region Audit Metadata

        /// <summary>
        /// Gets or sets the account creation time.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        #endregion
    }
}
