#region Namespaces

using System.Windows;
using System.Windows.Controls;

#endregion

namespace VisualInspectionTrainingSystem.Controls.Progress
{
    /// <summary>
    /// Displays a lightweight, accessible busy overlay for an operation that is already in progress.
    /// </summary>
    public partial class ModernProgressBar : UserControl
    {
        #region Dependency Properties

        /// <summary>
        /// Identifies the <see cref="IsActive"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(
                "IsActive",
                typeof(bool),
                typeof(ModernProgressBar),
                new PropertyMetadata(false));

        /// <summary>
        /// Identifies the <see cref="Message"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                "Message",
                typeof(string),
                typeof(ModernProgressBar),
                new PropertyMetadata("Working..."));

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the shared busy overlay.
        /// </summary>
        public ModernProgressBar()
        {
            InitializeComponent();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether the overlay is visible and intercepts repeated input.
        /// </summary>
        public bool IsActive
        {
            get { return (bool)GetValue(IsActiveProperty); }
            set { SetValue(IsActiveProperty, value); }
        }

        /// <summary>
        /// Gets or sets the non-sensitive operation description announced to the user.
        /// </summary>
        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        #endregion
    }
}
