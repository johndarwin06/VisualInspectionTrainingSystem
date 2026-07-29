#region Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Coordinates the single application theme resource dictionary without configuration discovery.
    /// </summary>
    public sealed class ApplicationThemeService
    {
        #region Constants

        private const string LightThemePath =
            "/Resources/Themes/LightTheme.xaml";

        private const string DarkThemePath =
            "/Resources/Themes/DarkTheme.xaml";

        private const string ThemeFailureSource =
            "Application Theme";

        #endregion

        #region Fields

        private static readonly Lazy<ApplicationThemeService> CurrentService =
            new Lazy<ApplicationThemeService>(
                () => new ApplicationThemeService(),
                true);

        /// <summary>
        /// Keeps Fluent controls aligned with the established application Indigo brand.
        /// </summary>
        private static readonly Color BrandAccentColor =
            Color.FromRgb(0x43, 0x58, 0xC7);

        private volatile bool _isDarkTheme;

        #endregion

        #region Constructors

        private ApplicationThemeService()
        {
            _isDarkTheme = DetectCurrentThemeWithoutDispatch();
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs after the application theme dictionary and theme state change successfully.
        /// </summary>
        public event EventHandler ThemeChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the process-wide application theme coordinator.
        /// </summary>
        public static ApplicationThemeService Current
        {
            get { return CurrentService.Value; }
        }

        /// <summary>
        /// Gets whether the successfully applied application theme is dark.
        /// </summary>
        public bool IsDarkTheme
        {
            get { return _isDarkTheme; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the requested embedded application theme or schedules it on the UI dispatcher.
        /// </summary>
        /// <param name="isDarkTheme">True for the dark theme; false for the light theme.</param>
        /// <returns>True when the change was applied or safely queued for the UI thread.</returns>
        public bool SetTheme(bool isDarkTheme)
        {
            try
            {
                Application application = Application.Current;

                if (application == null || application.Dispatcher == null)
                {
                    return false;
                }

                if (!application.Dispatcher.CheckAccess())
                {
                    application.Dispatcher.BeginInvoke(
                        DispatcherPriority.Normal,
                        new Action(() => ApplyThemeOnUiThread(
                            application,
                            isDarkTheme)));
                    return true;
                }

                return ApplyThemeOnUiThread(application, isDarkTheme);
            }
            catch (Exception ex)
            {
                LogFailure(ex);
                return false;
            }
        }

        /// <summary>
        /// Applies or queues the existing light application theme.
        /// </summary>
        /// <returns>True when the change was applied or safely queued.</returns>
        public bool UseLightTheme()
        {
            return SetTheme(false);
        }

        /// <summary>
        /// Applies or queues the existing dark application theme.
        /// </summary>
        /// <returns>True when the change was applied or safely queued.</returns>
        public bool UseDarkTheme()
        {
            return SetTheme(true);
        }

        #endregion

        #region Theme Application

        private bool ApplyThemeOnUiThread(
            Application application,
            bool isDarkTheme)
        {
            try
            {
                string requestedPath = isDarkTheme
                    ? DarkThemePath
                    : LightThemePath;
                IList<ResourceDictionary> themeDictionaries = application
                    .Resources
                    .MergedDictionaries
                    .Where(IsApplicationThemeDictionary)
                    .ToList();
                ResourceDictionary requestedDictionary = themeDictionaries
                    .FirstOrDefault(dictionary =>
                        HasSource(dictionary, requestedPath));
                bool themeChanged = _isDarkTheme != isDarkTheme;

                if (requestedDictionary == null)
                {
                    requestedDictionary = new ResourceDictionary
                    {
                        Source = new Uri(
                            requestedPath,
                            UriKind.Relative)
                    };
                }

                int insertionIndex = GetThemeInsertionIndex(
                    application,
                    themeDictionaries);

                if (!application.Resources.MergedDictionaries.Contains(
                        requestedDictionary))
                {
                    application.Resources.MergedDictionaries.Insert(
                        insertionIndex,
                        requestedDictionary);
                }

                foreach (ResourceDictionary dictionary in themeDictionaries)
                {
                    if (!ReferenceEquals(dictionary, requestedDictionary))
                    {
                        application.Resources.MergedDictionaries.Remove(
                            dictionary);
                    }
                }

                MoveThemeToInsertionIndex(
                    application,
                    requestedDictionary,
                    insertionIndex);

                ApplyFluentTheme(isDarkTheme);
                _isDarkTheme = isDarkTheme;

                if (themeChanged)
                {
                    RaiseThemeChangedSafely();
                }

                return true;
            }
            catch (Exception ex)
            {
                LogFailure(ex);
                return false;
            }
        }

        private static int GetThemeInsertionIndex(
            Application application,
            IList<ResourceDictionary> themeDictionaries)
        {
            if (themeDictionaries.Count == 0)
            {
                return 0;
            }

            int index = application.Resources.MergedDictionaries.IndexOf(
                themeDictionaries[0]);

            return index < 0 ? 0 : index;
        }

        private static void MoveThemeToInsertionIndex(
            Application application,
            ResourceDictionary requestedDictionary,
            int insertionIndex)
        {
            int currentIndex = application.Resources.MergedDictionaries.IndexOf(
                requestedDictionary);
            int boundedIndex = Math.Max(
                0,
                Math.Min(
                    insertionIndex,
                    application.Resources.MergedDictionaries.Count - 1));

            if (currentIndex == boundedIndex)
            {
                return;
            }

            application.Resources.MergedDictionaries.Remove(
                requestedDictionary);
            application.Resources.MergedDictionaries.Insert(
                boundedIndex,
                requestedDictionary);
        }

        /// <summary>
        /// Keeps WPF-UI, Violeta, and custom application resources on the same theme.
        /// </summary>
        /// <param name="isDarkTheme">True when the dark palette is requested.</param>
        private static void ApplyFluentTheme(bool isDarkTheme)
        {
            ApplicationTheme theme = isDarkTheme
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;

            ApplicationThemeManager.Apply(
                theme,
                WindowBackdropType.Mica,
                false);
            ApplicationAccentColorManager.Apply(
                BrandAccentColor,
                theme,
                false,
                false);
        }

        #endregion

        #region Theme Detection

        private static bool DetectCurrentThemeWithoutDispatch()
        {
            try
            {
                Application application = Application.Current;

                if (application == null ||
                    application.Dispatcher == null ||
                    !application.Dispatcher.CheckAccess())
                {
                    return false;
                }

                return application.Resources.MergedDictionaries.Any(
                    dictionary => HasSource(dictionary, DarkThemePath));
            }
            catch (Exception ex)
            {
                LogFailure(ex);
                return false;
            }
        }

        private static bool IsApplicationThemeDictionary(
            ResourceDictionary dictionary)
        {
            return HasSource(dictionary, LightThemePath) ||
                   HasSource(dictionary, DarkThemePath);
        }

        private static bool HasSource(
            ResourceDictionary dictionary,
            string expectedPath)
        {
            if (dictionary == null || dictionary.Source == null)
            {
                return false;
            }

            string source = dictionary.Source.OriginalString
                .Replace('\\', '/');

            return source.EndsWith(
                expectedPath,
                StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Event Handling

        private void RaiseThemeChangedSafely()
        {
            EventHandler handlers = ThemeChanged;

            if (handlers == null)
            {
                return;
            }

            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    LogFailure(ex);
                }
            }
        }

        #endregion

        #region Diagnostics

        private static void LogFailure(Exception exception)
        {
            ApplicationErrorLogger.LogUnhandledException(
                ThemeFailureSource,
                exception,
                false);
        }

        #endregion
    }
}
