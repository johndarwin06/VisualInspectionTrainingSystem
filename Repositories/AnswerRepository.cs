#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using VisualInspectionTrainingSystem.Models;
using VisualInspectionTrainingSystem.Services;

#endregion

namespace VisualInspectionTrainingSystem.Repositories
{
    /// <summary>
    /// Provides transactional answer persistence, reusable image truth, and administrator review workflows.
    /// </summary>
    public class AnswerRepository
    {
        #region Constants

        private const string AnswerTableName = "tbl_quiz_answer";

        private const string TruthTableName = "tbl_image_review_truth";

        private const string ImageHashIndexName = "IX_tbl_quiz_answer_ImageHash";

        private const string SafeStaleReviewMessage =
            "Review data changed after it was loaded. Refresh the review queue and try again.";

        #endregion

        #region Fields

        private readonly MySqlService _database;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the answer repository with the configured MySQL service.
        /// </summary>
        public AnswerRepository()
            : this(new MySqlService())
        {
        }

        /// <summary>
        /// Initializes the answer repository with an existing database service.
        /// </summary>
        /// <param name="database">Database service shared with an owning repository when required.</param>
        internal AnswerRepository(MySqlService database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        #endregion

        #region Public Read Methods

        /// <summary>
        /// Loads all saved answers for deterministic administrator filtering and searching.
        /// </summary>
        /// <returns>Answers ordered newest first.</returns>
        public List<QuizAnswer> GetForReview()
        {
            const string sql = @"
SELECT
    a.AnswerID,
    a.SessionID,
    s.EmployeeNo,
    a.ImageID,
    a.ImageHash,
    a.ImageFileName,
    a.UserAnswer,
    a.CorrectAnswer,
    a.IsCorrect,
    a.AnswerTime,
    a.ReviewSource,
    a.ReviewedAt,
    a.ReviewedBy
FROM tbl_quiz_answer a
INNER JOIN tbl_training_session s
    ON s.SessionID = a.SessionID
ORDER BY a.AnswerTime DESC, a.AnswerID DESC;";

            try
            {
                _database.OpenConnection();
                MySqlConnection connection = _database.GetConnection();

                EnsureTable(connection);

                List<QuizAnswer> answers = new List<QuizAnswer>();

                using (MySqlCommand command = new MySqlCommand(sql, connection))
                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        answers.Add(MapAnswer(reader));
                    }
                }

                return answers;
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Loads current reusable truth and propagation impact for one stable image.
        /// </summary>
        /// <param name="imageHash">Normalized or normalizable stable image identity.</param>
        /// <returns>Current truth and matching answer/session counts.</returns>
        public ReviewImpact GetReviewImpact(string imageHash)
        {
            string normalizedHash = ImageService.NormalizeImageHash(imageHash);

            const string sql = @"
SELECT
    (
        SELECT CorrectAnswer
        FROM tbl_image_review_truth
        WHERE ImageHash = @ImageHash
        LIMIT 1
    ) AS CurrentTruth,
    (
        SELECT COUNT(*)
        FROM tbl_quiz_answer
        WHERE ImageHash = @ImageHash
    ) AS AnswerCount,
    (
        SELECT COUNT(DISTINCT SessionID)
        FROM tbl_quiz_answer
        WHERE ImageHash = @ImageHash
    ) AS SessionCount;";

            try
            {
                _database.OpenConnection();
                MySqlConnection connection = _database.GetConnection();

                EnsureTable(connection);

                using (MySqlCommand command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ImageHash", normalizedHash);

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return new ReviewImpact(null, 0, 0);

                        return new ReviewImpact(
                            ReadNullableAnswer(reader, "CurrentTruth"),
                            ReadRequiredInt(reader, "AnswerCount"),
                            ReadRequiredInt(reader, "SessionCount"));
                    }
                }
            }
            finally
            {
                _database.CloseConnection();
            }
        }

        #endregion

        #region Public Review Methods

        /// <summary>
        /// Preserves the compatibility API for reviewing one answer.
        /// Legacy rows remain individually reviewable and are never propagated by ImageID.
        /// </summary>
        /// <param name="answerId">Selected answer identity.</param>
        /// <param name="correctAnswer">Administrator GOOD or NG truth.</param>
        public void ReviewAnswer(
            int answerId,
            QuizAnswerType correctAnswer)
        {
            ReviewAnswer(
                answerId,
                correctAnswer,
                null,
                null,
                null,
                null);
        }

        /// <summary>
        /// Reviews one answer, creates or corrects reusable truth, propagates by stable hash, and recalculates sessions.
        /// </summary>
        /// <param name="answerId">Selected answer identity.</param>
        /// <param name="correctAnswer">Administrator GOOD or NG truth.</param>
        /// <param name="reviewerEmployeeNo">Reviewer employee number when available.</param>
        /// <param name="expectedCurrentTruth">Truth shown by the administrator screen for stale-update protection.</param>
        /// <param name="confirmedLegacyImageHash">Administrator-confirmed preview hash for one legacy row.</param>
        /// <param name="confirmedLegacyFileName">Safe filename associated with the confirmed legacy preview.</param>
        /// <returns>Committed propagation and recalculation counts.</returns>
        public ReviewOperationResult ReviewAnswer(
            int answerId,
            QuizAnswerType correctAnswer,
            string reviewerEmployeeNo,
            QuizAnswerType? expectedCurrentTruth,
            string confirmedLegacyImageHash,
            string confirmedLegacyFileName)
        {
            if (answerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(answerId));

            ValidateAnswerType(correctAnswer, nameof(correctAnswer));

            MySqlTransaction transaction = null;

            try
            {
                _database.OpenConnection();
                MySqlConnection connection = _database.GetConnection();

                EnsureTable(connection);

                transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);

                LockedAnswer selectedAnswer = LoadLockedAnswer(
                    answerId,
                    connection,
                    transaction);

                if (selectedAnswer == null)
                    throw new InvalidOperationException("The selected answer no longer exists.");

                bool attachedLegacyIdentity = false;

                if (string.IsNullOrWhiteSpace(selectedAnswer.ImageHash) &&
                    !string.IsNullOrWhiteSpace(confirmedLegacyImageHash))
                {
                    selectedAnswer.ImageHash = ImageService.NormalizeImageHash(
                        confirmedLegacyImageHash);
                    selectedAnswer.ImageFileName = NormalizeFileName(
                        confirmedLegacyFileName);

                    AttachLegacyIdentity(
                        selectedAnswer,
                        connection,
                        transaction);

                    attachedLegacyIdentity = true;
                }

                ReviewOperationResult result;

                if (string.IsNullOrWhiteSpace(selectedAnswer.ImageHash))
                {
                    UpdateLegacyAnswer(
                        selectedAnswer,
                        correctAnswer,
                        reviewerEmployeeNo,
                        connection,
                        transaction);

                    RecalculateSessions(
                        new[] { selectedAnswer.SessionID },
                        connection,
                        transaction);

                    result = new ReviewOperationResult(
                        1,
                        0,
                        1,
                        1,
                        1,
                        0);
                }
                else
                {
                    bool allowExistingSameTruth = attachedLegacyIdentity &&
                                                  !expectedCurrentTruth.HasValue;

                    result = ApplyReviewGroups(
                        new[]
                        {
                            new ReviewGroup(
                                selectedAnswer.ImageHash,
                                selectedAnswer.AnswerID,
                                attachedLegacyIdentity
                                    ? (QuizAnswerType?)null
                                    : expectedCurrentTruth,
                                true,
                                allowExistingSameTruth)
                        },
                        1,
                        correctAnswer,
                        reviewerEmployeeNo,
                        0,
                        connection,
                        transaction);
                }

                transaction.Commit();
                transaction.Dispose();
                transaction = null;

                return result;
            }
            catch (Exception ex)
            {
                RollbackTransaction(transaction, "administrator review", ex);

                if (IsSafeReviewException(ex))
                    throw;

                throw new InvalidOperationException(
                    "The administrator review could not be completed. No review or session changes were saved.",
                    ex);
            }
            finally
            {
                if (transaction != null)
                    transaction.Dispose();

                _database.CloseConnection();
            }
        }

        /// <summary>
        /// Applies one GOOD or NG decision per selected stable image in one atomic transaction.
        /// </summary>
        /// <param name="selectedAnswers">Rows selected on the administrator screen.</param>
        /// <param name="correctAnswer">Shared bulk GOOD or NG truth.</param>
        /// <param name="reviewerEmployeeNo">Reviewer employee number when available.</param>
        /// <returns>Committed selected, unique-image, answer, session, missing-identity, and correction counts.</returns>
        public ReviewOperationResult ReviewAnswers(
            IEnumerable<QuizAnswer> selectedAnswers,
            QuizAnswerType correctAnswer,
            string reviewerEmployeeNo)
        {
            ValidateAnswerType(correctAnswer, nameof(correctAnswer));

            List<QuizAnswer> selected = MaterializeSelectedAnswers(selectedAnswers);
            MySqlTransaction transaction = null;

            try
            {
                _database.OpenConnection();
                MySqlConnection connection = _database.GetConnection();

                EnsureTable(connection);

                transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);

                List<LockedAnswer> lockedAnswers = LoadLockedAnswers(
                    selected.Select(answer => answer.AnswerID),
                    connection,
                    transaction);

                if (lockedAnswers.Count != selected.Count)
                    throw new InvalidOperationException(SafeStaleReviewMessage);

                Dictionary<int, QuizAnswer> selectedById = selected.ToDictionary(
                    answer => answer.AnswerID);
                Dictionary<string, ReviewGroup> groups =
                    new Dictionary<string, ReviewGroup>(StringComparer.OrdinalIgnoreCase);
                int missingIdentityCount = 0;

                foreach (LockedAnswer lockedAnswer in lockedAnswers)
                {
                    QuizAnswer screenAnswer = selectedById[lockedAnswer.AnswerID];

                    if (!HashesMatch(screenAnswer.ImageHash, lockedAnswer.ImageHash))
                        throw new InvalidOperationException(SafeStaleReviewMessage);

                    if (string.IsNullOrWhiteSpace(lockedAnswer.ImageHash))
                    {
                        missingIdentityCount++;
                        continue;
                    }

                    string imageHash = ImageService.NormalizeImageHash(lockedAnswer.ImageHash);
                    ReviewGroup group;

                    if (!groups.TryGetValue(imageHash, out group))
                    {
                        group = new ReviewGroup(
                            imageHash,
                            lockedAnswer.AnswerID,
                            screenAnswer.CorrectAnswer,
                            true,
                            false);
                        groups.Add(imageHash, group);
                    }
                    else if (!NullableAnswersEqual(
                                 group.ExpectedCurrentTruth,
                                 screenAnswer.CorrectAnswer))
                    {
                        throw new InvalidOperationException(
                            "Selected duplicate images contain conflicting loaded truth. Refresh before retrying the bulk review.");
                    }
                }

                ReviewOperationResult result = ApplyReviewGroups(
                    groups.Values,
                    selected.Count,
                    correctAnswer,
                    reviewerEmployeeNo,
                    missingIdentityCount,
                    connection,
                    transaction);

                transaction.Commit();
                transaction.Dispose();
                transaction = null;

                return result;
            }
            catch (Exception ex)
            {
                RollbackTransaction(transaction, "bulk administrator review", ex);

                if (IsSafeReviewException(ex))
                    throw;

                throw new InvalidOperationException(
                    "The bulk review could not be completed. No review or session changes were saved.",
                    ex);
            }
            finally
            {
                if (transaction != null)
                    transaction.Dispose();

                _database.CloseConnection();
            }
        }

        #endregion

        #region Public Persistence Methods

        /// <summary>
        /// Saves all answers for one existing training session atomically.
        /// </summary>
        /// <param name="sessionId">Parent session identity.</param>
        /// <param name="answers">New answers with valid stable image hashes.</param>
        public void SaveMany(
            int sessionId,
            IEnumerable<QuizAnswer> answers)
        {
            if (sessionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sessionId));

            List<QuizAnswer> validatedAnswers = ValidateAnswersForPersistence(answers);
            MySqlTransaction transaction = null;

            try
            {
                _database.OpenConnection();
                MySqlConnection connection = _database.GetConnection();

                EnsureTable(connection);

                transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);

                SaveMany(
                    sessionId,
                    validatedAnswers,
                    connection,
                    transaction);

                transaction.Commit();
                transaction.Dispose();
                transaction = null;
            }
            catch (Exception ex)
            {
                RollbackTransaction(transaction, "answer persistence", ex);

                throw new InvalidOperationException(
                    "Failed to save quiz answers. The answer transaction was rolled back.",
                    ex);
            }
            finally
            {
                if (transaction != null)
                    transaction.Dispose();

                _database.CloseConnection();
            }
        }

        #endregion

        #region Internal Schema Methods

        /// <summary>
        /// Creates and idempotently upgrades answer and reusable-truth schema outside data transactions.
        /// </summary>
        /// <param name="connection">Open MySQL connection.</param>
        internal void EnsureTable(MySqlConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            const string createAnswerSql = @"
CREATE TABLE IF NOT EXISTS tbl_quiz_answer
(
    AnswerID INT AUTO_INCREMENT PRIMARY KEY,
    SessionID INT NOT NULL,
    ImageID INT NOT NULL,
    ImageHash CHAR(64) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
    ImageFileName VARCHAR(255) NULL,
    UserAnswer VARCHAR(10),
    CorrectAnswer VARCHAR(10),
    IsCorrect BIT,
    AnswerTime DATETIME,
    ReviewSource VARCHAR(20) NULL,
    ReviewedAt DATETIME NULL,
    ReviewedBy VARCHAR(20) NULL,
    KEY IX_tbl_quiz_answer_ImageHash (ImageHash),
    FOREIGN KEY(SessionID)
        REFERENCES tbl_training_session(SessionID)
);";

            using (MySqlCommand command = new MySqlCommand(createAnswerSql, connection))
            {
                command.ExecuteNonQuery();
            }

            EnsureAnswerColumn(
                connection,
                "ImageHash",
                "CHAR(64) CHARACTER SET ascii COLLATE ascii_general_ci NULL AFTER ImageID");
            EnsureAnswerColumn(
                connection,
                "ImageFileName",
                "VARCHAR(255) NULL AFTER ImageHash");
            EnsureAnswerColumn(
                connection,
                "ReviewSource",
                "VARCHAR(20) NULL AFTER AnswerTime");
            EnsureAnswerColumn(
                connection,
                "ReviewedAt",
                "DATETIME NULL AFTER ReviewSource");
            EnsureAnswerColumn(
                connection,
                "ReviewedBy",
                "VARCHAR(20) NULL AFTER ReviewedAt");
            EnsureImageHashIndex(connection);
            EnsureTruthTable(connection);
        }

        #endregion

        #region Internal Persistence Methods

        /// <summary>
        /// Saves answers using one existing connection and transaction after one bounded truth preload.
        /// </summary>
        internal void SaveMany(
            int sessionId,
            IEnumerable<QuizAnswer> answers,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            if (sessionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sessionId));

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            List<QuizAnswer> validatedAnswers = ValidateAnswersForPersistence(answers);
            List<string> hashes = validatedAnswers
                .Select(answer => answer.ImageHash)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Dictionary<string, TruthRecord> truths = LoadTruthsForUpdate(
                hashes,
                connection,
                transaction);

            foreach (QuizAnswer answer in validatedAnswers)
            {
                TruthRecord truth;

                if (truths.TryGetValue(answer.ImageHash, out truth))
                {
                    answer.CorrectAnswer = truth.CorrectAnswer;
                    answer.IsCorrect = answer.UserAnswer == truth.CorrectAnswer;
                    answer.ReviewSource = QuizAnswer.AutomaticReviewSource;
                    answer.ReviewedAt = truth.ReviewedAt;
                    answer.ReviewedBy = truth.ReviewerEmployeeNo;
                }
                else
                {
                    answer.CorrectAnswer = null;
                    answer.IsCorrect = false;
                    answer.ReviewSource = null;
                    answer.ReviewedAt = null;
                    answer.ReviewedBy = null;
                }

                Save(
                    sessionId,
                    answer,
                    connection,
                    transaction);
            }

            RecalculateSessions(
                new[] { sessionId },
                connection,
                transaction);
        }

        /// <summary>
        /// Validates one newly created answer before it reaches MySQL.
        /// </summary>
        internal static void ValidateAnswerForPersistence(
            QuizAnswer answer,
            string parameterName)
        {
            if (answer == null)
                throw new ArgumentNullException(parameterName);

            if (answer.ImageID <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "ImageID must be greater than zero.");
            }

            answer.ImageHash = ImageService.NormalizeImageHash(answer.ImageHash);
            answer.FileName = NormalizeFileName(answer.FileName);

            ValidateAnswerType(
                answer.UserAnswer,
                parameterName + ".UserAnswer");

            if (answer.CorrectAnswer.HasValue)
            {
                ValidateAnswerType(
                    answer.CorrectAnswer.Value,
                    parameterName + ".CorrectAnswer");

                if (answer.IsCorrect !=
                    (answer.UserAnswer == answer.CorrectAnswer.Value))
                {
                    throw new ArgumentException(
                        "IsCorrect must match UserAnswer and CorrectAnswer.",
                        parameterName);
                }
            }
            else if (answer.IsCorrect)
            {
                throw new ArgumentException(
                    "Pending answers cannot be marked correct.",
                    parameterName);
            }

            if (answer.AnswerTime == DateTime.MinValue)
                throw new ArgumentException("AnswerTime is required.", parameterName);

            if (answer.ElapsedSeconds < 0 ||
                double.IsNaN(answer.ElapsedSeconds) ||
                double.IsInfinity(answer.ElapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "ElapsedSeconds must be finite and non-negative.");
            }
        }

        /// <summary>
        /// Recalculates every distinct affected session using supported GOOD/NG review semantics.
        /// </summary>
        internal static void RecalculateSessions(
            IEnumerable<int> sessionIds,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            if (sessionIds == null)
                throw new ArgumentNullException(nameof(sessionIds));

            foreach (int sessionId in sessionIds.Distinct().OrderBy(id => id))
            {
                RecalculateSession(
                    sessionId,
                    connection,
                    transaction);
            }
        }

        #endregion

        #region Review Transaction Helpers

        /// <summary>
        /// Applies grouped image decisions and recalculates each affected session once.
        /// </summary>
        private static ReviewOperationResult ApplyReviewGroups(
            IEnumerable<ReviewGroup> reviewGroups,
            int selectedRowCount,
            QuizAnswerType correctAnswer,
            string reviewerEmployeeNo,
            int missingIdentityCount,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            List<ReviewGroup> groups = reviewGroups
                .OrderBy(group => group.ImageHash, StringComparer.Ordinal)
                .ToList();
            HashSet<int> affectedSessionIds = new HashSet<int>();
            int updatedAnswerCount = 0;
            int correctionCount = 0;
            DateTime reviewedAt = TrimToSecond(DateTime.Now);
            string reviewer = NormalizeReviewer(reviewerEmployeeNo);

            foreach (ReviewGroup group in groups)
            {
                TruthRecord existingTruth = LoadTruthForUpdate(
                    group.ImageHash,
                    connection,
                    transaction);

                EnsureExpectedTruth(
                    existingTruth,
                    group,
                    correctAnswer);

                if (existingTruth == null)
                {
                    InsertTruth(
                        group.ImageHash,
                        correctAnswer,
                        group.SourceAnswerId,
                        reviewer,
                        reviewedAt,
                        connection,
                        transaction);
                }
                else if (existingTruth.CorrectAnswer != correctAnswer)
                {
                    UpdateTruth(
                        existingTruth,
                        correctAnswer,
                        group.SourceAnswerId,
                        reviewer,
                        reviewedAt,
                        connection,
                        transaction);
                    correctionCount++;
                }

                List<LockedAnswer> matchingAnswers = LoadMatchingAnswersForUpdate(
                    group.ImageHash,
                    connection,
                    transaction);

                foreach (LockedAnswer matchingAnswer in matchingAnswers)
                {
                    affectedSessionIds.Add(matchingAnswer.SessionID);
                }

                UpdateMatchingAnswers(
                    group.ImageHash,
                    group.SourceAnswerId,
                    correctAnswer,
                    reviewer,
                    reviewedAt,
                    connection,
                    transaction);

                updatedAnswerCount += matchingAnswers.Count;
            }

            RecalculateSessions(
                affectedSessionIds,
                connection,
                transaction);

            return new ReviewOperationResult(
                selectedRowCount,
                groups.Count,
                updatedAnswerCount,
                affectedSessionIds.Count,
                missingIdentityCount,
                correctionCount);
        }

        /// <summary>
        /// Rejects a stale screen before a truth row can be overwritten.
        /// </summary>
        private static void EnsureExpectedTruth(
            TruthRecord existingTruth,
            ReviewGroup group,
            QuizAnswerType requestedTruth)
        {
            QuizAnswerType? actualTruth = existingTruth == null
                ? (QuizAnswerType?)null
                : existingTruth.CorrectAnswer;

            if (!group.EnforceExpectedTruth)
                return;

            if (NullableAnswersEqual(actualTruth, group.ExpectedCurrentTruth))
                return;

            if (group.AllowExistingSameTruth &&
                actualTruth.HasValue &&
                actualTruth.Value == requestedTruth)
            {
                return;
            }

            throw new InvalidOperationException(SafeStaleReviewMessage);
        }

        /// <summary>
        /// Locks one selected answer.
        /// </summary>
        private static LockedAnswer LoadLockedAnswer(
            int answerId,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
SELECT
    AnswerID,
    SessionID,
    ImageHash,
    ImageFileName,
    CorrectAnswer
FROM tbl_quiz_answer
WHERE AnswerID = @AnswerID
LIMIT 1
FOR UPDATE;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@AnswerID", answerId);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return MapLockedAnswer(reader);
                }
            }
        }

        /// <summary>
        /// Locks every selected answer in deterministic identity order.
        /// </summary>
        private static List<LockedAnswer> LoadLockedAnswers(
            IEnumerable<int> answerIds,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            List<int> ids = answerIds.Distinct().OrderBy(id => id).ToList();
            string parameterList = BuildParameterList("@AnswerID", ids.Count);
            string sql = @"
SELECT
    AnswerID,
    SessionID,
    ImageHash,
    ImageFileName,
    CorrectAnswer
FROM tbl_quiz_answer
WHERE AnswerID IN (" + parameterList + @")
ORDER BY AnswerID
FOR UPDATE;";

            List<LockedAnswer> answers = new List<LockedAnswer>();

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                AddParameters(command, "@AnswerID", ids.Cast<object>().ToList());

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        answers.Add(MapLockedAnswer(reader));
                }
            }

            return answers;
        }

        /// <summary>
        /// Locks all answer rows sharing one stable image identity.
        /// </summary>
        private static List<LockedAnswer> LoadMatchingAnswersForUpdate(
            string imageHash,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
SELECT
    AnswerID,
    SessionID,
    ImageHash,
    ImageFileName,
    CorrectAnswer
FROM tbl_quiz_answer
WHERE ImageHash = @ImageHash
ORDER BY AnswerID
FOR UPDATE;";

            List<LockedAnswer> answers = new List<LockedAnswer>();

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@ImageHash", imageHash);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        answers.Add(MapLockedAnswer(reader));
                }
            }

            return answers;
        }

        /// <summary>
        /// Locks current reusable truth or the indexed gap for a not-yet-reviewed hash.
        /// </summary>
        private static TruthRecord LoadTruthForUpdate(
            string imageHash,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
SELECT
    ImageHash,
    CorrectAnswer,
    ReviewedAt,
    ReviewerEmployeeNo,
    Version
FROM tbl_image_review_truth
WHERE ImageHash = @ImageHash
LIMIT 1
FOR UPDATE;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@ImageHash", imageHash);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    QuizAnswerType? correctAnswer = ReadNullableAnswer(
                        reader,
                        "CorrectAnswer");

                    if (!correctAnswer.HasValue)
                    {
                        throw new InvalidOperationException(
                            "Stored reusable image truth is invalid.");
                    }

                    return new TruthRecord(
                        ImageService.NormalizeImageHash(reader["ImageHash"].ToString()),
                        correctAnswer.Value,
                        ReadRequiredDate(reader, "ReviewedAt"),
                        ReadOptionalString(reader, "ReviewerEmployeeNo"),
                        ReadRequiredInt(reader, "Version"));
                }
            }
        }

        /// <summary>
        /// Inserts first administrator truth for a stable image.
        /// </summary>
        private static void InsertTruth(
            string imageHash,
            QuizAnswerType correctAnswer,
            int sourceAnswerId,
            string reviewer,
            DateTime reviewedAt,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
INSERT INTO tbl_image_review_truth
(
    ImageHash,
    CorrectAnswer,
    ReviewedAt,
    SourceAnswerID,
    ReviewerEmployeeNo,
    Version,
    LastUpdated
)
VALUES
(
    @ImageHash,
    @CorrectAnswer,
    @ReviewedAt,
    @SourceAnswerID,
    @ReviewerEmployeeNo,
    1,
    @LastUpdated
);";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                AddTruthParameters(
                    command,
                    imageHash,
                    correctAnswer,
                    sourceAnswerId,
                    reviewer,
                    reviewedAt);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Corrects reusable truth using the locked version as an optimistic safety check.
        /// </summary>
        private static void UpdateTruth(
            TruthRecord existingTruth,
            QuizAnswerType correctAnswer,
            int sourceAnswerId,
            string reviewer,
            DateTime reviewedAt,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
UPDATE tbl_image_review_truth
SET
    CorrectAnswer = @CorrectAnswer,
    ReviewedAt = @ReviewedAt,
    SourceAnswerID = @SourceAnswerID,
    ReviewerEmployeeNo = @ReviewerEmployeeNo,
    Version = Version + 1,
    LastUpdated = @LastUpdated
WHERE ImageHash = @ImageHash
  AND Version = @ExpectedVersion;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                AddTruthParameters(
                    command,
                    existingTruth.ImageHash,
                    correctAnswer,
                    sourceAnswerId,
                    reviewer,
                    reviewedAt);
                command.Parameters.AddWithValue("@ExpectedVersion", existingTruth.Version);

                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException(SafeStaleReviewMessage);
            }
        }

        /// <summary>
        /// Updates every answer with the exact stable hash from its own trainee selection.
        /// </summary>
        private static void UpdateMatchingAnswers(
            string imageHash,
            int sourceAnswerId,
            QuizAnswerType correctAnswer,
            string reviewer,
            DateTime reviewedAt,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
UPDATE tbl_quiz_answer
SET
    CorrectAnswer = @CorrectAnswer,
    IsCorrect = CASE
        WHEN UPPER(TRIM(UserAnswer)) IN ('GOOD', 'NG')
         AND UPPER(TRIM(UserAnswer)) = @CorrectAnswer THEN 1
        ELSE 0
    END,
    ReviewSource = CASE
        WHEN AnswerID = @SourceAnswerID THEN 'MANUAL'
        ELSE 'AUTO'
    END,
    ReviewedAt = @ReviewedAt,
    ReviewedBy = @ReviewerEmployeeNo
WHERE ImageHash = @ImageHash;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@CorrectAnswer", GetAnswerText(correctAnswer));
                command.Parameters.AddWithValue("@SourceAnswerID", sourceAnswerId);
                command.Parameters.AddWithValue("@ReviewedAt", reviewedAt);
                command.Parameters.AddWithValue(
                    "@ReviewerEmployeeNo",
                    string.IsNullOrWhiteSpace(reviewer)
                        ? (object)DBNull.Value
                        : reviewer);
                command.Parameters.AddWithValue("@ImageHash", imageHash);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Attaches an administrator-confirmed preview identity to one locked legacy row only.
        /// </summary>
        private static void AttachLegacyIdentity(
            LockedAnswer answer,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
UPDATE tbl_quiz_answer
SET
    ImageHash = @ImageHash,
    ImageFileName = @ImageFileName
WHERE AnswerID = @AnswerID
  AND ImageHash IS NULL;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@ImageHash", answer.ImageHash);
                command.Parameters.AddWithValue(
                    "@ImageFileName",
                    string.IsNullOrWhiteSpace(answer.ImageFileName)
                        ? (object)DBNull.Value
                        : answer.ImageFileName);
                command.Parameters.AddWithValue("@AnswerID", answer.AnswerID);

                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException(SafeStaleReviewMessage);
            }
        }

        /// <summary>
        /// Reviews one legacy answer without creating or propagating reusable truth.
        /// </summary>
        private static void UpdateLegacyAnswer(
            LockedAnswer answer,
            QuizAnswerType correctAnswer,
            string reviewerEmployeeNo,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
UPDATE tbl_quiz_answer
SET
    CorrectAnswer = @CorrectAnswer,
    IsCorrect = CASE
        WHEN UPPER(TRIM(UserAnswer)) IN ('GOOD', 'NG')
         AND UPPER(TRIM(UserAnswer)) = @CorrectAnswer THEN 1
        ELSE 0
    END,
    ReviewSource = 'MANUAL',
    ReviewedAt = @ReviewedAt,
    ReviewedBy = @ReviewedBy
WHERE AnswerID = @AnswerID;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@CorrectAnswer", GetAnswerText(correctAnswer));
                command.Parameters.AddWithValue("@ReviewedAt", TrimToSecond(DateTime.Now));
                command.Parameters.AddWithValue(
                    "@ReviewedBy",
                    GetNullableReviewerValue(reviewerEmployeeNo));
                command.Parameters.AddWithValue("@AnswerID", answer.AnswerID);

                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException(SafeStaleReviewMessage);
            }
        }

        #endregion

        #region Automatic Truth Helpers

        /// <summary>
        /// Loads all matching truth rows in one bounded command inside the session transaction.
        /// </summary>
        private static Dictionary<string, TruthRecord> LoadTruthsForUpdate(
            IList<string> imageHashes,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            Dictionary<string, TruthRecord> truths =
                new Dictionary<string, TruthRecord>(StringComparer.OrdinalIgnoreCase);

            if (imageHashes == null || imageHashes.Count == 0)
                return truths;

            string parameterList = BuildParameterList("@ImageHash", imageHashes.Count);
            string sql = @"
SELECT
    ImageHash,
    CorrectAnswer,
    ReviewedAt,
    ReviewerEmployeeNo,
    Version
FROM tbl_image_review_truth
WHERE ImageHash IN (" + parameterList + @")
ORDER BY ImageHash
FOR UPDATE;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                AddParameters(
                    command,
                    "@ImageHash",
                    imageHashes.Cast<object>().ToList());

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        QuizAnswerType? correctAnswer = ReadNullableAnswer(
                            reader,
                            "CorrectAnswer");

                        if (!correctAnswer.HasValue)
                            throw new InvalidOperationException("Stored reusable image truth is invalid.");

                        TruthRecord truth = new TruthRecord(
                            ImageService.NormalizeImageHash(reader["ImageHash"].ToString()),
                            correctAnswer.Value,
                            ReadRequiredDate(reader, "ReviewedAt"),
                            ReadOptionalString(reader, "ReviewerEmployeeNo"),
                            ReadRequiredInt(reader, "Version"));

                        truths[truth.ImageHash] = truth;
                    }
                }
            }

            return truths;
        }

        #endregion

        #region Session Recalculation

        /// <summary>
        /// Recalculates one locked session from supported reviewed answer values.
        /// </summary>
        private static void RecalculateSession(
            int sessionId,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            if (sessionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sessionId));

            EnsureSessionExists(sessionId, connection, transaction);

            const string sql = @"
UPDATE tbl_training_session s
LEFT JOIN
(
    SELECT
        SessionID,
        SUM(CASE
            WHEN UPPER(TRIM(CorrectAnswer)) IN ('GOOD', 'NG')
             AND IsCorrect = 1 THEN 1
            ELSE 0
        END) AS CorrectAnswers,
        SUM(CASE
            WHEN UPPER(TRIM(CorrectAnswer)) IN ('GOOD', 'NG')
             AND (IsCorrect IS NULL OR IsCorrect = 0) THEN 1
            ELSE 0
        END) AS WrongAnswers,
        SUM(CASE
            WHEN UPPER(TRIM(CorrectAnswer)) IN ('GOOD', 'NG') THEN 1
            ELSE 0
        END) AS ReviewedAnswers
    FROM tbl_quiz_answer
    WHERE SessionID = @SessionID
    GROUP BY SessionID
) answerTotals
    ON answerTotals.SessionID = s.SessionID
SET
    s.CorrectAnswers = IFNULL(answerTotals.CorrectAnswers, 0),
    s.WrongAnswers = IFNULL(answerTotals.WrongAnswers, 0),
    s.Accuracy = CASE
        WHEN IFNULL(answerTotals.ReviewedAnswers, 0) = 0 THEN 0
        ELSE ROUND(IFNULL(answerTotals.CorrectAnswers, 0) * 100.0 /
                   answerTotals.ReviewedAnswers, 2)
    END
WHERE s.SessionID = @SessionID;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@SessionID", sessionId);

                if (command.ExecuteNonQuery() != 1)
                {
                    throw new InvalidOperationException(
                        "Session recalculation did not affect exactly one row.");
                }
            }
        }

        /// <summary>
        /// Locks and verifies a parent training session before recalculation.
        /// </summary>
        private static void EnsureSessionExists(
            int sessionId,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
SELECT SessionID
FROM tbl_training_session
WHERE SessionID = @SessionID
LIMIT 1
FOR UPDATE;";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@SessionID", sessionId);

                object result = command.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException("Parent training session was not found.");
            }
        }

        #endregion

        #region Save Helpers

        /// <summary>
        /// Inserts one prepared answer through the active session transaction.
        /// </summary>
        private static void Save(
            int sessionId,
            QuizAnswer answer,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            const string sql = @"
INSERT INTO tbl_quiz_answer
(
    SessionID,
    ImageID,
    ImageHash,
    ImageFileName,
    UserAnswer,
    CorrectAnswer,
    IsCorrect,
    AnswerTime,
    ReviewSource,
    ReviewedAt,
    ReviewedBy
)
VALUES
(
    @SessionID,
    @ImageID,
    @ImageHash,
    @ImageFileName,
    @UserAnswer,
    @CorrectAnswer,
    @IsCorrect,
    @AnswerTime,
    @ReviewSource,
    @ReviewedAt,
    @ReviewedBy
);";

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@SessionID", sessionId);
                command.Parameters.AddWithValue("@ImageID", answer.ImageID);
                command.Parameters.AddWithValue("@ImageHash", answer.ImageHash);
                command.Parameters.AddWithValue(
                    "@ImageFileName",
                    string.IsNullOrWhiteSpace(answer.FileName)
                        ? (object)DBNull.Value
                        : answer.FileName);
                command.Parameters.AddWithValue("@UserAnswer", GetAnswerText(answer.UserAnswer));
                command.Parameters.AddWithValue(
                    "@CorrectAnswer",
                    GetNullableAnswerText(answer.CorrectAnswer));
                command.Parameters.AddWithValue("@IsCorrect", answer.IsCorrect);
                command.Parameters.AddWithValue("@AnswerTime", TrimToSecond(answer.AnswerTime));
                command.Parameters.AddWithValue(
                    "@ReviewSource",
                    string.IsNullOrWhiteSpace(answer.ReviewSource)
                        ? (object)DBNull.Value
                        : answer.ReviewSource);
                command.Parameters.AddWithValue(
                    "@ReviewedAt",
                    answer.ReviewedAt.HasValue
                        ? (object)TrimToSecond(answer.ReviewedAt.Value)
                        : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@ReviewedBy",
                    GetNullableReviewerValue(answer.ReviewedBy));
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Validates and materializes an answer collection once.
        /// </summary>
        private static List<QuizAnswer> ValidateAnswersForPersistence(
            IEnumerable<QuizAnswer> answers)
        {
            if (answers == null)
                throw new ArgumentNullException(nameof(answers));

            List<QuizAnswer> validated = new List<QuizAnswer>();
            int index = 0;

            foreach (QuizAnswer answer in answers)
            {
                ValidateAnswerForPersistence(
                    answer,
                    "answers[" + index + "]");
                validated.Add(answer);
                index++;
            }

            if (validated.Count == 0)
                throw new ArgumentException("At least one answer is required.", nameof(answers));

            return validated;
        }

        #endregion

        #region Schema Upgrade

        /// <summary>
        /// Adds a missing answer column idempotently.
        /// </summary>
        private static void EnsureAnswerColumn(
            MySqlConnection connection,
            string columnName,
            string definition)
        {
            if (ColumnExists(connection, AnswerTableName, columnName))
                return;

            string sql = "ALTER TABLE " + AnswerTableName +
                         " ADD COLUMN " + columnName + " " + definition + ";";

            try
            {
                using (MySqlCommand command = new MySqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (MySqlException ex)
            {
                if (ex.Number != 1060 ||
                    !ColumnExists(connection, AnswerTableName, columnName))
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Creates the answer hash index when missing.
        /// </summary>
        private static void EnsureImageHashIndex(MySqlConnection connection)
        {
            if (IndexExists(connection, AnswerTableName, ImageHashIndexName))
                return;

            const string sql = @"
CREATE INDEX IX_tbl_quiz_answer_ImageHash
ON tbl_quiz_answer (ImageHash);";

            try
            {
                using (MySqlCommand command = new MySqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (MySqlException ex)
            {
                if (ex.Number != 1061 ||
                    !IndexExists(connection, AnswerTableName, ImageHashIndexName))
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Creates the one-current-truth-per-hash table idempotently.
        /// </summary>
        private static void EnsureTruthTable(MySqlConnection connection)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS tbl_image_review_truth
(
    ImageHash CHAR(64) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    CorrectAnswer VARCHAR(10) NOT NULL,
    ReviewedAt DATETIME NOT NULL,
    SourceAnswerID INT NULL,
    ReviewerEmployeeNo VARCHAR(20) NULL,
    Version INT NOT NULL DEFAULT 1,
    LastUpdated DATETIME NOT NULL,
    PRIMARY KEY (ImageHash),
    CONSTRAINT CK_tbl_image_review_truth_CorrectAnswer
        CHECK (UPPER(TRIM(CorrectAnswer)) IN ('GOOD', 'NG'))
);";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Returns whether a column exists in the active database.
        /// </summary>
        private static bool ColumnExists(
            MySqlConnection connection,
            string tableName,
            string columnName)
        {
            const string sql = @"
SELECT COUNT(*)
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @TableName
  AND COLUMN_NAME = @ColumnName;";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);
                command.Parameters.AddWithValue("@ColumnName", columnName);

                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        /// <summary>
        /// Returns whether an index exists in the active database.
        /// </summary>
        private static bool IndexExists(
            MySqlConnection connection,
            string tableName,
            string indexName)
        {
            const string sql = @"
SELECT COUNT(*)
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @TableName
  AND INDEX_NAME = @IndexName;";

            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);
                command.Parameters.AddWithValue("@IndexName", indexName);

                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        #endregion

        #region Mapping

        /// <summary>
        /// Maps one administrator review row.
        /// </summary>
        private static QuizAnswer MapAnswer(MySqlDataReader reader)
        {
            QuizAnswerType? correctAnswer = ReadNullableAnswer(
                reader,
                "CorrectAnswer");

            return new QuizAnswer
            {
                AnswerID = ReadRequiredInt(reader, "AnswerID"),
                SessionID = ReadRequiredInt(reader, "SessionID"),
                EmployeeNo = ReadRequiredString(reader, "EmployeeNo"),
                ImageID = ReadRequiredInt(reader, "ImageID"),
                ImageHash = ReadOptionalString(reader, "ImageHash"),
                FileName = ReadOptionalString(reader, "ImageFileName"),
                UserAnswer = ReadRequiredAnswer(reader, "UserAnswer"),
                CorrectAnswer = correctAnswer,
                IsCorrect = ReadIsCorrect(reader, correctAnswer.HasValue),
                AnswerTime = ReadRequiredDate(reader, "AnswerTime"),
                ReviewSource = ReadOptionalString(reader, "ReviewSource"),
                ReviewedAt = ReadOptionalDate(reader, "ReviewedAt"),
                ReviewedBy = ReadOptionalString(reader, "ReviewedBy")
            };
        }

        /// <summary>
        /// Maps one locked answer row used by review transactions.
        /// </summary>
        private static LockedAnswer MapLockedAnswer(MySqlDataReader reader)
        {
            return new LockedAnswer(
                ReadRequiredInt(reader, "AnswerID"),
                ReadRequiredInt(reader, "SessionID"),
                ReadOptionalString(reader, "ImageHash"),
                ReadOptionalString(reader, "ImageFileName"),
                ReadNullableAnswer(reader, "CorrectAnswer"));
        }

        #endregion

        #region Conversion Helpers

        /// <summary>
        /// Converts one supported answer to database text.
        /// </summary>
        private static string GetAnswerText(QuizAnswerType answer)
        {
            ValidateAnswerType(answer, nameof(answer));
            return answer.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// Converts nullable truth to a database value.
        /// </summary>
        private static object GetNullableAnswerText(QuizAnswerType? answer)
        {
            return answer.HasValue
                ? (object)GetAnswerText(answer.Value)
                : DBNull.Value;
        }

        /// <summary>
        /// Validates that an answer enum is GOOD or NG.
        /// </summary>
        private static void ValidateAnswerType(
            QuizAnswerType answer,
            string parameterName)
        {
            if (answer != QuizAnswerType.Good &&
                answer != QuizAnswerType.Ng)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    answer,
                    "Answer must be GOOD or NG.");
            }
        }

        /// <summary>
        /// Reads a required supported trainee answer.
        /// </summary>
        private static QuizAnswerType ReadRequiredAnswer(
            MySqlDataReader reader,
            string columnName)
        {
            QuizAnswerType? answer = ReadNullableAnswer(reader, columnName);

            if (!answer.HasValue)
                throw new InvalidOperationException(columnName + " must be GOOD or NG.");

            return answer.Value;
        }

        /// <summary>
        /// Reads supported truth; malformed or blank values remain pending.
        /// </summary>
        private static QuizAnswerType? ReadNullableAnswer(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];

            if (value == null || value == DBNull.Value)
                return null;

            string text = value.ToString().Trim();

            if (string.Equals(text, "GOOD", StringComparison.OrdinalIgnoreCase))
                return QuizAnswerType.Good;

            if (string.Equals(text, "NG", StringComparison.OrdinalIgnoreCase))
                return QuizAnswerType.Ng;

            return null;
        }

        /// <summary>
        /// Reads a required integer.
        /// </summary>
        private static int ReadRequiredInt(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];

            if (value == null || value == DBNull.Value)
                throw new InvalidOperationException(columnName + " is required.");

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads a required non-blank string.
        /// </summary>
        private static string ReadRequiredString(
            MySqlDataReader reader,
            string columnName)
        {
            string value = ReadOptionalString(reader, columnName);

            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(columnName + " is required.");

            return value;
        }

        /// <summary>
        /// Reads an optional string.
        /// </summary>
        private static string ReadOptionalString(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];

            return value == null || value == DBNull.Value
                ? null
                : value.ToString();
        }

        /// <summary>
        /// Reads a required date.
        /// </summary>
        private static DateTime ReadRequiredDate(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];

            if (value == null || value == DBNull.Value)
                throw new InvalidOperationException(columnName + " is required.");

            return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads an optional date.
        /// </summary>
        private static DateTime? ReadOptionalDate(
            MySqlDataReader reader,
            string columnName)
        {
            object value = reader[columnName];

            if (value == null || value == DBNull.Value)
                return null;

            return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads IsCorrect without treating pending values as wrong.
        /// </summary>
        private static bool ReadIsCorrect(
            MySqlDataReader reader,
            bool isReviewed)
        {
            object value = reader["IsCorrect"];

            if (value == null || value == DBNull.Value)
                return false;

            return isReviewed && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        #endregion

        #region General Helpers

        /// <summary>
        /// Materializes unique selected answer IDs and rejects invalid selections.
        /// </summary>
        private static List<QuizAnswer> MaterializeSelectedAnswers(
            IEnumerable<QuizAnswer> selectedAnswers)
        {
            if (selectedAnswers == null)
                throw new ArgumentNullException(nameof(selectedAnswers));

            List<QuizAnswer> selected = selectedAnswers
                .Where(answer => answer != null)
                .GroupBy(answer => answer.AnswerID)
                .Select(group => group.First())
                .OrderBy(answer => answer.AnswerID)
                .ToList();

            if (selected.Count == 0 || selected.Any(answer => answer.AnswerID <= 0))
                throw new ArgumentException("Select at least one saved answer.", nameof(selectedAnswers));

            return selected;
        }

        /// <summary>
        /// Builds parameter names for a bounded IN clause.
        /// </summary>
        private static string BuildParameterList(
            string prefix,
            int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            return string.Join(
                ", ",
                Enumerable.Range(0, count).Select(index => prefix + index));
        }

        /// <summary>
        /// Adds indexed command parameters.
        /// </summary>
        private static void AddParameters(
            MySqlCommand command,
            string prefix,
            IList<object> values)
        {
            for (int index = 0; index < values.Count; index++)
                command.Parameters.AddWithValue(prefix + index, values[index]);
        }

        /// <summary>
        /// Adds common truth insert/update parameters.
        /// </summary>
        private static void AddTruthParameters(
            MySqlCommand command,
            string imageHash,
            QuizAnswerType correctAnswer,
            int sourceAnswerId,
            string reviewer,
            DateTime reviewedAt)
        {
            command.Parameters.AddWithValue("@ImageHash", imageHash);
            command.Parameters.AddWithValue("@CorrectAnswer", GetAnswerText(correctAnswer));
            command.Parameters.AddWithValue("@ReviewedAt", reviewedAt);
            command.Parameters.AddWithValue("@SourceAnswerID", sourceAnswerId);
            command.Parameters.AddWithValue(
                "@ReviewerEmployeeNo",
                string.IsNullOrWhiteSpace(reviewer)
                    ? (object)DBNull.Value
                    : reviewer);
            command.Parameters.AddWithValue("@LastUpdated", reviewedAt);
        }

        /// <summary>
        /// Returns whether two nullable truth values match.
        /// </summary>
        private static bool NullableAnswersEqual(
            QuizAnswerType? first,
            QuizAnswerType? second)
        {
            return first.HasValue == second.HasValue &&
                   (!first.HasValue || first.Value == second.Value);
        }

        /// <summary>
        /// Returns whether two optional image hashes represent the same stable identity.
        /// </summary>
        private static bool HashesMatch(
            string first,
            string second)
        {
            if (string.IsNullOrWhiteSpace(first) &&
                string.IsNullOrWhiteSpace(second))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(first) ||
                string.IsNullOrWhiteSpace(second))
            {
                return false;
            }

            return string.Equals(
                ImageService.NormalizeImageHash(first),
                ImageService.NormalizeImageHash(second),
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Reduces a filename to its final safe path component.
        /// </summary>
        private static string NormalizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            string normalized = Path.GetFileName(fileName.Trim());

            if (normalized.Length > 255)
                normalized = normalized.Substring(0, 255);

            return normalized;
        }

        /// <summary>
        /// Normalizes a reviewer employee number for optional persistence.
        /// </summary>
        private static string NormalizeReviewer(string reviewerEmployeeNo)
        {
            if (string.IsNullOrWhiteSpace(reviewerEmployeeNo))
                return null;

            string normalized = reviewerEmployeeNo.Trim();

            return normalized.Length <= 20
                ? normalized
                : normalized.Substring(0, 20);
        }

        /// <summary>
        /// Converts optional reviewer text to a database value.
        /// </summary>
        private static object GetNullableReviewerValue(string reviewerEmployeeNo)
        {
            string reviewer = NormalizeReviewer(reviewerEmployeeNo);

            return string.IsNullOrWhiteSpace(reviewer)
                ? (object)DBNull.Value
                : reviewer;
        }

        /// <summary>
        /// Normalizes a DateTime to MySQL second precision.
        /// </summary>
        private static DateTime TrimToSecond(DateTime value)
        {
            return new DateTime(
                value.Year,
                value.Month,
                value.Day,
                value.Hour,
                value.Minute,
                value.Second,
                value.Kind);
        }

        /// <summary>
        /// Returns whether a review exception already carries safe concurrency or selection context.
        /// </summary>
        private static bool IsSafeReviewException(Exception exception)
        {
            if (!(exception is InvalidOperationException) ||
                string.IsNullOrWhiteSpace(exception.Message))
            {
                return false;
            }

            return exception.Message.IndexOf(
                       "Refresh",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   exception.Message.IndexOf(
                       "conflicting",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   exception.Message.IndexOf(
                       "selected answer no longer exists",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Rolls back an active transaction and preserves a rollback failure.
        /// </summary>
        private static void RollbackTransaction(
            MySqlTransaction transaction,
            string operationName,
            Exception originalException)
        {
            if (transaction == null)
                return;

            try
            {
                transaction.Rollback();
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Failed to roll back the " + operationName + " transaction.",
                    new AggregateException(originalException, rollbackException));
            }
        }

        #endregion

        #region Nested Public Results

        /// <summary>
        /// Describes current reusable truth and its propagation impact.
        /// </summary>
        public sealed class ReviewImpact
        {
            internal ReviewImpact(
                QuizAnswerType? currentTruth,
                int answerCount,
                int sessionCount)
            {
                CurrentTruth = currentTruth;
                AnswerCount = answerCount;
                SessionCount = sessionCount;
            }

            /// <summary>
            /// Gets current reusable GOOD/NG truth, or null when none exists.
            /// </summary>
            public QuizAnswerType? CurrentTruth { get; private set; }

            /// <summary>
            /// Gets matching answer count.
            /// </summary>
            public int AnswerCount { get; private set; }

            /// <summary>
            /// Gets distinct affected session count.
            /// </summary>
            public int SessionCount { get; private set; }
        }

        /// <summary>
        /// Describes one committed individual or bulk review operation.
        /// </summary>
        public sealed class ReviewOperationResult
        {
            internal ReviewOperationResult(
                int selectedRowCount,
                int uniqueImageCount,
                int updatedAnswerCount,
                int affectedSessionCount,
                int missingIdentityCount,
                int correctionCount)
            {
                SelectedRowCount = selectedRowCount;
                UniqueImageCount = uniqueImageCount;
                UpdatedAnswerCount = updatedAnswerCount;
                AffectedSessionCount = affectedSessionCount;
                MissingIdentityCount = missingIdentityCount;
                CorrectionCount = correctionCount;
            }

            public int SelectedRowCount { get; private set; }

            public int UniqueImageCount { get; private set; }

            public int UpdatedAnswerCount { get; private set; }

            public int AffectedSessionCount { get; private set; }

            public int MissingIdentityCount { get; private set; }

            public int CorrectionCount { get; private set; }

            public bool WasCorrection
            {
                get
                {
                    return CorrectionCount > 0;
                }
            }
        }

        #endregion

        #region Nested Private Types

        /// <summary>
        /// Captures one stable-image decision and loaded truth expectation.
        /// </summary>
        private sealed class ReviewGroup
        {
            public ReviewGroup(
                string imageHash,
                int sourceAnswerId,
                QuizAnswerType? expectedCurrentTruth,
                bool enforceExpectedTruth,
                bool allowExistingSameTruth)
            {
                ImageHash = ImageService.NormalizeImageHash(imageHash);
                SourceAnswerId = sourceAnswerId;
                ExpectedCurrentTruth = expectedCurrentTruth;
                EnforceExpectedTruth = enforceExpectedTruth;
                AllowExistingSameTruth = allowExistingSameTruth;
            }

            public string ImageHash { get; private set; }

            public int SourceAnswerId { get; private set; }

            public QuizAnswerType? ExpectedCurrentTruth { get; private set; }

            public bool EnforceExpectedTruth { get; private set; }

            public bool AllowExistingSameTruth { get; private set; }
        }

        /// <summary>
        /// Holds the minimum selected or matching answer state locked by a transaction.
        /// </summary>
        private sealed class LockedAnswer
        {
            public LockedAnswer(
                int answerId,
                int sessionId,
                string imageHash,
                string imageFileName,
                QuizAnswerType? correctAnswer)
            {
                AnswerID = answerId;
                SessionID = sessionId;
                ImageHash = imageHash;
                ImageFileName = imageFileName;
                CorrectAnswer = correctAnswer;
            }

            public int AnswerID { get; private set; }

            public int SessionID { get; private set; }

            public string ImageHash { get; set; }

            public string ImageFileName { get; set; }

            public QuizAnswerType? CorrectAnswer { get; private set; }
        }

        /// <summary>
        /// Holds one reusable truth row and its concurrency version.
        /// </summary>
        private sealed class TruthRecord
        {
            public TruthRecord(
                string imageHash,
                QuizAnswerType correctAnswer,
                DateTime reviewedAt,
                string reviewerEmployeeNo,
                int version)
            {
                ImageHash = imageHash;
                CorrectAnswer = correctAnswer;
                ReviewedAt = reviewedAt;
                ReviewerEmployeeNo = reviewerEmployeeNo;
                Version = version;
            }

            public string ImageHash { get; private set; }

            public QuizAnswerType CorrectAnswer { get; private set; }

            public DateTime ReviewedAt { get; private set; }

            public string ReviewerEmployeeNo { get; private set; }

            public int Version { get; private set; }
        }

        #endregion
    }
}
