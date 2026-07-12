#region Namespaces

using System.Windows;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Provides centralized window navigation.
    /// </summary>
    public static class NavigationService
    {
        #region Public Methods

        public static void Navigate(Window currentWindow, Window nextWindow)
        {
            if (nextWindow == null)
                return;

            nextWindow.Show();

            currentWindow?.Close();
        }

        public static void Show(Window window)
        {
            window?.Show();
        }

        public static void Close(Window window)
        {
            window?.Close();
        }

        #endregion
    }
}