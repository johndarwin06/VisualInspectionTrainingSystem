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
    /// Provides application-wide WPF exception handling.
    /// </summary>
    public partial class App : Application
    {
        #region Fields

        private int _fatalShutdownRequested;

        #endregion

        #region Application Lifecycle

        /// <summary>
        /// Registers process-wide error handlers before the application opens its first window.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            RegisterGlobalExceptionHandlers();

            base.OnStartup(e);
        }

        /// <summary>
        /// Releases process-wide handler subscriptions during normal shutdown.
        /// </summary>
        /// <param name="e">The exit event arguments.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            UnregisterGlobalExceptionHandlers();

            base.OnExit(e);
        }

        #endregion

        #region Exception Handlers

        /// <summary>
        /// Logs a UI-thread exception and shuts down cleanly rather than leaving the application in an unknown state.
        /// </summary>
        /// <param name="sender">The dispatcher that raised the event.</param>
        /// <param name="e">The unhandled dispatcher exception information.</param>
        private void App_DispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            ApplicationErrorLogger.LogUnhandledException(
                "WPF Dispatcher",
                e == null
                    ? null
                    : e.Exception);

            if (e != null)
            {
                e.Handled = true;
            }

            RequestSafeShutdown();
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
            ApplicationErrorLogger.LogUnhandledException(
                "Task Scheduler",
                e == null
                    ? null
                    : e.Exception);

            if (e != null)
            {
                e.SetObserved();
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
            ApplicationErrorLogger.LogUnhandledException(
                e != null && e.IsTerminating
                    ? "AppDomain Terminating"
                    : "AppDomain",
                e == null
                    ? null
                    : e.ExceptionObject as Exception);
        }

        #endregion

        #region Handler Registration

        /// <summary>
        /// Registers application-wide handlers that are not declared in XAML.
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
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
            TaskScheduler.UnobservedTaskException -=
                TaskScheduler_UnobservedTaskException;

            AppDomain.CurrentDomain.UnhandledException -=
                CurrentDomain_UnhandledException;
        }

        #endregion

        #region Shutdown

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
                // Exception handlers must not throw while an application is terminating.
            }
        }

        #endregion
    }
}
