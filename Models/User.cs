#region Namespaces

using System;

#endregion

namespace VisualInspectionTrainingSystem.Models
{
    /// <summary>
    /// Represents an application user.
    /// </summary>
    public class User
    {
        #region Properties

        public int UserID { get; set; }

        public string EmployeeNo { get; set; }

        public string FullName { get; set; }

        public string PasswordHash { get; set; }

        public string Role { get; set; }

        public string Department { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        #endregion
    }
} 