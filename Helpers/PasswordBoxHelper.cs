#region Namespaces

using System.Windows;
using System.Windows.Controls;

#endregion

namespace VisualInspectionTrainingSystem.Helpers
{
    /// <summary>
    /// Enables binding for PasswordBox.Password.
    /// </summary>
    public static class PasswordBoxHelper
    {
        #region Attached Properties

        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    OnBoundPasswordChanged));

        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BindPassword",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false, OnBindPasswordChanged));

        private static readonly DependencyProperty UpdatingPasswordProperty =
            DependencyProperty.RegisterAttached(
                "UpdatingPassword",
                typeof(bool),
                typeof(PasswordBoxHelper));

        #endregion

        #region Get / Set

        public static string GetBoundPassword(DependencyObject dp)
        {
            return (string)dp.GetValue(BoundPasswordProperty);
        }

        public static void SetBoundPassword(
            DependencyObject dp,
            string value)
        {
            dp.SetValue(BoundPasswordProperty, value);
        }

        public static bool GetBindPassword(DependencyObject dp)
        {
            return (bool)dp.GetValue(BindPasswordProperty);
        }

        public static void SetBindPassword(
            DependencyObject dp,
            bool value)
        {
            dp.SetValue(BindPasswordProperty, value);
        }

        private static bool GetUpdatingPassword(
            DependencyObject dp)
        {
            return (bool)dp.GetValue(UpdatingPasswordProperty);
        }

        private static void SetUpdatingPassword(
            DependencyObject dp,
            bool value)
        {
            dp.SetValue(UpdatingPasswordProperty, value);
        }

        #endregion

        #region Property Changed

        private static void OnBoundPasswordChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            PasswordBox passwordBox = d as PasswordBox;

            if (passwordBox == null)
                return;

            passwordBox.PasswordChanged -= HandlePasswordChanged;

            if (!GetUpdatingPassword(passwordBox))
            {
                passwordBox.Password = e.NewValue?.ToString() ?? string.Empty;
            }

            passwordBox.PasswordChanged += HandlePasswordChanged;
        }

        private static void OnBindPasswordChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            PasswordBox passwordBox = d as PasswordBox;

            if (passwordBox == null)
                return;

            bool wasBound = (bool)e.OldValue;
            bool needToBind = (bool)e.NewValue;

            if (wasBound)
            {
                passwordBox.PasswordChanged -= HandlePasswordChanged;
            }

            if (needToBind)
            {
                passwordBox.PasswordChanged += HandlePasswordChanged;
            }
        }

        #endregion

        #region Events

        private static void HandlePasswordChanged(
            object sender,
            RoutedEventArgs e)
        {
            PasswordBox passwordBox = sender as PasswordBox;

            if (passwordBox == null)
                return;

            SetUpdatingPassword(passwordBox, true);

            SetBoundPassword(passwordBox, passwordBox.Password);

            SetUpdatingPassword(passwordBox, false);
        }

        #endregion
    }
}