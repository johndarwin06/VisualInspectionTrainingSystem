#region Namespaces

using System;
using System.Reflection;
using System.Threading.Tasks;
using VisualInspectionTrainingSystem.Services;
using VisualInspectionTrainingSystem.ViewModels;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// ViewModel for the Splash Screen.
    /// </summary>
    public class SplashViewModel : BaseViewModel
    {
        #region Fields

        private readonly SystemInitializerService _initializer;

        private int _progress;
        private string _statusMessage;

        #endregion

        #region Constructor

        public SplashViewModel()
        {
            Version = "Version " +
                      Assembly.GetExecutingAssembly()
                              .GetName()
                              .Version;

            _initializer = new SystemInitializerService();

            _initializer.ProgressChanged += Initializer_ProgressChanged;

            _initializer.InitializationCompleted += Initializer_InitializationCompleted;

            _ = InitializeAsync();
        }

        #endregion

        #region Properties

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string Version
        {
            get;
        }

        #endregion

        #region Events

        public event EventHandler InitializationCompleted;

        #endregion

        #region Initialization

        private async Task InitializeAsync()
        {
            await _initializer.InitializeAsync();
        }

        #endregion

        #region Event Handlers

        private void Initializer_ProgressChanged(object sender, InitializationProgressEventArgs e)
        {
            Progress = e.Progress;
            StatusMessage = e.Message;
        }

        private void Initializer_InitializationCompleted(object sender, EventArgs e)
        {
            InitializationCompleted?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
