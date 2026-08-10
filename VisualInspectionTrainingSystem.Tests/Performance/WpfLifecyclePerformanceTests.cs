#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.Tests.Infrastructure;
using VisualInspectionTrainingSystem.Views.Admin;
using VisualInspectionTrainingSystem.Views.Dashboard;
using VisualInspectionTrainingSystem.Views.History;
using VisualInspectionTrainingSystem.Views.Home;
using VisualInspectionTrainingSystem.Views.Login;
using VisualInspectionTrainingSystem.Views.Quiz;
using VisualInspectionTrainingSystem.Views.Reports;
using VisualInspectionTrainingSystem.Views.Result;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Performance
{
    /// <summary>
    /// Measures real Fluent resource and window lifecycle work on an STA Dispatcher.
    /// Database-backed refresh timing is intentionally measured by the isolated schema suite.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.Performance)]
    [NonParallelizable]
    [Apartment(ApartmentState.STA)]
    public sealed class WpfLifecyclePerformanceTests
    {
        #region Fields

        private App _application;
        private IDisposable _configurationScope;
        private string _temporaryDirectory;
        private double _coldResourceInitializationMilliseconds;

        #endregion

        #region Lifecycle

        /// <summary>Loads actual application resources against isolated unavailable services.</summary>
        [OneTimeSetUp]
        public void SetUpApplication()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "VITS-I18-WPF-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);

            string settingsFile = CreateIsolatedSettings();
            _configurationScope = ConfigurationService.UseSettingsFileForTesting(settingsFile);

            if (Application.ResourceAssembly == null)
                Application.ResourceAssembly = typeof(App).Assembly;

            Stopwatch stopwatch = Stopwatch.StartNew();
            _application = new App
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            _application.InitializeComponent();
            stopwatch.Stop();
            _coldResourceInitializationMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            // The first Dispatcher frame honors the real production StartupUri.
            // Close that independently tested splash before measuring explicit
            // authenticated workspace cycles in this same real App instance.
            PumpDispatcher(TimeSpan.FromMilliseconds(50));
            CloseAllWindows();
            bool startupWindowsClosed = PumpDispatcherUntil(
                delegate { return Application.Current.Windows.Count == 0; },
                TimeSpan.FromSeconds(2));
            Assert.That(
                startupWindowsClosed,
                Is.True,
                "The production startup surface did not close during fixture isolation.");
        }

        /// <summary>Closes windows, drains cancellation callbacks, and removes all test files.</summary>
        [OneTimeTearDown]
        public void TearDownApplication()
        {
            CloseAllWindows();
            SessionService.Logout();
            PumpDispatcher(TimeSpan.FromMilliseconds(400));

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

            if (Directory.Exists(_temporaryDirectory))
                Directory.Delete(_temporaryDirectory, true);

            Assert.That(Directory.Exists(_temporaryDirectory), Is.False);
        }

        #endregion

        #region Resource Construction

        /// <summary>
        /// Records single-process cold resource initialization and repeated warm lookups.
        /// The cold value is diagnostic; visible process launches qualify real startup.
        /// </summary>
        [Test]
        public void ApplicationResources_LoadAndResolveWarmSemanticKeys()
        {
            TestContext.Progress.WriteLine(
                "PERF|Startup.WpfResources.ColdDiagnostic|Samples=1|Milliseconds={0:0.000}",
                _coldResourceInitializationMilliseconds);

            object latest = null;
            PerformanceMeasurement.Measure(
                "Startup.WpfResources.WarmLookup",
                3,
                21,
                delegate
                {
                    latest = _application.TryFindResource("App.PrimaryBrush");
                });

            Assert.Multiple(delegate
            {
                Assert.That(_application.Resources.MergedDictionaries.Count, Is.GreaterThan(3));
                Assert.That(latest, Is.Not.Null);
            });
        }

        #endregion

        #region Window Lifecycle

        /// <summary>Measures unauthenticated Login and Registration surface cycles.</summary>
        [Test]
        public void AuthenticationWindows_RepeatedConstructionLeavesNoOpenWindow()
        {
            SessionService.Logout();

            try
            {
                MeasureWindow("Navigation.Login", () => new LoginWindow());
                MeasureWindow("Navigation.Registration", () => new RegistrationWindow());
            }
            finally
            {
                CloseAllWindows();
                SessionService.Logout();
                PumpDispatcher(TimeSpan.FromMilliseconds(300));
            }

            Assert.That(Application.Current.Windows.Count, Is.Zero);
        }

        /// <summary>Measures administrator shell and primary workspace open/close cycles.</summary>
        [Test]
        public void AdministratorWindows_RepeatedConstructionLeavesNoOpenWindow()
        {
            SessionService.Login(CreateUser(UserRoles.Admin));

            try
            {
                MeasureWindow(
                    "Navigation.AdminShell",
                    delegate
                    {
                        // Closing the production shell intentionally logs out. Each
                        // measured shell construction therefore establishes the same
                        // precondition as a real successful login.
                        SessionService.Login(CreateUser(UserRoles.Admin));
                        return new MainWindow();
                    });
                SessionService.Login(CreateUser(UserRoles.Admin));
                MeasureWindow("Navigation.ReviewWorkflow", () => new AdminWindow());
                MeasureWindow("Navigation.UserManagement", () => new UserManagementWindow());
                MeasureWindow("Navigation.Dashboard", () => new DashboardWindow());
                MeasureWindow("Navigation.Reports", () => new ReportsWindow());
            }
            finally
            {
                CloseAllWindows();
                SessionService.Logout();
                PumpDispatcher(TimeSpan.FromMilliseconds(500));
            }

            Assert.That(Application.Current.Windows.Count, Is.Zero);
        }

        /// <summary>Measures trainee shell, setup, and history open/close cycles.</summary>
        [Test]
        public void TraineeWindows_RepeatedConstructionLeavesNoOpenWindow()
        {
            SessionService.Login(CreateUser(UserRoles.User));

            try
            {
                MeasureWindow(
                    "Navigation.TraineeShell",
                    delegate
                    {
                        // MainWindow owns and clears the authenticated session when
                        // it closes, so repeated shell cycles must model login too.
                        SessionService.Login(CreateUser(UserRoles.User));
                        return new MainWindow();
                    });
                SessionService.Login(CreateUser(UserRoles.User));
                MeasureWindow("Navigation.TrainingSetup", () => new HomeWindow());
                MeasureWindow(
                    "Navigation.Quiz10",
                    () => new QuizWindow(ImageService.DefaultQuizSize));
                MeasureWindow(
                    "Navigation.Quiz20",
                    () => new QuizWindow(ImageService.ExtendedQuizSize));
                MeasureWindow(
                    "Navigation.Result20",
                    () => new ResultWindow(CreateResultAnswers(20)));
                MeasureWindow("Navigation.TrainingHistory", () => new TrainingHistoryWindow());
                MeasureWindow(
                    "Navigation.SessionDetail",
                    () => new TrainingHistoryDetailWindow(1));
            }
            finally
            {
                CloseAllWindows();
                SessionService.Logout();
                PumpDispatcher(TimeSpan.FromMilliseconds(500));
            }

            Assert.That(Application.Current.Windows.Count, Is.Zero);
        }

        private static void MeasureWindow(
            string name,
            Func<Window> factory)
        {
            int originalWindowCount = Application.Current.Windows.Count;
            long beforeBytes = GC.GetTotalMemory(false);

            PerformanceMeasurement.Measure(
                name,
                1,
                5,
                delegate
                {
                    Window window = factory();
                    window.Show();
                    PumpDispatcher(TimeSpan.FromMilliseconds(20));
                    window.Close();
                    bool removed = PumpDispatcherUntil(
                        delegate
                        {
                            return !Application.Current.Windows
                                .Cast<Window>()
                                .Contains(window);
                        },
                        TimeSpan.FromSeconds(1));
                    string remainingWindows = DescribeOpenWindows();

                    Assert.Multiple(delegate
                    {
                        Assert.That(removed, Is.True, "The closed window remained registered.");
                        Assert.That(window.DataContext, Is.Null);
                        Assert.That(
                            Application.Current.Windows.Count,
                            Is.EqualTo(originalWindowCount),
                            "Unexpected windows after close: " + remainingWindows);
                    });
                });

            // Forced collection is diagnostic only. WPF and the JIT can retain framework
            // allocations, so the value is reported rather than used as a timing gate.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long afterBytes = GC.GetTotalMemory(true);
            TestContext.Progress.WriteLine(
                "RESOURCE|{0}|ManagedDeltaBytes={1}",
                name,
                afterBytes - beforeBytes);

            Assert.That(Application.Current.Windows.Count, Is.EqualTo(originalWindowCount));
        }

        #endregion

        #region Fixtures

        private static List<QuizAnswer> CreateResultAnswers(int count)
        {
            List<QuizAnswer> answers = new List<QuizAnswer>(count);

            for (int index = 0; index < count; index++)
            {
                QuizAnswerType userAnswer = index % 2 == 0
                    ? QuizAnswerType.Good
                    : QuizAnswerType.Ng;
                QuizAnswerType? correctAnswer = index % 4 == 0
                    ? (QuizAnswerType?)null
                    : userAnswer;

                answers.Add(new QuizAnswer
                {
                    AnswerID = index + 1,
                    SessionID = 1,
                    Sequence = index + 1,
                    ImageID = index + 1,
                    ImageHash = index.ToString("x64"),
                    FileName = "performance-" + index + ".bmp",
                    UserAnswer = userAnswer,
                    CorrectAnswer = correctAnswer,
                    IsCorrect = correctAnswer.HasValue,
                    AnswerTime = DateTime.Now.AddSeconds(index)
                });
            }

            return answers;
        }

        private string CreateIsolatedSettings()
        {
            string images = Path.Combine(_temporaryDirectory, "Images");
            string logs = Path.Combine(_temporaryDirectory, "Logs");
            string exports = Path.Combine(_temporaryDirectory, "Exports");
            string reports = Path.Combine(_temporaryDirectory, "Reports");
            Directory.CreateDirectory(images);
            Directory.CreateDirectory(logs);
            Directory.CreateDirectory(exports);
            Directory.CreateDirectory(reports);
            CreateQuizFixtures(images, ImageService.ExtendedQuizSize);

            string settingsFile = Path.Combine(
                _temporaryDirectory,
                "DatabaseSettings.performance.config");

            new XDocument(
                new XElement(
                    "applicationSettings",
                    new XElement(
                        "mysql",
                        new XAttribute("server", "127.0.0.1"),
                        new XAttribute("port", "1"),
                        new XAttribute("database", "vits_performance_test"),
                        new XAttribute("username", "performance_runner"),
                        new XAttribute("password", "test-placeholder"),
                        new XAttribute("sslMode", "Disabled"),
                        new XAttribute("connectionTimeoutSeconds", "1"),
                        new XAttribute("retryCount", "0"),
                        new XAttribute("retryDelayMilliseconds", "0")),
                    new XElement(
                        "paths",
                        new XAttribute("quizImageFolder", images),
                        new XAttribute("logFolder", logs),
                        new XAttribute("exportFolder", exports),
                        new XAttribute("reportFolder", reports))))
                .Save(settingsFile);

            return settingsFile;
        }

        private static void CreateQuizFixtures(
            string directory,
            int count)
        {
            byte[] bitmap = CreateOnePixelBitmap();

            for (int index = 0; index < count; index++)
            {
                bitmap[54] = (byte)(index & 0xFF);
                bitmap[55] = (byte)((index >> 8) & 0xFF);
                bitmap[56] = (byte)((index >> 16) & 0xFF);

                File.WriteAllBytes(
                    Path.Combine(
                        directory,
                        "performance-" + index.ToString("D2") + ".bmp"),
                    bitmap);
            }
        }

        private static byte[] CreateOnePixelBitmap()
        {
            return new byte[]
            {
                0x42, 0x4D, 58, 0, 0, 0, 0, 0, 0, 0, 54, 0, 0, 0,
                40, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 24, 0,
                0, 0, 0, 0, 4, 0, 0, 0, 0x13, 0x0B, 0, 0, 0x13, 0x0B,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x20, 0x80, 0xE0, 0
            };
        }

        private static User CreateUser(string role)
        {
            return new User
            {
                UserID = string.Equals(role, UserRoles.Admin, StringComparison.Ordinal) ? 1 : 2,
                EmployeeNo = "PERF-SESSION",
                FullName = "Performance Session",
                Department = "Performance",
                Role = role,
                IsActive = true
            };
        }

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

        private static bool PumpDispatcherUntil(
            Func<bool> condition,
            TimeSpan timeout)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (!condition() && stopwatch.Elapsed < timeout)
            {
                PumpDispatcher(TimeSpan.FromMilliseconds(10));
            }

            return condition();
        }

        private static void CloseAllWindows()
        {
            if (Application.Current == null)
                return;

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
                }
            }
        }

        private static string DescribeOpenWindows()
        {
            if (Application.Current == null ||
                Application.Current.Windows.Count == 0)
            {
                return "None";
            }

            return string.Join(
                ",",
                Application.Current.Windows
                    .Cast<Window>()
                    .Select(
                        window => window.GetType().Name +
                                  "(Visible=" + window.IsVisible + ")"));
        }

        #endregion
    }
}
