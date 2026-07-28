#region Namespaces

using VisualInspectionTrainingSystem.Models;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Presents trainee-owned progress analytics without exposing administrator data.
    /// </summary>
    public sealed class TraineeProgressChartViewModel : AnalyticsChartViewModel
    {
        #region Fields

        private string _progressPrompt;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes an empty trainee progress chart presentation.
        /// </summary>
        public TraineeProgressChartViewModel()
            : base()
        {
            _progressPrompt = CreateProgressPrompt();
        }

        /// <summary>
        /// Initializes a trainee progress chart presentation from trainee-scoped data.
        /// </summary>
        /// <param name="data">Chart data already restricted to the current trainee.</param>
        public TraineeProgressChartViewModel(AnalyticsChartData data)
            : base(data)
        {
            _progressPrompt = CreateProgressPrompt();
        }

        #endregion

        #region Presentation Properties

        /// <summary>
        /// Gets the trainee-facing chart section title.
        /// </summary>
        public string Title
        {
            get { return "My training progress"; }
        }

        /// <summary>
        /// Gets the trainee-facing chart section description.
        /// </summary>
        public string Description
        {
            get { return "Your completed training, reviewed feedback, selections, and time spent."; }
        }

        /// <summary>
        /// Gets a safe callout that reflects the currently available progress data.
        /// </summary>
        public string ProgressPrompt
        {
            get { return _progressPrompt; }
            private set { SetProperty(ref _progressPrompt, value); }
        }

        /// <summary>
        /// Gets whether the trainee has reviewed feedback in the current range.
        /// </summary>
        public bool HasReviewedFeedback
        {
            get { return HasReviewedAccuracyData; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Replaces all trainee chart series from the latest trainee-scoped data.
        /// </summary>
        /// <param name="data">Chart data already restricted to the current trainee.</param>
        public void UpdateProgress(AnalyticsChartData data)
        {
            Update(data);
            ProgressPrompt = CreateProgressPrompt();
            OnPropertyChanged(nameof(HasReviewedFeedback));
        }

        #endregion

        #region Prompt Helpers

        private string CreateProgressPrompt()
        {
            if (!IsAvailable)
            {
                return "Progress charts are temporarily unavailable.";
            }

            if (IsEmpty)
            {
                return "Complete a training session to begin your progress trend.";
            }

            if (!HasReviewedAccuracyData)
            {
                return "Your activity is shown; reviewed accuracy will appear after administrator review.";
            }

            return "Use these trends with reviewed results to guide your next training session.";
        }

        #endregion
    }
}
