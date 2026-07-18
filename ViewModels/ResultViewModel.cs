#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VisualInspectionTrainingSystem.Commands;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Provides read-only quiz result statistics, answer filters, and safe image previews.
    /// </summary>
    public class ResultViewModel : BaseViewModel, IDisposable
    {
        #region Constants

        /// <summary>
        /// Filter that displays every answer.
        /// </summary>
        public const string AllFilter = "All";

        /// <summary>
        /// Filter that displays reviewed answers that do not match administrator truth.
        /// </summary>
        public const string WrongFilter = "Wrong";

        /// <summary>
        /// Filter that displays every trainee NG selection.
        /// </summary>
        public const string NgFilter = "NG";

        /// <summary>
        /// Filter that displays answers still awaiting administrator review.
        /// </summary>
        public const string PendingFilter = "Pending";

        #endregion

        #region Fields

        private readonly ImageService _imageService;
        private CancellationTokenSource _previewCancellation;
        private ReadOnlyCollection<QuizAnswer> _displayedAnswers;
        private QuizAnswer _selectedAnswer;
        private BitmapImage _selectedImagePreview;
        private string _previewStatus;
        private string _selectedFilter;
        private bool _isPreviewLoading;
        private long _previewGeneration;
        private int _disposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a result view-model from an independent snapshot of the supplied answers.
        /// </summary>
        /// <param name="answers">Completed quiz answers. Null is treated as an empty result.</param>
        public ResultViewModel(List<QuizAnswer> answers)
        {
            StatisticsService statisticsService = new StatisticsService();

            _imageService = new ImageService();
            Statistics = statisticsService.Calculate(answers);
            Answers = new List<QuizAnswer>(Statistics.Answers);
            CompletedAt = DateTime.Now;
            FilterCommand = new RelayCommand(
                new Action<object>(ApplyFilterFromParameter));
            _previewStatus = "Select an answer to preview its image.";

            ApplyFilter(AllFilter);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the command used to select an answer filter.
        /// </summary>
        public ICommand FilterCommand { get; private set; }

        #endregion

        #region Snapshot And Selection

        /// <summary>
        /// Gets the legacy-compatible list of cloned answers supplied to this result.
        /// Calculations and filters remain backed by the independent statistics snapshot.
        /// </summary>
        public List<QuizAnswer> Answers { get; private set; }

        /// <summary>
        /// Gets the calculated statistics for the answer snapshot.
        /// </summary>
        public ResultStatistics Statistics { get; private set; }

        /// <summary>
        /// Gets the answers visible under the current filter.
        /// </summary>
        public ReadOnlyCollection<QuizAnswer> DisplayedAnswers
        {
            get { return _displayedAnswers; }
            private set { SetProperty(ref _displayedAnswers, value); }
        }

        /// <summary>
        /// Gets or sets the answer selected for detail and image preview.
        /// </summary>
        public QuizAnswer SelectedAnswer
        {
            get { return _selectedAnswer; }
            set
            {
                if (SetProperty(ref _selectedAnswer, value))
                {
                    BeginPreviewLoad(value);
                }
            }
        }

        /// <summary>
        /// Gets the fully detached image for the selected answer.
        /// </summary>
        public BitmapImage SelectedImagePreview
        {
            get { return _selectedImagePreview; }
            private set { SetProperty(ref _selectedImagePreview, value); }
        }

        /// <summary>
        /// Gets a non-sensitive status describing the current preview state.
        /// </summary>
        public string PreviewStatus
        {
            get { return _previewStatus; }
            private set { SetProperty(ref _previewStatus, value); }
        }

        /// <summary>
        /// Gets whether a detached preview is currently being decoded.
        /// </summary>
        public bool IsPreviewLoading
        {
            get { return _isPreviewLoading; }
            private set { SetProperty(ref _isPreviewLoading, value); }
        }

        /// <summary>
        /// Gets the active answer-filter key.
        /// </summary>
        public string SelectedFilter
        {
            get { return _selectedFilter; }
            private set { SetProperty(ref _selectedFilter, value); }
        }

        /// <summary>
        /// Gets a concise count for the active answer filter.
        /// </summary>
        public string FilteredAnswerCountText
        {
            get
            {
                int count = DisplayedAnswers == null
                    ? 0
                    : DisplayedAnswers.Count;

                return string.Format(
                    "{0} of {1} answers",
                    count,
                    TotalQuestions);
            }
        }

        #endregion

        #region Summary Properties

        /// <summary>
        /// Gets when this result view-model was created.
        /// </summary>
        public DateTime CompletedAt { get; private set; }

        /// <summary>
        /// Gets the number of recorded answers.
        /// </summary>
        public int TotalQuestions { get { return Statistics.TotalQuestions; } }

        /// <summary>
        /// Gets the number of trainee GOOD selections.
        /// </summary>
        public int GoodAnswers { get { return Statistics.UserGoodAnswers; } }

        /// <summary>
        /// Gets the number of trainee NG selections.
        /// </summary>
        public int NgAnswers { get { return Statistics.UserNgAnswers; } }

        /// <summary>
        /// Gets the number of answers with administrator truth.
        /// </summary>
        public int ReviewedAnswers { get { return Statistics.ReviewedAnswers; } }

        /// <summary>
        /// Gets the number of answers awaiting administrator review.
        /// </summary>
        public int PendingReviewAnswers { get { return Statistics.PendingReviewAnswers; } }

        /// <summary>
        /// Gets correct answers among reviewed rows only.
        /// </summary>
        public int CorrectAnswers { get { return Statistics.CorrectReviewedAnswers; } }

        /// <summary>
        /// Gets wrong answers among reviewed rows only.
        /// </summary>
        public int WrongAnswers { get { return Statistics.WrongReviewedAnswers; } }

        /// <summary>
        /// Gets the percentage of trainee GOOD selections.
        /// </summary>
        public double GoodPercentage { get { return Statistics.GoodPercentage; } }

        /// <summary>
        /// Gets the percentage of trainee NG selections.
        /// </summary>
        public double NgPercentage { get { return Statistics.NgPercentage; } }

        /// <summary>
        /// Gets the percentage of answers reviewed by an administrator.
        /// </summary>
        public double ReviewCoveragePercentage
        {
            get { return Statistics.ReviewCoveragePercentage; }
        }

        /// <summary>
        /// Gets the percentage of answers pending review.
        /// </summary>
        public double PendingReviewPercentage
        {
            get
            {
                return TotalQuestions == 0
                    ? 0
                    : Math.Round(100 - ReviewCoveragePercentage, 2);
            }
        }

        /// <summary>
        /// Gets the reviewed accuracy percentage.
        /// </summary>
        public double ReviewedAccuracyPercentage
        {
            get { return Statistics.ReviewedAccuracyPercentage; }
        }

        /// <summary>
        /// Gets the reviewed wrong percentage.
        /// </summary>
        public double ReviewedWrongPercentage
        {
            get
            {
                return ReviewedAnswers == 0
                    ? 0
                    : Math.Round(100 - ReviewedAccuracyPercentage, 2);
            }
        }

        /// <summary>
        /// Gets the sum of valid elapsed values.
        /// </summary>
        public double TotalElapsedSeconds { get { return Statistics.TotalElapsedSeconds; } }

        /// <summary>
        /// Gets the average of valid elapsed values.
        /// </summary>
        public double AverageElapsedSeconds { get { return Statistics.AverageElapsedSeconds; } }

        /// <summary>
        /// Gets the fastest valid elapsed value.
        /// </summary>
        public double FastestElapsedSeconds { get { return Statistics.FastestElapsedSeconds; } }

        /// <summary>
        /// Gets the slowest valid elapsed value.
        /// </summary>
        public double SlowestElapsedSeconds { get { return Statistics.SlowestElapsedSeconds; } }

        #endregion

        #region NG Analysis Properties

        /// <summary>
        /// Gets trainee NG selections as a percentage of all answers.
        /// </summary>
        public double UserNgRatePercentage { get { return Statistics.UserNgRatePercentage; } }

        /// <summary>
        /// Gets reviewed answers whose administrator truth is NG.
        /// </summary>
        public int ReviewedActualNgAnswers { get { return Statistics.ReviewedActualNgAnswers; } }

        /// <summary>
        /// Gets reviewed answers whose administrator truth is GOOD.
        /// </summary>
        public int ReviewedActualGoodAnswers { get { return Statistics.ReviewedActualGoodAnswers; } }

        /// <summary>
        /// Gets reviewed NG images correctly identified as NG.
        /// </summary>
        public int CorrectlyDetectedNgAnswers { get { return Statistics.CorrectlyDetectedNgAnswers; } }

        /// <summary>
        /// Gets reviewed GOOD images selected as NG.
        /// </summary>
        public int FalseNgAnswers { get { return Statistics.FalseNgAnswers; } }

        /// <summary>
        /// Gets reviewed NG images selected as GOOD.
        /// </summary>
        public int MissedNgAnswers { get { return Statistics.MissedNgAnswers; } }

        /// <summary>
        /// Gets the reviewed NG detection rate.
        /// </summary>
        public double NgDetectionRatePercentage
        {
            get { return Statistics.NgDetectionRatePercentage; }
        }

        /// <summary>
        /// Gets the false NG rate among reviewed actual GOOD images.
        /// </summary>
        public double FalseNgRatePercentage
        {
            get { return Statistics.FalseNgRatePercentage; }
        }

        #endregion

        #region Display Text

        /// <summary>
        /// Gets the result creation time in a stable local display format.
        /// </summary>
        public string CompletedAtText
        {
            get { return CompletedAt.ToString("yyyy-MM-dd HH:mm:ss"); }
        }

        /// <summary>
        /// Gets total valid elapsed time text.
        /// </summary>
        public string TotalElapsedText
        {
            get { return string.Format("{0:0.00} s", TotalElapsedSeconds); }
        }

        /// <summary>
        /// Gets average valid elapsed time text, or N/A when no valid timing exists.
        /// </summary>
        public string AverageElapsedText
        {
            get { return FormatTiming(AverageElapsedSeconds); }
        }

        /// <summary>
        /// Gets fastest valid elapsed time text, or N/A when no valid timing exists.
        /// </summary>
        public string FastestElapsedText
        {
            get { return FormatTiming(FastestElapsedSeconds); }
        }

        /// <summary>
        /// Gets slowest valid elapsed time text, or N/A when no valid timing exists.
        /// </summary>
        public string SlowestElapsedText
        {
            get { return FormatTiming(SlowestElapsedSeconds); }
        }

        /// <summary>
        /// Gets a safe review-completion summary.
        /// </summary>
        public string ReviewStatusText
        {
            get
            {
                if (TotalQuestions == 0)
                    return "No answers recorded";

                if (PendingReviewAnswers == 0)
                    return "Review completed";

                return string.Format(
                    "{0} pending review",
                    PendingReviewAnswers);
            }
        }

        /// <summary>
        /// Gets reviewed accuracy text without treating pending answers as wrong.
        /// </summary>
        public string AccuracyText
        {
            get
            {
                return ReviewedAnswers == 0
                    ? "Pending Review"
                    : FormatPercentage(ReviewedAccuracyPercentage);
            }
        }

        /// <summary>
        /// Gets reviewed NG detection text, or N/A when no reviewed NG truth exists.
        /// </summary>
        public string NgDetectionRateText
        {
            get
            {
                return ReviewedActualNgAnswers == 0
                    ? "N/A"
                    : FormatPercentage(NgDetectionRatePercentage);
            }
        }

        /// <summary>
        /// Gets false NG rate text, or N/A when no reviewed GOOD truth exists.
        /// </summary>
        public string FalseNgRateText
        {
            get
            {
                return ReviewedActualGoodAnswers == 0
                    ? "N/A"
                    : FormatPercentage(FalseNgRatePercentage);
            }
        }

        #endregion

        #region Filtering

        /// <summary>
        /// Applies a recognized filter supplied by the command parameter.
        /// </summary>
        private void ApplyFilterFromParameter(object parameter)
        {
            ApplyFilter(parameter as string);
        }

        /// <summary>
        /// Replaces the displayed read-only list without changing the source snapshot.
        /// </summary>
        private void ApplyFilter(string requestedFilter)
        {
            string filter = NormalizeFilter(requestedFilter);
            IEnumerable<QuizAnswer> filteredAnswers = Statistics.Answers;

            if (filter == WrongFilter)
            {
                filteredAnswers = Statistics.Answers.Where(
                    answer => IsReviewed(answer) &&
                              answer.UserAnswer != answer.CorrectAnswer.Value);
            }
            else if (filter == NgFilter)
            {
                filteredAnswers = Statistics.Answers.Where(
                    answer => answer.UserAnswer == QuizAnswerType.Ng);
            }
            else if (filter == PendingFilter)
            {
                filteredAnswers = Statistics.Answers.Where(
                    answer => !IsReviewed(answer));
            }

            SelectedAnswer = null;
            SelectedFilter = filter;
            DisplayedAnswers = new ReadOnlyCollection<QuizAnswer>(
                filteredAnswers.ToList());
            OnPropertyChanged(nameof(FilteredAnswerCountText));
        }

        /// <summary>
        /// Converts unknown or blank filter values to the safe All filter.
        /// </summary>
        private static string NormalizeFilter(string requestedFilter)
        {
            if (requestedFilter == WrongFilter ||
                requestedFilter == NgFilter ||
                requestedFilter == PendingFilter)
            {
                return requestedFilter;
            }

            return AllFilter;
        }

        /// <summary>
        /// Returns whether administrator truth contains a supported GOOD or NG value.
        /// </summary>
        private static bool IsReviewed(QuizAnswer answer)
        {
            return answer != null &&
                   answer.CorrectAnswer.HasValue &&
                   Enum.IsDefined(
                       typeof(QuizAnswerType),
                       answer.CorrectAnswer.Value);
        }

        #endregion

        #region Preview Lifecycle

        /// <summary>
        /// Cancels the previous selection and starts one observed preview operation.
        /// </summary>
        private void BeginPreviewLoad(QuizAnswer answer)
        {
            CancellationTokenSource previousCancellation = _previewCancellation;
            long generation = Interlocked.Increment(ref _previewGeneration);

            _previewCancellation = null;

            if (previousCancellation != null)
            {
                previousCancellation.Cancel();
                previousCancellation.Dispose();
            }

            SelectedImagePreview = null;
            IsPreviewLoading = false;

            if (Volatile.Read(ref _disposed) != 0)
                return;

            if (answer == null)
            {
                PreviewStatus = "Select an answer to preview its image.";
                return;
            }

            if (string.IsNullOrWhiteSpace(answer.FilePath))
            {
                PreviewStatus = "No image is available for this answer.";
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();

            _previewCancellation = cancellation;
            IsPreviewLoading = true;
            PreviewStatus = "Loading image preview...";

            Task previewTask = LoadPreviewAsync(
                answer,
                generation,
                cancellation.Token);

            previewTask.ContinueWith(
                task =>
                {
                    Exception ignored = task.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Loads a detached preview and rejects cancellation, stale selection, and close races.
        /// </summary>
        private async Task LoadPreviewAsync(
            QuizAnswer answer,
            long generation,
            CancellationToken cancellationToken)
        {
            try
            {
                BitmapImage bitmap = await _imageService.LoadBitmapAsync(
                    answer.FilePath,
                    cancellationToken);

                if (!CanPublishPreview(
                    answer,
                    generation,
                    cancellationToken))
                {
                    return;
                }

                SelectedImagePreview = bitmap;
                PreviewStatus = "Image preview ready.";
            }
            catch (OperationCanceledException)
            {
                // Selection changes and window close are expected cancellation paths.
            }
            catch (Exception)
            {
                if (CanPublishPreview(
                    answer,
                    generation,
                    cancellationToken))
                {
                    SelectedImagePreview = null;
                    PreviewStatus = "Image preview is unavailable.";
                }
            }
            finally
            {
                if (generation == Interlocked.Read(ref _previewGeneration) &&
                    Volatile.Read(ref _disposed) == 0)
                {
                    IsPreviewLoading = false;
                }
            }
        }

        /// <summary>
        /// Returns whether a preview still belongs to the live, selected answer.
        /// </summary>
        private bool CanPublishPreview(
            QuizAnswer answer,
            long generation,
            CancellationToken cancellationToken)
        {
            return Volatile.Read(ref _disposed) == 0 &&
                   !cancellationToken.IsCancellationRequested &&
                   generation == Interlocked.Read(ref _previewGeneration) &&
                   ReferenceEquals(answer, SelectedAnswer);
        }

        #endregion

        #region Formatting Helpers

        /// <summary>
        /// Formats a percentage consistently for result labels.
        /// </summary>
        private static string FormatPercentage(double value)
        {
            return string.Format("{0:0.00}%", value);
        }

        /// <summary>
        /// Formats timing only when the snapshot has a valid elapsed value.
        /// </summary>
        private string FormatTiming(double value)
        {
            return Statistics.ValidTimingAnswers == 0
                ? "N/A"
                : string.Format("{0:0.00} s", value);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Cancels preview work and prevents late image publication after the window closes.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            Interlocked.Increment(ref _previewGeneration);

            CancellationTokenSource cancellation = _previewCancellation;

            _previewCancellation = null;

            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }

            _selectedAnswer = null;
            SelectedImagePreview = null;
            IsPreviewLoading = false;
        }

        #endregion
    }
}
