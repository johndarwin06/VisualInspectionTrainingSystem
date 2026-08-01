#region Namespaces

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem
{
    /// <summary>
    /// Provides application lifecycle coordination and process-wide WPF exception handling.
    /// </summary>
    public partial class App : Application
    {
        #region Constants

        private const string FatalErrorMessage =
            "An unexpected error occurred and the application must close. " +
            "Please restart the application. Contact support if the problem continues.";

        private const string FatalErrorTitle = "Application Error";

        private static readonly TimeSpan FatalLogFlushTimeout =
            TimeSpan.FromMilliseconds(750);

        private static readonly TimeSpan NormalLogShutdownTimeout =
            TimeSpan.FromSeconds(1);

        #endregion

        #region Fields

        private int _dispatcherFatalErrorHandling;
        private int _fatalShutdownRequested;
        private int _globalHandlersRegistered;

        #endregion

        #region Application Lifecycle

        /// <summary>
        /// Initializes logging and registers process-wide handlers before the first window opens.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            ApplicationErrorLogger.Initialize();
            RegisterGlobalExceptionHandlers();
            ApplicationThemeService.Current.UseLightTheme();

            ApplicationErrorLogger.LogInformation(
                "Application Lifecycle",
                "Application startup began.");

            base.OnStartup(e);
        }

        /// <summary>
        /// Records normal shutdown, flushes within a fixed bound, and releases global handlers.
        /// </summary>
        /// <param name="e">The exit event arguments.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            ApplicationErrorLogger.LogInformation(
                "Application Lifecycle",
                "Application shutdown completed normally.");

            ApplicationErrorLogger.Flush(NormalLogShutdownTimeout);
            UnregisterGlobalExceptionHandlers();
            ApplicationErrorLogger.Shutdown(NormalLogShutdownTimeout);

            base.OnExit(e);
        }

        #endregion

        #region Exception Handlers

        /// <summary>
        /// Logs one UI-thread exception, shows one non-sensitive notification, and requests controlled shutdown.
        /// </summary>
        /// <param name="sender">The dispatcher that raised the event.</param>
        /// <param name="e">The unhandled dispatcher exception information.</param>
        private void App_DispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            if (Interlocked.Exchange(
                    ref _dispatcherFatalErrorHandling,
                    1) != 0)
            {
                MarkDispatcherExceptionHandled(e);
                RequestSafeShutdown();

                return;
            }

            try
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "WPF Dispatcher",
                    e == null
                        ? null
                        : e.Exception,
                    true);

                ApplicationErrorLogger.Flush(FatalLogFlushTimeout);
                ShowFatalErrorMessage();
            }
            finally
            {
                MarkDispatcherExceptionHandled(e);
                RequestSafeShutdown();
            }
        }

        /// <summary>
        /// Logs and observes a faulted task that was never awaited by its owner.
        /// </summary>
        /// <param name="sender">The task scheduler that raised the event.</param>
        /// <param name="e">The unobserved task exception information.</param>
        private void TaskScheduler_UnobservedTaskException(
            object sender,
            UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "Task Scheduler",
                    e == null
                        ? null
                        : e.Exception,
                    false);
            }
            finally
            {
                if (e != null)
                {
                    e.SetObserved();
                }
            }
        }

        /// <summary>
        /// Logs a final AppDomain failure that cannot be recovered by WPF.
        /// </summary>
        /// <param name="sender">The current application domain.</param>
        /// <param name="e">The unhandled exception information.</param>
        private void CurrentDomain_UnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            bool isTerminating = e != null && e.IsTerminating;

            ApplicationErrorLogger.LogUnhandledException(
                isTerminating
                    ? "AppDomain Terminating"
                    : "AppDomain",
                e == null
                    ? null
                    : e.ExceptionObject as Exception,
                isTerminating);

            if (isTerminating)
            {
                ApplicationErrorLogger.Flush(FatalLogFlushTimeout);
            }
        }

        #endregion

        #region Handler Registration

        /// <summary>
        /// Registers application-wide handlers that are not declared in XAML.
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
            if (Interlocked.Exchange(
                    ref _globalHandlersRegistered,
                    1) != 0)
            {
                return;
            }

            TaskScheduler.UnobservedTaskException +=
                TaskScheduler_UnobservedTaskException;

            AppDomain.CurrentDomain.UnhandledException +=
                CurrentDomain_UnhandledException;
        }

        /// <summary>
        /// Removes application-wide handlers during normal process teardown.
        /// </summary>
        private void UnregisterGlobalExceptionHandlers()
        {
            if (Interlocked.Exchange(
                    ref _globalHandlersRegistered,
                    0) == 0)
            {
                return;
            }

            TaskScheduler.UnobservedTaskException -=
                TaskScheduler_UnobservedTaskException;

            AppDomain.CurrentDomain.UnhandledException -=
                CurrentDomain_UnhandledException;
        }

        #endregion

        #region Shutdown

        /// <summary>
        /// Displays a single generic failure notification without exposing exception details.
        /// </summary>
        private static void ShowFatalErrorMessage()
        {
            try
            {
                MessageBox.Show(
                    FatalErrorMessage,
                    FatalErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // A notification failure must not prevent safe shutdown.
            }
        }

        /// <summary>
        /// Marks the dispatcher failure handled only while controlled shutdown is being requested.
        /// </summary>
        /// <param name="e">The dispatcher exception information.</param>
        private static void MarkDispatcherExceptionHandled(
            DispatcherUnhandledExceptionEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            try
            {
                e.Handled = true;
            }
            catch
            {
                // The process is already in a fatal path; do not replace the original failure.
            }
        }

        /// <summary>
        /// Requests a single safe shutdown after a fatal UI-thread exception.
        /// </summary>
        private void RequestSafeShutdown()
        {
            if (Interlocked.Exchange(
                    ref _fatalShutdownRequested,
                    1) != 0)
            {
                return;
            }

            try
            {
                Shutdown(-1);
            }
            catch
            {
                // Exception handlers must not throw while the application is terminating.
            }
        }

        #endregion
    }
}
