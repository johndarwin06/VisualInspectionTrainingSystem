#region Namespaces

using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using VisualInspectionTrainingSystem.Views.Dialogs;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Presents one consistent application dialog while preserving standard WPF message-box results.
    /// </summary>
    public static class ApplicationDialogService
    {
        #region Constants

        private const string DefaultCaption =
            "Visual Inspection Training System";

        private const string DefaultMessage =
            "The operation could not be completed.";

        #endregion

        #region Fields

        private static int _isDialogOpen;
        private static ApplicationDialogWindow _activeDialog;

        #endregion

        #region Public API

        /// <summary>
        /// Shows a keyboard-accessible application dialog and returns the selected standard result.
        /// </summary>
        /// <param name="message">Non-sensitive user-facing message.</param>
        /// <param name="caption">Concise dialog title.</param>
        /// <param name="buttons">Standard button arrangement.</param>
        /// <param name="icon">Standard semantic icon.</param>
        /// <returns>The selected action, or <see cref="MessageBoxResult.None"/> for a suppressed duplicate.</returns>
        public static MessageBoxResult Show(
            string message,
            string caption,
            MessageBoxButton buttons,
            MessageBoxImage icon)
        {
            Application application = Application.Current;

            if (application == null || application.Dispatcher == null)
            {
                return ShowNativeFallback(
                    message,
                    caption,
                    buttons,
                    icon);
            }

            Dispatcher dispatcher = application.Dispatcher;

            if (!dispatcher.CheckAccess())
            {
                return dispatcher.Invoke(
                    new Func<MessageBoxResult>(
                        () => ShowCore(
                            message,
                            caption,
                            buttons,
                            icon)));
            }

            return ShowCore(
                message,
                caption,
                buttons,
                icon);
        }

        #endregion

        #region Dialog Coordination

        private static MessageBoxResult ShowCore(
            string message,
            string caption,
            MessageBoxButton buttons,
            MessageBoxImage icon)
        {
            if (Interlocked.CompareExchange(ref _isDialogOpen, 1, 0) != 0)
            {
                ApplicationDialogWindow existing = _activeDialog;

                if (existing != null)
                {
                    existing.Activate();
                    existing.Focus();
                }

                return MessageBoxResult.None;
            }

            try
            {
                ApplicationDialogWindow dialog =
                    new ApplicationDialogWindow(
                        NormalizeMessage(message),
                        NormalizeCaption(caption),
                        buttons,
                        icon);
                Window owner = FindOwner(dialog);

                if (owner != null)
                    dialog.Owner = owner;

                _activeDialog = dialog;
                return dialog.ShowModal();
            }
            catch (Exception ex)
            {
                ApplicationErrorLogger.LogUnhandledException(
                    "Application Dialog",
                    ex,
                    false);
                return ShowNativeFallback(
                    message,
                    caption,
                    buttons,
                    icon);
            }
            finally
            {
                _activeDialog = null;
                Interlocked.Exchange(ref _isDialogOpen, 0);
            }
        }

        private static Window FindOwner(Window dialog)
        {
            Application application = Application.Current;

            if (application == null)
                return null;

            return application.Windows
                .OfType<Window>()
                .FirstOrDefault(
                    window => window != dialog &&
                              window.IsVisible &&
                              window.IsActive);
        }

        #endregion

        #region Safe Fallback

        private static MessageBoxResult ShowNativeFallback(
            string message,
            string caption,
            MessageBoxButton buttons,
            MessageBoxImage icon)
        {
            try
            {
                return MessageBox.Show(
                    NormalizeMessage(message),
                    NormalizeCaption(caption),
                    buttons,
                    icon);
            }
            catch
            {
                return MessageBoxResult.None;
            }
        }

        private static string NormalizeMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? DefaultMessage
                : message.Trim();
        }

        private static string NormalizeCaption(string caption)
        {
            return string.IsNullOrWhiteSpace(caption)
                ? DefaultCaption
                : caption.Trim();
        }

        #endregion
    }
}
