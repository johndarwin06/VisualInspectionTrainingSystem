#region Namespaces

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

#endregion

namespace VisualInspectionTrainingSystem.Views.Dialogs
{
    /// <summary>
    /// Hosts the consistent Fluent application confirmation and notification surface.
    /// </summary>
    public partial class ApplicationDialogWindow : FluentWindow
    {
        #region Fields

        private readonly MessageBoxButton _buttons;
        private MessageBoxResult _result;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a dialog with standard WPF button and icon semantics.
        /// </summary>
        /// <param name="message">Non-sensitive user-facing message.</param>
        /// <param name="caption">Dialog heading.</param>
        /// <param name="buttons">Standard action arrangement.</param>
        /// <param name="icon">Semantic dialog icon.</param>
        public ApplicationDialogWindow(
            string message,
            string caption,
            MessageBoxButton buttons,
            MessageBoxImage icon)
        {
            InitializeComponent();
            _buttons = buttons;
            _result = MessageBoxResult.None;
            Title = caption;
            CaptionText.Text = caption;
            ConfigureIcon(icon);
            ConfigureButtons(buttons);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Shows this window modally and returns the selected standard result.
        /// </summary>
        /// <returns>The selected button result or the safe close result.</returns>
        public MessageBoxResult ShowModal()
        {
            ShowDialog();
            return _result == MessageBoxResult.None
                ? GetSafeCloseResult(_buttons)
                : _result;
        }

        #endregion

        #region Configuration

        private void ConfigureIcon(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Error:
                    DialogIcon.Symbol = SymbolRegular.ErrorCircle24;
                    DialogIcon.Foreground = FindBrush("App.DangerBrush");
                    break;

                case MessageBoxImage.Warning:
                    DialogIcon.Symbol = SymbolRegular.Warning24;
                    DialogIcon.Foreground = FindBrush("App.WarningBrush");
                    break;

                case MessageBoxImage.Question:
                    DialogIcon.Symbol = SymbolRegular.QuestionCircle24;
                    DialogIcon.Foreground = FindBrush("App.AccentBrush");
                    break;

                default:
                    DialogIcon.Symbol = SymbolRegular.Info24;
                    DialogIcon.Foreground = FindBrush("App.AccentBrush");
                    break;
            }
        }

        private void ConfigureButtons(MessageBoxButton buttons)
        {
            TertiaryButton.Visibility = Visibility.Collapsed;
            SecondaryButton.Visibility = Visibility.Collapsed;

            switch (buttons)
            {
                case MessageBoxButton.OKCancel:
                    ConfigureButton(PrimaryButton, "OK", MessageBoxResult.OK, "Confirm");
                    ConfigureButton(SecondaryButton, "Cancel", MessageBoxResult.Cancel, "Cancel");
                    SecondaryButton.Visibility = Visibility.Visible;
                    SecondaryButton.IsCancel = true;
                    break;

                case MessageBoxButton.YesNo:
                    ConfigureButton(PrimaryButton, "Yes", MessageBoxResult.Yes, "Yes");
                    ConfigureButton(SecondaryButton, "No", MessageBoxResult.No, "No");
                    SecondaryButton.Visibility = Visibility.Visible;
                    SecondaryButton.IsCancel = true;
                    break;

                case MessageBoxButton.YesNoCancel:
                    ConfigureButton(PrimaryButton, "Yes", MessageBoxResult.Yes, "Yes");
                    ConfigureButton(SecondaryButton, "No", MessageBoxResult.No, "No");
                    ConfigureButton(TertiaryButton, "Cancel", MessageBoxResult.Cancel, "Cancel");
                    SecondaryButton.Visibility = Visibility.Visible;
                    TertiaryButton.Visibility = Visibility.Visible;
                    TertiaryButton.IsCancel = true;
                    break;

                default:
                    ConfigureButton(PrimaryButton, "OK", MessageBoxResult.OK, "OK");
                    PrimaryButton.IsCancel = true;
                    break;
            }
        }

        private static void ConfigureButton(
            Button button,
            string text,
            MessageBoxResult result,
            string accessibleName)
        {
            button.Content = text;
            button.Tag = result;
            System.Windows.Automation.AutomationProperties.SetName(
                button,
                accessibleName);
        }

        private Brush FindBrush(string key)
        {
            return TryFindResource(key) as Brush ?? Brushes.DodgerBlue;
        }

        #endregion

        #region Interaction

        private void OnResultButtonClick(
            object sender,
            RoutedEventArgs eventArgs)
        {
            Button button = sender as Button;

            if (button != null && button.Tag is MessageBoxResult)
                _result = (MessageBoxResult)button.Tag;

            Close();
        }

        private void OnPreviewKeyDown(
            object sender,
            KeyEventArgs eventArgs)
        {
            if (eventArgs.Key != Key.Escape)
                return;

            _result = GetSafeCloseResult(_buttons);
            eventArgs.Handled = true;
            Close();
        }

        private static MessageBoxResult GetSafeCloseResult(
            MessageBoxButton buttons)
        {
            switch (buttons)
            {
                case MessageBoxButton.YesNo:
                    return MessageBoxResult.No;

                case MessageBoxButton.OK:
                    return MessageBoxResult.OK;

                default:
                    return MessageBoxResult.Cancel;
            }
        }

        #endregion
    }
}
