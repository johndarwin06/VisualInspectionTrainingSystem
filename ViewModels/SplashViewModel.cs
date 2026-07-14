#region Namespaces

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// ViewModel for the Splash Screen.
    /// </summary>
    public class SplashViewModel : BaseViewModel
    {
        #region Fields

        private readonly object _syncRoot;
        private readonly SystemInitializerService _initializer;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _initializationTask;
        private bool _completionRaised;
        private bool _closeRequested;

        private int _progress;
        private string _statusMessage;
        private string _diagnosticMessage;
        private bool _isInitializing;
        private bool _hasFailed;
        private bool _isReady;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the splash screen view model.
        /// </summary>
        public SplashViewModel()
        {
            _syncRoot = new object();

            Version = "Version " +
                      Assembly.GetExecutingAssembly()
                              .GetName()
                              .Version;

            StatusMessage = "Preparing startup checks...";
            DiagnosticMessage = string.Empty;

            _initializer = new SystemInitializerService();

            _initializer.ProgressChanged += Initializer_ProgressChanged;

            ExitCommand = new RelayCommand(RequestClose);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Startup progress from 0 through 100.
        /// </summary>
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        /// <summary>
        /// Primary non-sensitive startup status.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Supporting non-sensitive startup diagnostics.
        /// </summary>
        public string DiagnosticMessage
        {
            get => _diagnosticMessage;
            set => SetProperty(ref _diagnosticMessage, value);
        }

        /// <summary>
        /// True while startup initialization is running.
        /// </summary>
        public bool IsInitializing
        {
            get => _isInitializing;
            set => SetProperty(ref _isInitializing, value);
        }

        /// <summary>
        /// True when startup failed or was cancelled.
        /// </summary>
        public bool HasFailed
        {
            get => _hasFailed;
            set => SetProperty(ref _hasFailed, value);
        }

        /// <summary>
        /// True when all required startup checks passed.
        /// </summary>
        public bool IsReady
        {
            get => _isReady;
            set => SetProperty(ref _isReady, value);
        }

        /// <summary>
        /// Application version text.
        /// </summary>
        public string Version
        {
            get;
        }

        #endregion

        #region Commands

        /// <summary>
        /// Requests that the splash window close.
        /// </summary>
        public ICommand ExitCommand
        {
            get;
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when required startup checks complete successfully.
        /// </summary>
        public event EventHandler InitializationCompleted;

        /// <summary>
        /// Raised when startup fails or is cancelled.
        /// </summary>
        public event EventHandler InitializationFailed;

        /// <summary>
        /// Raised when the user requests the splash window to close.
        /// </summary>
        public event EventHandler CloseRequested;

        #endregion

        #region Initialization

        /// <summary>
        /// Starts initialization once and awaits the existing task on duplicate calls.
        /// </summary>
        public Task StartInitializationAsync()
        {
            lock (_syncRoot)
            {
                if (_initializationTask == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    _initializationTask = InitializeCoreAsync(_cancellationTokenSource.Token);
                }

                return _initializationTask;
            }
        }

        /// <summary>
        /// Cancels startup initialization safely.
        /// </summary>
        public void CancelInitialization()
        {
            _closeRequested = true;

            CancellationTokenSource cancellationTokenSource =
                _cancellationTokenSource;

            if (cancellationTokenSource != null &&
                !cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// Runs startup initialization and applies the result.
        /// </summary>
        private async Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            IsInitializing = true;
            HasFailed = false;
            IsReady = false;
            DiagnosticMessage = string.Empty;

            try
            {
                InitializationResult result =
                    await _initializer.InitializeAsync(cancellationToken);

                ApplyInitializationResult(result);
            }
            catch (OperationCanceledException)
            {
                ApplyInitializationResult(
                    InitializationResult.CreateCancelled(
                        "Startup cancelled.",
                        "Initialization was cancelled before completion."));
            }
            catch
            {
                ApplyInitializationResult(
                    InitializationResult.Failed(
                        "Startup failed because an unexpected initialization error occurred.",
                        "Unexpected startup exception. Review configuration and service availability."));
            }
            finally
            {
                IsInitializing = false;
            }
        }

        /// <summary>
        /// Applies startup result values to the view model.
        /// </summary>
        private void ApplyInitializationResult(InitializationResult result)
        {
            if (result == null)
            {
                result = InitializationResult.Failed(
                    "Startup failed.",
                    "Startup returned no result.");
            }

            StatusMessage = result.StatusMessage;
            DiagnosticMessage = result.DiagnosticMessage;

            if (result.Succeeded)
            {
                Progress = 100;
                HasFailed = false;
                IsReady = true;

                RaiseInitializationCompletedOnce();

                return;
            }

            HasFailed = true;
            IsReady = false;

            if (result.Cancelled &&
                _closeRequested)
            {
                return;
            }

            InitializationFailed?.Invoke(
                this,
                EventArgs.Empty);
        }

        /// <summary>
        /// Raises successful initialization only once.
        /// </summary>
        private void RaiseInitializationCompletedOnce()
        {
            if (_completionRaised ||
                _closeRequested)
            {
                return;
            }

            _completionRaised = true;

            InitializationCompleted?.Invoke(
                this,
                EventArgs.Empty);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Updates splash status from the initializer service.
        /// </summary>
        private void Initializer_ProgressChanged(
            object sender,
            InitializationProgressEventArgs e)
        {
            if (_closeRequested ||
                e == null)
            {
                return;
            }

            Progress = e.Progress;
            StatusMessage = e.Message;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Requests the owning splash window to close.
        /// </summary>
        private void RequestClose()
        {
            CloseRequested?.Invoke(
                this,
                EventArgs.Empty);
        }

        #endregion
    }
}
