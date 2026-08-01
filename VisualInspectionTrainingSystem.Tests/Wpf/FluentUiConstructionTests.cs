#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using VisualInspectionTrainingSystem.Controls.Charts;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;
using VisualInspectionTrainingSystem.ViewModels;
using VisualInspectionTrainingSystem.Views.Admin;
using VisualInspectionTrainingSystem.Views.Dashboard;
using VisualInspectionTrainingSystem.Views.Dialogs;
using VisualInspectionTrainingSystem.Views.History;
using VisualInspectionTrainingSystem.Views.Home;
using VisualInspectionTrainingSystem.Views.Reports;
using VisualInspectionTrainingSystem.Views.Result;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Wpf
{
    /// <summary>
    /// Verifies Fluent resources, production workspace composition, XAML structure,
    /// controls, charts, and accessibility contracts on a real STA Dispatcher.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Wpf)]
    [NonParallelizable]
    [Apartment(ApartmentState.STA)]
    public sealed class FluentUiConstructionTests
    {
        #region Fields

        private App _application;
        private IDisposable _configurationScope;
        private string _temporaryRoot;

        #endregion

        #region Fixture Lifecycle

        /// <summary>
        /// Loads the real application resources and redirects all production
        /// repositories to isolated, unreachable test configuration.
        /// </summary>
        [OneTimeSetUp]
        public void SetUpApplication()
        {
            _temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "VitsWorkspaceComposition-" + Guid.NewGuid().ToString("N"));

            string imageFolder = Path.Combine(_temporaryRoot, "Images");
            string logFolder = Path.Combine(_temporaryRoot, "Logs");
            string exportFolder = Path.Combine(_temporaryRoot, "Exports");
            string reportFolder = Path.Combine(_temporaryRoot, "Reports");

            Directory.CreateDirectory(imageFolder);
            Directory.CreateDirectory(logFolder);
            Directory.CreateDirectory(exportFolder);
            Directory.CreateDirectory(reportFolder);

            string settingsFile = Path.Combine(
                _temporaryRoot,
                "DatabaseSettings.test.config");

            WriteIsolatedSettings(
                settingsFile,
                imageFolder,
                logFolder,
                exportFolder,
                reportFolder);

            _configurationScope =
                ConfigurationService.UseSettingsFileForTesting(settingsFile);
            ApplicationErrorLogger.ConfigureLogFolder(logFolder);

            if (Application.ResourceAssembly == null)
            {
                Application.ResourceAssembly = typeof(App).Assembly;
            }

            _application = new App
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            _application.InitializeComponent();
        }

        /// <summary>
        /// Closes every created window, releases the test configuration, and
        /// removes the isolated files after background cancellation is observed.
        /// </summary>
        [OneTimeTearDown]
        public void TearDownApplication()
        {
            CloseAllWindows();
            SessionService.Logout();
            PumpDispatcher(TimeSpan.FromMilliseconds(300));

            if (_application != null)
            {
                _application.Shutdown();
                _application = null;
            }

            if (_configurationScope != null)
            {
                _configurationScope.Dispose();
                _configurationScope = null;
            }

            DeleteTemporaryRoot();
            Assert.That(
                Directory.Exists(_temporaryRoot),
                Is.False,
                "The production-composition test left temporary artifacts behind.");
        }

        #endregion

        #region XAML Source Tests

        /// <summary>Confirms every production XAML document remains well-formed.</summary>
        [Test]
        public void ProductionXaml_AllDocumentsAreWellFormed()
        {
            List<string> xamlFiles = GetProductionXamlFiles().ToList();
            List<string> invalidFiles = new List<string>();

            foreach (string file in xamlFiles)
            {
                try
                {
                    XDocument.Load(file, LoadOptions.SetLineInfo);
                }
                catch
                {
                    invalidFiles.Add(Path.GetFileName(file));
                }
            }

            Assert.That(xamlFiles.Count, Is.GreaterThan(20));
            Assert.That(invalidFiles, Is.Empty);
        }

        /// <summary>Confirms the application resource order loads WPF-UI, Violeta, then custom theme resources.</summary>
        [Test]
        public void AppResources_PreserveFluentDictionaryOrder()
        {
            string source = File.ReadAllText(
                Path.Combine(GetRepositoryRoot(), "App.xaml"));

            int wpfUiTheme = source.IndexOf("<ui:ThemesDictionary", StringComparison.Ordinal);
            int wpfUiControls = source.IndexOf("<ui:ControlsDictionary", StringComparison.Ordinal);
            int violetaTheme = source.IndexOf("<vio:ThemesDictionary", StringComparison.Ordinal);
            int violetaControls = source.IndexOf("<vio:ControlsDictionary", StringComparison.Ordinal);
            int customTheme = source.IndexOf(
                "Source=\"/Resources/Themes/LightTheme.xaml\"",
                StringComparison.Ordinal);

            Assert.That(wpfUiTheme, Is.GreaterThanOrEqualTo(0));
            Assert.That(wpfUiControls, Is.GreaterThan(wpfUiTheme));
            Assert.That(violetaTheme, Is.GreaterThan(wpfUiControls));
            Assert.That(violetaControls, Is.GreaterThan(violetaTheme));
            Assert.That(customTheme, Is.GreaterThan(violetaControls));
        }

        /// <summary>Confirms the production presentation no longer references retired UI frameworks.</summary>
        [Test]
        public void ProductionXaml_HasNoRetiredFrameworkReferences()
        {
            string combined = string.Join(
                Environment.NewLine,
                GetProductionXamlFiles().Select(File.ReadAllText));

            Assert.That(combined, Does.Not.Contain("MahApps"));
            Assert.That(combined, Does.Not.Contain("MaterialDesign"));
            Assert.That(combined, Does.Not.Contain("ControlzEx"));
        }

        /// <summary>
        /// Confirms the Quiz keeps theme-aware Fluent surfaces, semantic choices,
        /// complete interaction states, progress, loading, and keyboard guidance.
        /// </summary>
        [Test]
        public void QuizWorkspace_PreservesFluentVisualAndInteractionContracts()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    GetRepositoryRoot(),
                    "Views",
                    "Quiz",
                    "QuizWindow.xaml"));

            Assert.That(
                source,
                Does.Contain("Background=\"{DynamicResource App.BackgroundBrush}\""));
            Assert.That(source, Does.Contain("GoodQuizButtonStyle"));
            Assert.That(source, Does.Contain("NgQuizButtonStyle"));
            Assert.That(source, Does.Contain("IsMouseOver"));
            Assert.That(source, Does.Contain("IsPressed"));
            Assert.That(source, Does.Contain("IsKeyboardFocused"));
            Assert.That(source, Does.Contain("IsEnabled"));
            Assert.That(source, Does.Contain("Command=\"{Binding GoodCommand}\""));
            Assert.That(source, Does.Contain("Command=\"{Binding NgCommand}\""));
            Assert.That(source, Does.Contain("Value=\"{Binding CompletionPercentage"));
            Assert.That(source, Does.Contain("IsActive=\"{Binding IsImageLoading}\""));
            Assert.That(source, Does.Contain("G  &#x2022;  GOOD"));
            Assert.That(source, Does.Contain("N  &#x2022;  NG"));
            Assert.That(source, Does.Contain("ESC  &#x2022;  Exit safely"));
        }

        /// <summary>Confirms data-heavy screens preserve recycling virtualization.</summary>
        [TestCase("Views/Admin/AdminWindow.xaml")]
        [TestCase("Views/Admin/UserManagementWindow.xaml")]
        [TestCase("Views/History/TrainingHistoryWindow.xaml")]
        [TestCase("Views/History/TrainingHistoryDetailWindow.xaml")]
        [TestCase("Views/Reports/ReportsWindow.xaml")]
        [TestCase("Views/Result/ResultWindow.xaml")]
        public void DataHeavyView_PreservesVirtualizationContract(string relativePath)
        {
            string source = File.ReadAllText(
                Path.Combine(
                    GetRepositoryRoot(),
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string globalStyles = File.ReadAllText(
                Path.Combine(
                    GetRepositoryRoot(),
                    "Resources",
                    "Styles",
                    "ApplicationStyles.xaml"));

            bool declaresDataGrid = source.IndexOf(
                "<DataGrid",
                StringComparison.Ordinal) >= 0;
            bool localVirtualization = source.IndexOf(
                "VirtualizingPanel.VirtualizationMode=\"Recycling\"",
                StringComparison.Ordinal) >= 0;
            bool globalVirtualization = globalStyles.IndexOf(
                "VirtualizingPanel.VirtualizationMode\" Value=\"Recycling\"",
                StringComparison.Ordinal) >= 0;

            Assert.That(declaresDataGrid, Is.True);
            Assert.That(localVirtualization || globalVirtualization, Is.True);
        }

        /// <summary>Confirms primary resizable windows preserve minimum size and keyboard-cycle metadata.</summary>
        [TestCase("MainWindow.xaml")]
        [TestCase("Views/Admin/AdminWindow.xaml")]
        [TestCase("Views/Admin/UserManagementWindow.xaml")]
        [TestCase("Views/Dashboard/DashboardWindow.xaml")]
        [TestCase("Views/Home/HomeWindow.xaml")]
        [TestCase("Views/History/TrainingHistoryWindow.xaml")]
        [TestCase("Views/Login/LoginWindow.xaml")]
        [TestCase("Views/Quiz/QuizWindow.xaml")]
        [TestCase("Views/Reports/ReportsWindow.xaml")]
        [TestCase("Views/Result/ResultWindow.xaml")]
        public void ResizableWindow_PreservesSizingAndKeyboardContracts(string relativePath)
        {
            string source = File.ReadAllText(
                Path.Combine(
                    GetRepositoryRoot(),
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));

            Assert.That(source, Does.Contain("MinWidth="));
            Assert.That(source, Does.Contain("MinHeight="));
            Assert.That(source, Does.Contain("KeyboardNavigation.TabNavigation=\"Cycle\""));
        }

        #endregion

        #region Runtime Construction Tests

        /// <summary>Constructs real Fluent resources, common controls, chart controls, and both themes on STA.</summary>
        [Test]
        public void FluentResources_ControlsAndChartsConstructOnSta()
        {
            Button button = new Button();
            TextBox textBox = new TextBox();
            ComboBox comboBox = new ComboBox();
            DatePicker datePicker = new DatePicker();
            DataGrid dataGrid = new DataGrid();
            AnalyticsChartsPanel charts = new AnalyticsChartsPanel();
            bool darkApplied = ApplicationThemeService.Current.UseDarkTheme();
            bool lightApplied = ApplicationThemeService.Current.UseLightTheme();

            Assert.That(_application.Resources.MergedDictionaries.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(button, Is.Not.Null);
            Assert.That(textBox, Is.Not.Null);
            Assert.That(comboBox, Is.Not.Null);
            Assert.That(datePicker, Is.Not.Null);
            Assert.That(dataGrid, Is.Not.Null);
            Assert.That(charts, Is.Not.Null);
            Assert.That(darkApplied, Is.True);
            Assert.That(lightApplied, Is.True);
            Assert.That(ApplicationThemeService.Current.IsDarkTheme, Is.False);
        }

        /// <summary>
        /// Exercises both role shells through real production navigation factories,
        /// actual resources, real ViewModels and repositories, and a WPF Dispatcher.
        /// </summary>
        [Test]
        public void ProductionComposition_AuthorizedWorkspacesOpenAndUnauthorizedRoutesStayBlocked()
        {
            VerifyAdministratorComposition();
            VerifyTraineeComposition();

            Assert.That(
                FindNavigationFailureDialogs(),
                Is.Empty,
                "An authorized production workspace displayed Workspace Unavailable.");
        }

        /// <summary>
        /// Verifies both trainee Back buttons close the owned workspace, restore
        /// the same shell, and remain single-flight under repeated navigation.
        /// </summary>
        [Test]
        public void TraineeWorkspaceBackActions_ReturnToExistingShellWithoutDuplicates()
        {
            SessionService.Login(CreateControlledUser(UserRoles.User));

            MainWindow shell = new MainWindow();

            try
            {
                shell.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(100));

                MainShellViewModel viewModel =
                    AssertShellAuthorization(shell, false);

                AssertBackNavigation<HomeWindow>(
                    shell,
                    viewModel.TrainingCommand);
                AssertBackNavigation<TrainingHistoryWindow>(
                    shell,
                    viewModel.HistoryCommand);
            }
            finally
            {
                shell.Close();
                PumpDispatcher(TimeSpan.FromMilliseconds(100));
                SessionService.Logout();
            }
        }

        #endregion

        #region Production Composition

        private void VerifyAdministratorComposition()
        {
            SessionService.Login(CreateControlledUser(UserRoles.Admin));

            MainWindow shell = new MainWindow();

            try
            {
                shell.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(100));

                MainShellViewModel viewModel =
                    AssertShellAuthorization(shell, true);

                AssertAuthorizedRoute<AdminWindow>(
                    shell,
                    viewModel.ReviewWorkflowCommand);
                AssertAuthorizedRoute<UserManagementWindow>(
                    shell,
                    viewModel.UserManagementCommand);
                AssertAuthorizedRoute<DashboardWindow>(
                    shell,
                    viewModel.DashboardCommand);
                AssertAuthorizedRoute<ReportsWindow>(
                    shell,
                    viewModel.ReportsCommand);
            }
            finally
            {
                shell.Close();
                PumpDispatcher(TimeSpan.FromMilliseconds(100));
                SessionService.Logout();
            }
        }

        private void VerifyTraineeComposition()
        {
            SessionService.Login(CreateControlledUser(UserRoles.User));

            MainWindow shell = new MainWindow();

            try
            {
                shell.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(100));

                MainShellViewModel viewModel =
                    AssertShellAuthorization(shell, false);

                AssertAuthorizedRoute<HomeWindow>(
                    shell,
                    viewModel.TrainingCommand);
                AssertAuthorizedRoute<TrainingHistoryWindow>(
                    shell,
                    viewModel.HistoryCommand);

                AssertDirectWorkspace<TrainingHistoryDetailWindow>(
                    shell,
                    () => new TrainingHistoryDetailWindow(int.MaxValue));
                AssertDirectWorkspace<ResultWindow>(
                    shell,
                    () => new ResultWindow());
            }
            finally
            {
                shell.Close();
                PumpDispatcher(TimeSpan.FromMilliseconds(100));
                SessionService.Logout();
            }
        }

        private static MainShellViewModel AssertShellAuthorization(
            MainWindow shell,
            bool administrator)
        {
            MainShellViewModel viewModel = shell.DataContext as MainShellViewModel;

            Assert.That(viewModel, Is.Not.Null);
            Assert.That(viewModel.IsAdministrator, Is.EqualTo(administrator));
            Assert.That(viewModel.IsTrainee, Is.EqualTo(!administrator));
            Assert.That(
                viewModel.ReviewWorkflowCommand.CanExecute(null),
                Is.EqualTo(administrator));
            Assert.That(
                viewModel.DashboardCommand.CanExecute(null),
                Is.EqualTo(administrator));
            Assert.That(
                viewModel.ReportsCommand.CanExecute(null),
                Is.EqualTo(administrator));
            Assert.That(
                viewModel.UserManagementCommand.CanExecute(null),
                Is.EqualTo(administrator));
            Assert.That(
                viewModel.TrainingCommand.CanExecute(null),
                Is.EqualTo(!administrator));
            Assert.That(
                viewModel.HistoryCommand.CanExecute(null),
                Is.EqualTo(!administrator));

            return viewModel;
        }

        private void AssertAuthorizedRoute<TWindow>(
            MainWindow shell,
            ICommand command)
            where TWindow : Window
        {
            bool navigationFailureObserved = false;
            DispatcherTimer dialogGuard = CreateDialogGuard(
                () => navigationFailureObserved = true);

            try
            {
                Assert.That(command.CanExecute(null), Is.True);
                dialogGuard.Start();
                command.Execute(null);
                PumpDispatcher(TimeSpan.FromMilliseconds(150));
            }
            finally
            {
                dialogGuard.Stop();
            }

            List<TWindow> workspaces = Application.Current.Windows
                .OfType<TWindow>()
                .Where(window => window.IsVisible)
                .ToList();

            Assert.That(
                navigationFailureObserved,
                Is.False,
                GetLatestTestLogSummary());
            Assert.That(workspaces.Count, Is.EqualTo(1));

            TWindow workspace = workspaces[0];
            AssertProductionWindow(workspace);

            command.Execute(null);
            PumpDispatcher(TimeSpan.FromMilliseconds(50));

            Assert.That(
                Application.Current.Windows
                    .OfType<TWindow>()
                    .Count(window => window.IsVisible),
                Is.EqualTo(1),
                "Repeated navigation created a duplicate production workspace.");

            workspace.Close();
            PumpDispatcher(TimeSpan.FromMilliseconds(150));

            Assert.That(shell.IsEnabled, Is.True);
        }

        private static void AssertBackNavigation<TWindow>(
            MainWindow shell,
            ICommand command)
            where TWindow : Window
        {
            Assert.That(command.CanExecute(null), Is.True);

            command.Execute(null);
            command.Execute(null);
            PumpDispatcher(TimeSpan.FromMilliseconds(150));

            List<TWindow> workspaces = Application.Current.Windows
                .OfType<TWindow>()
                .Where(window => window.IsVisible)
                .ToList();

            Assert.That(
                workspaces.Count,
                Is.EqualTo(1),
                "Repeated trainee navigation created a duplicate workspace.");

            TWindow workspace = workspaces[0];
            Button backButton = workspace.FindName("BackButton") as Button;

            AssertProductionWindow(workspace);
            Assert.That(backButton, Is.Not.Null);
            Assert.That(backButton.IsEnabled, Is.True);
            Assert.That(backButton.IsTabStop, Is.True);
            Assert.That(
                System.Windows.Automation.AutomationProperties.GetName(backButton),
                Is.EqualTo("Back to trainee home"));

            backButton.RaiseEvent(
                new RoutedEventArgs(Button.ClickEvent, backButton));
            PumpDispatcher(TimeSpan.FromMilliseconds(150));

            Assert.That(
                Application.Current.Windows
                    .OfType<TWindow>()
                    .Count(window => window.IsVisible),
                Is.Zero);
            Assert.That(shell.IsVisible, Is.True);
            Assert.That(shell.IsEnabled, Is.True);
        }

        private static void AssertDirectWorkspace<TWindow>(
            MainWindow shell,
            Func<TWindow> factory)
            where TWindow : Window
        {
            TWindow workspace = null;

            try
            {
                workspace = factory();
                workspace.Owner = shell;
                workspace.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(150));
                AssertProductionWindow(workspace);
            }
            finally
            {
                if (workspace != null)
                {
                    workspace.Close();
                    PumpDispatcher(TimeSpan.FromMilliseconds(100));
                }
            }
        }

        private static void AssertProductionWindow(Window workspace)
        {
            PropertyInfo titleBarProperty = workspace.GetType().GetProperty(
                "ExtendsContentIntoTitleBar",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(workspace.IsLoaded, Is.True);
            Assert.That(workspace.IsVisible, Is.True);
            Assert.That(workspace.DataContext, Is.Not.Null);
            Assert.That(titleBarProperty, Is.Not.Null);
            Assert.That(
                titleBarProperty.GetValue(workspace, null),
                Is.EqualTo(true));
            Assert.That(FindNavigationFailureDialogs(), Is.Empty);
        }

        private static DispatcherTimer CreateDialogGuard(Action onFailure)
        {
            DispatcherTimer timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(25),
                DispatcherPriority.Send,
                delegate
                {
                    foreach (ApplicationDialogWindow dialog in
                        FindNavigationFailureDialogs())
                    {
                        onFailure();
                        dialog.Close();
                    }
                },
                Dispatcher.CurrentDispatcher);

            return timer;
        }

        private static List<ApplicationDialogWindow> FindNavigationFailureDialogs()
        {
            if (Application.Current == null)
            {
                return new List<ApplicationDialogWindow>();
            }

            return Application.Current.Windows
                .OfType<ApplicationDialogWindow>()
                .Where(dialog => string.Equals(
                    dialog.Title,
                    "Workspace Unavailable",
                    StringComparison.Ordinal))
                .ToList();
        }

        private string GetLatestTestLogSummary()
        {
            string logFolder = Path.Combine(_temporaryRoot, "Logs");
            string logFile = Directory.Exists(logFolder)
                ? Directory.EnumerateFiles(
                        logFolder,
                        "application-errors-*.log",
                        SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;

            if (string.IsNullOrWhiteSpace(logFile))
            {
                return "No isolated technical diagnostic was written.";
            }

            string[] diagnosticLines = File.ReadAllLines(logFile)
                .Where(line =>
                    line.StartsWith("Source:", StringComparison.Ordinal) ||
                    line.StartsWith("ExceptionType:", StringComparison.Ordinal) ||
                    line.StartsWith("ExceptionMessage:", StringComparison.Ordinal))
                .Reverse()
                .Take(3)
                .Reverse()
                .ToArray();

            return string.Join(Environment.NewLine, diagnosticLines)
                .Replace(_temporaryRoot, "[temporary path]");
        }

        #endregion

        #region Test Configuration

        private static User CreateControlledUser(string role)
        {
            return new User
            {
                UserID = role == UserRoles.Admin ? -101 : -102,
                EmployeeNo = role == UserRoles.Admin
                    ? "TEST-ADMIN"
                    : "TEST-TRAINEE",
                FullName = "Controlled Test User",
                Department = "Regression Testing",
                Role = role,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };
        }

        private static void WriteIsolatedSettings(
            string settingsFile,
            string imageFolder,
            string logFolder,
            string exportFolder,
            string reportFolder)
        {
            XDocument document = new XDocument(
                new XElement(
                    "applicationSettings",
                    new XElement(
                        "mysql",
                        new XAttribute("server", "127.0.0.1"),
                        new XAttribute("port", "1"),
                        new XAttribute("database", "vits_regression_test"),
                        new XAttribute("username", "test_runner"),
                        new XAttribute("password", "not-a-secret"),
                        new XAttribute("sslMode", "Disabled"),
                        new XAttribute("connectionTimeoutSeconds", "1"),
                        new XAttribute("retryCount", "0"),
                        new XAttribute("retryDelayMilliseconds", "0")),
                    new XElement(
                        "paths",
                        new XAttribute("quizImageFolder", imageFolder),
                        new XAttribute("logFolder", logFolder),
                        new XAttribute("exportFolder", exportFolder),
                        new XAttribute("reportFolder", reportFolder))));

            document.Save(settingsFile);
        }

        private void DeleteTemporaryRoot()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(_temporaryRoot))
                    {
                        Directory.Delete(_temporaryRoot, true);
                    }

                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(50);
                }
            }
        }

        #endregion

        #region Dispatcher and File Helpers

        private static void PumpDispatcher(TimeSpan duration)
        {
            DispatcherFrame frame = new DispatcherFrame();
            DispatcherTimer timer = new DispatcherTimer(
                duration,
                DispatcherPriority.ApplicationIdle,
                delegate
                {
                    frame.Continue = false;
                },
                Dispatcher.CurrentDispatcher);

            timer.Start();
            Dispatcher.PushFrame(frame);
            timer.Stop();
        }

        private static void CloseAllWindows()
        {
            if (Application.Current == null)
            {
                return;
            }

            Window[] windows = Application.Current.Windows
                .Cast<Window>()
                .ToArray();

            foreach (Window window in windows)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                    // Teardown continues so no subsequent test inherits a window.
                }
            }
        }

        private static IEnumerable<string> GetProductionXamlFiles()
        {
            string root = GetRepositoryRoot();

            return Directory.EnumerateFiles(
                    root,
                    "*.xaml",
                    SearchOption.AllDirectories)
                .Where(file => file.IndexOf(
                    Path.DirectorySeparatorChar +
                    "VisualInspectionTrainingSystem.Tests" +
                    Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) < 0)
                .Where(file => file.IndexOf(
                    Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) < 0)
                .Where(file => file.IndexOf(
                    Path.DirectorySeparatorChar + "packages" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) < 0);
        }

        private static string GetRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);

            while (directory != null)
            {
                if (File.Exists(Path.Combine(
                        directory.FullName,
                        "VisualInspectionTrainingSystem.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "The repository root could not be located from the test output.");
        }

        #endregion
    }
}
