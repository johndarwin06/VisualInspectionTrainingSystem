#region Namespaces

using FluentWindow = Wpf.Ui.Controls.FluentWindow;

#endregion

namespace VisualInspectionTrainingSystem.Views
{
    /// <summary>
    /// Displays the shared Fluent progress surface for a foreground operation.
    /// </summary>
    public partial class LoadingWindow : FluentWindow
    {
        #region Constructor

        /// <summary>
        /// Initializes the operation progress window.
        /// </summary>
        public LoadingWindow()
        {
            InitializeComponent();
        }

        #endregion
    }
}
