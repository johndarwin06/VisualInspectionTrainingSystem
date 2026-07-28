#region Namespaces

using System;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Repositories;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Defines current-user-only read operations for trainee training history.
    /// </summary>
    public interface ITrainingHistoryService
    {
        #region Read Methods

        /// <summary>
        /// Loads one bounded page for the active session identity.
        /// </summary>
        /// <param name="query">Filter and paging request that contains no user identity.</param>
        /// <returns>A deterministic current-user history page.</returns>
        TrainingHistoryPage GetHistoryPage(TrainingHistoryQuery query);

        /// <summary>
        /// Loads one completed session only when it belongs to the active session identity.
        /// </summary>
        /// <param name="sessionId">Requested session identity.</param>
        /// <returns>Authorized read-only detail, or null when unavailable.</returns>
        TrainingHistorySessionDetail GetSessionDetail(int sessionId);

        /// <summary>
        /// Loads an exact seven-day or thirty-day progress series for the active trainee.
        /// </summary>
        /// <param name="dayCount">Supported local-day range: seven or thirty days.</param>
        /// <returns>A chart-neutral, zero-filled current-trainee series.</returns>
        AnalyticsChartData GetProgressChartData(int dayCount);

        #endregion
    }

    /// <summary>
    /// Applies active-session authorization before delegating read-only history queries.
    /// </summary>
    public sealed class TrainingHistoryService : ITrainingHistoryService
    {
        #region Fields

        private readonly TrainingHistoryRepository _repository;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the current-user history service with the production repository.
        /// </summary>
        public TrainingHistoryService()
            : this(new TrainingHistoryRepository())
        {
        }

        /// <summary>
        /// Initializes the service with an explicit repository for in-assembly verification.
        /// </summary>
        /// <param name="repository">Read-only training history repository.</param>
        internal TrainingHistoryService(TrainingHistoryRepository repository)
        {
            if (repository == null)
                throw new ArgumentNullException(nameof(repository));

            _repository = repository;
        }

        #endregion

        #region Public Read Methods

        /// <summary>
        /// Loads one bounded page using only the active user's identity.
        /// </summary>
        /// <param name="query">Identity-free filter and paging request.</param>
        /// <returns>A deterministic current-user history page.</returns>
        public TrainingHistoryPage GetHistoryPage(TrainingHistoryQuery query)
        {
            string employeeNo = GetAuthorizedEmployeeNo();
            return _repository.GetHistoryPage(employeeNo, query);
        }

        /// <summary>
        /// Loads one session only when it belongs to the active user.
        /// </summary>
        /// <param name="sessionId">Requested session identity.</param>
        /// <returns>Authorized read-only detail, or null when unavailable.</returns>
        public TrainingHistorySessionDetail GetSessionDetail(int sessionId)
        {
            string employeeNo = GetAuthorizedEmployeeNo();
            return _repository.GetSessionDetail(employeeNo, sessionId);
        }

        /// <summary>
        /// Loads progress analytics using only the active canonical trainee identity.
        /// </summary>
        /// <param name="dayCount">Supported local-day range: seven or thirty days.</param>
        /// <returns>A chart-neutral, zero-filled current-trainee series.</returns>
        public AnalyticsChartData GetProgressChartData(int dayCount)
        {
            string employeeNo = GetAuthorizedTraineeEmployeeNo();
            return _repository.GetProgressChartData(employeeNo, dayCount);
        }

        #endregion

        #region Authorization

        /// <summary>
        /// Captures and validates the active application identity at the service boundary.
        /// </summary>
        /// <returns>The canonical current employee number.</returns>
        private static string GetAuthorizedEmployeeNo()
        {
            User user = SessionService.CurrentUser;

            if (user == null ||
                !user.IsActive ||
                string.IsNullOrWhiteSpace(user.EmployeeNo))
            {
                throw new UnauthorizedAccessException(
                    "An active authenticated user is required for training history.");
            }

            string normalizedRole = UserRoles.Normalize(user.Role);

            if (!string.Equals(
                    normalizedRole,
                    UserRoles.User,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    normalizedRole,
                    UserRoles.Admin,
                    StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "The authenticated user role is invalid.");
            }

            string employeeNo = user.EmployeeNo.Trim();

            if (employeeNo.Length > 20)
            {
                throw new UnauthorizedAccessException(
                    "The authenticated user identity is invalid.");
            }

            return employeeNo;
        }

        /// <summary>
        /// Captures an active canonical trainee identity for personal analytics.
        /// </summary>
        /// <returns>The trimmed current trainee employee number.</returns>
        private static string GetAuthorizedTraineeEmployeeNo()
        {
            User user = SessionService.CurrentUser;

            if (user == null ||
                !user.IsActive ||
                string.IsNullOrWhiteSpace(user.EmployeeNo))
            {
                throw new UnauthorizedAccessException(
                    "An active authenticated trainee is required for progress analytics.");
            }

            if (!string.Equals(
                    UserRoles.Normalize(user.Role),
                    UserRoles.User,
                    StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "Progress analytics is available only to an authenticated trainee.");
            }

            string employeeNo = user.EmployeeNo.Trim();

            if (employeeNo.Length > 20)
            {
                throw new UnauthorizedAccessException(
                    "The authenticated user identity is invalid.");
            }

            return employeeNo;
        }

        #endregion
    }
}
