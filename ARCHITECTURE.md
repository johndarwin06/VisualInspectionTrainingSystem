# ARCHITECTURE

## Configuration System

The application uses a single local XML configuration file for machine-specific settings:

- Tracked template: `DatabaseSettings.example.config`
- Ignored local file: `DatabaseSettings.local.config`
- Loader: `Services/ConfigurationService.cs`

`App.config` only points to the local configuration file name through `ApplicationSettingsFile`; it does not store database credentials or application folder paths.

## Settings Models

`ConfigurationService` exposes strongly typed settings:

- `ApplicationSettings`
- `DatabaseSettings`
- `PathSettings`

The service centralizes XML parsing, path normalization, required-value validation, directory checks, and MySQL connection string construction.

## Directory Validation

The configured quiz image folder is required to exist. The log, export, and report folders are output folders and are created automatically when missing and when the application has permission.

Startup validation occurs in `SystemInitializerService`. Quiz and admin image loading use the configured quiz image folder through `AppConstants.QuizImageFolder` for compatibility. Report CSV export uses the configured export folder.

## Database Transactions

Completed quiz persistence is coordinated by `SessionRepository.Save`. The session row and all related answer rows are inserted with the same `MySqlConnection` and `MySqlTransaction`; the transaction commits only after every insert succeeds.

Standalone answer batch persistence is coordinated by `AnswerRepository.SaveMany`. All answer rows in the batch use one connection and transaction.

Admin answer review is coordinated by `AnswerRepository.ReviewAnswer`. The selected answer row is locked, updated, and followed by parent session statistics recalculation in the same transaction. The parent session row is locked before recalculation.

Table creation checks run outside data transactions because MySQL DDL can cause implicit commits. Data-changing workflows roll back on exceptions and rethrow non-sensitive context.

## Connection Resiliency

MySQL connection resiliency is centralized in `Services/MySqlService.cs` and configured through the local XML configuration loaded by `ConfigurationService`.

`DatabaseSettings` includes:

- `ConnectionTimeoutSeconds`
- `RetryCount`
- `RetryDelayMilliseconds`

Each connection attempt uses the configured timeout in the generated MySQL connection string. Transient network and server availability failures are retried up to the configured retry count with a short configured delay. Authentication failures, invalid configuration, cancellation, and startup timeout failures are not retried.

Startup database validation runs through `SystemInitializerService` using the asynchronous MySQL connection test and a bounded cancellation token derived from the configured timeout and retry policy. Errors shown to startup flow are non-sensitive and do not include passwords or full connection strings.

## Global Error Handling

`App.xaml` routes WPF dispatcher exceptions to `App.xaml.cs`. The application also subscribes once per application instance to `TaskScheduler.UnobservedTaskException` and `AppDomain.CurrentDomain.UnhandledException`, and removes those subscriptions during normal exit.

A dispatcher exception is treated as fatal: the application records a sanitized diagnostic, shows one fixed non-sensitive message, marks the exception handled as part of controlled shutdown, and requests shutdown once. Reentrant or duplicate dispatcher notifications neither log another dispatcher failure nor show another dialog. Task scheduler exceptions are recorded and explicitly observed; AppDomain exceptions are recorded with their terminating classification.

`ApplicationErrorLogger` does not discover configuration while handling an error. After validated startup configuration is loaded, `SystemInitializerService` caches `PathSettings.LogFolder`. The logger first attempts that existing configured directory and falls back to `%LocalAppData%\VisualInspectionTrainingSystem\Logs` when the primary path is missing or unwritable. Logging errors are swallowed so they cannot recursively reach a global handler.

Each error entry is serialized under one process lock and includes a UTC timestamp, unique ID, handler source, termination status, exception type, redacted bounded message, stack trace when available, bounded inner-exception details, and flattened aggregate exception types. Credential-like values, full connection strings, tokens, and configuration secrets are redacted before storage.

## Splash Startup Flow

The splash screen is coordinated by `Views/Splash/SplashWindow.xaml.cs`, `ViewModels/SplashViewModel.cs`, and `Services/SystemInitializerService.cs`.

`SplashWindow` starts initialization from the WPF `Loaded` event after the splash is visible. `SplashViewModel` keeps one initialization task per splash instance, exposes progress, status, and diagnostic properties, and raises completion only once. Closing the splash or pressing Exit cancels the initialization token and prevents the login window from opening afterward.

`SystemInitializerService` returns an `InitializationResult` for required startup checks. Configuration loading runs off the UI thread and is guarded by a bounded wait because configuration discovery performs synchronous filesystem work. If configuration loading exceeds the required-startup timeout, startup fails safely with a non-sensitive timeout result.

MySQL validation reuses `MySqlService`, and the database check is also guarded by an outer bounded wait so startup cannot hang if the connector does not return promptly. The service does not duplicate MySQL retry policy; it only bounds the startup wait around the existing resiliency implementation.

Optional image inventory checks run behind their own short bounded wait. If the configured image folder is unavailable, throws a filesystem exception, or does not respond before the optional timeout, the inventory result is marked skipped and startup continues after required checks pass. Timed-out background tasks are observed so late exceptions do not become unhandled, and late optional work does not update splash UI state.

The login window is opened only by the splash window after a successful startup result, and `Application.Current.MainWindow` is updated to the login window before the splash closes.

## Quiz Image Lifecycle

`ImageService.LoadImages(string, bool)` remains the complete-catalog API used by administrator and inventory workflows. Trainee initialization uses the separate `LoadQuizImages(string, int)` API, which accepts only 10 or 20, loads complete metadata through the existing API without an initial shuffle, removes case-insensitive duplicate paths, applies one Fisher-Yates shuffle, and takes at most the requested count. Metadata sampling does not decode every bitmap. If fewer unique images are available, the sample contains every available image once; the default requested size is 10.

`HomeViewModel` exposes the supported 10/20 choices and owns the selected value. Home passes that value explicitly through `QuizWindow` to `QuizViewModel`; unsupported values are rejected before normal quiz initialization. `QuizViewModel` remains the owner of quiz display state and continues to submit answers only through `QuizEngine`. It builds progress, completion, results, and persistence from the actual sample count, so fewer-image sessions never duplicate questions or persist a requested count that was not used.

`QuizViewModel` loads the active bitmap off the WPF UI thread and preloads only one upcoming image. The cache is a two-entry least-recently-used cache that retains the current and upcoming images; it never loads an entire quiz into memory.

Each bitmap is read into memory, decoded with `BitmapCacheOption.OnLoad`, and frozen before it is shared with the UI or cache. This releases the source file without reducing image decode fidelity. The ViewModel attaches a cancellation token and generation value to image work, observes task failures, and ignores late completion after a question changes or the window closes.

Answer commands are enabled only when the active bitmap is ready. A failed active-image load stops and cleans up the incomplete quiz without persisting it as completed. `QuizWindow` routes local G and N input through the existing commands, prevents repeated/queued keyboard input from becoming a second answer, and retains one Escape exit confirmation. It owns and disposes its ViewModel once when closed.

Quiz progress is derived from accepted answers: `CurrentQuestion`, `TotalQuestions`, `AnsweredQuestions`, `RemainingQuestions`, and `CompletionPercentage` are synchronized by the ViewModel. The percentage is zero before the first accepted answer, 100 after the final accepted answer, and `AnsweredQuestions + RemainingQuestions` always equals `TotalQuestions`.

## Result Module

`ResultWindow(List<QuizAnswer>)` remains the quiz-to-result entry point. `ResultViewModel` immediately passes the supplied answers to `StatisticsService`, which clones each non-null answer into a read-only `ResultStatistics` snapshot. The result module never writes answers, assigns `CorrectAnswer`, or persists a session; administrator truth remains owned by the Admin module.

Answer distribution uses all snapshot rows with valid user GOOD or NG selections. GOOD and NG percentages divide their respective counts by total snapshot answers. Timing includes only finite, non-negative elapsed values; total, average, fastest, and slowest use that valid-timing subset, and missing timing is displayed as N/A where appropriate.

Review coverage divides reviewed answers by total answers. An answer is reviewed only when `CorrectAnswer` contains a supported GOOD or NG value. Reviewed accuracy divides matching user/truth answers by reviewed answers, never total answers. Pending answers are shown as Pending Review, remain available for distribution and timing, and are excluded from correct and wrong counts.

NG analysis distinguishes trainee selection from reviewed truth:

- User NG rate is trainee NG selections divided by total answers.
- Correctly detected NG is user NG with reviewed truth NG.
- False NG is user NG with reviewed truth GOOD.
- Missed NG is user GOOD with reviewed truth NG.
- NG detection rate is correctly detected NG divided by reviewed actual NG.
- False NG rate is false NG divided by reviewed actual GOOD.

Zero reviewed-truth denominators display N/A rather than a misleading percentage. The All, Reviewed Wrong, User NG, and Pending Review filters replace only the displayed read-only collection and never mutate the statistics snapshot.

The ResultWindow uses native labeled WPF bars for user answer distribution, reviewed correct/wrong outcomes, reviewed/pending coverage, reviewed accuracy, NG detection, and false-NG rate. Every visual also presents its metric name, count, and percentage; zero values remain bounded and pending reviewed accuracy displays Pending Review.

Selected-answer preview uses the shared `ImageService` decoder. It reads the requested file on a worker task, uses `BitmapCacheOption.OnLoad`, freezes the bitmap, and releases the source stream before publication. `ResultViewModel` keeps only one preview, cancels the previous selection token, checks a generation and selected-answer identity, observes task completion, and disposes preview work when the window closes. Missing, unreadable, deleted, or corrupt images produce a fixed non-sensitive unavailable status.

## Dashboard Analytics

`DashboardRepository` calculates the five dashboard cards for one local calendar day using caller-supplied `@DayStart` and `@DayEnd` parameters. The range is half-open: `StartTime >= @DayStart` and `StartTime < @DayEnd`. SQL does not apply `DATE()` or another function to `StartTime`, preserving index-friendly filtering.

Session metrics and answer metrics are calculated in separate aggregate subqueries and combined only after each produces one row. This prevents the session-to-answer relationship from multiplying session durations. Today's Training counts only sessions with an `EndTime`; Time Spent sums only completed rows whose end is not earlier than their start. Incomplete and negative durations contribute no time.

Answer distribution counts normalized supported trainee GOOD and NG selections for sessions started in the selected day, including valid pending review selections. An answer is reviewed only when `UPPER(TRIM(CorrectAnswer))` is supported GOOD or NG. Correct requires a supported normalized `UserAnswer` that matches the supported truth; a supported truth with a null, unsupported, or mismatching user answer is reviewed wrong. Null, empty, whitespace, and unsupported truth values remain pending and never count as wrong. A zero reviewed denominator maps to null so the ViewModel displays N/A.

`DashboardViewModel` loads the metric snapshot and deterministic recent-session list on a worker task so normal WPF navigation remains responsive. One busy flag disables repeated command refresh, and successful refresh replaces the entire recent collection instead of appending rows. A failed refresh records the technical exception through `ApplicationErrorLogger`, clears stale values, and exposes only a fixed non-sensitive status message.

## Repository Validation

Repository public methods validate parameters before opening MySQL connections wherever applicable. Numeric identities must be greater than zero, EmployeeNo values must be present, answer collections must be non-null and contain no null elements, answer values must be GOOD or NG, completed sessions must have valid start/end ordering, and report date ranges must be ordered.

Completed quiz persistence still runs through `SessionRepository.Save` and `AnswerRepository.SaveMany` in one transaction. Before inserting the session header, `SessionRepository` checks for an existing completion with the same EmployeeNo, StartTime second, EndTime second, TotalQuestions, and answer count. When that duplicate rule matches, the transaction rolls back and no new session or answers are saved.

The duplicate rule is also enforced by the database. `SessionRepository.EnsureTable` upgrades `tbl_training_session` with a nullable `DuplicateKey VARCHAR(64)` column and the unique index `UX_tbl_training_session_DuplicateKey`. The key is a SHA-256 hash of EmployeeNo, StartTime second, EndTime second, TotalQuestions, and answer count. Existing unique historical completion rows are backfilled before the index is created; historical duplicate groups are left with a null `DuplicateKey` so the unique index can be created without deleting data. A legacy duplicate lookup remains in the save transaction to reject new saves that match those historical null-key rows.

Reviewed truth is represented only by normalized supported GOOD or NG `CorrectAnswer` values. Null, empty, whitespace, and unsupported truth values remain pending. Dashboard correctness requires a supported normalized `UserAnswer` and `CorrectAnswer` that match; a supported truth with a null, unsupported, or mismatching user answer is reviewed wrong, while unsupported truth never counts as wrong.

Report session rows aggregate answer data directly instead of relying only on stored summary columns, which keeps pending answers out of wrong-answer totals even when old session rows have stale summary values.

Dashboard metrics, report summaries, and admin review session recalculation use conditional aggregation to reduce repeated scans while preserving existing total, pending, reviewed, correct, wrong, and accuracy meanings.
