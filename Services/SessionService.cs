#region Namespaces

using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Stores the currently logged-in user.
    /// </summary>
    public static class SessionService
    {
        #region Properties

        public static User CurrentUser { get; private set; }

        public static bool IsLoggedIn
        {
            get
            {
                return CurrentUser != null;
            }
        }

        #endregion

        #region Methods

        public static void Login(User user)
        {
            CurrentUser = user;
        }

        public static void Logout()
        {
            CurrentUser = null;
        }

        #endregion
    }
}