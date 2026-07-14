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

## Splash Startup Flow

The splash screen is coordinated by `Views/Splash/SplashWindow.xaml.cs`, `ViewModels/SplashViewModel.cs`, and `Services/SystemInitializerService.cs`.

`SplashWindow` starts initialization from the WPF `Loaded` event after the splash is visible. `SplashViewModel` keeps one initialization task per splash instance, exposes progress, status, and diagnostic properties, and raises completion only once. Closing the splash or pressing Exit cancels the initialization token and prevents the login window from opening afterward.

`SystemInitializerService` returns an `InitializationResult` for required startup checks. Configuration loading runs off the UI thread and is guarded by a bounded wait because configuration discovery performs synchronous filesystem work. If configuration loading exceeds the required-startup timeout, startup fails safely with a non-sensitive timeout result.

MySQL validation reuses `MySqlService`, and the database check is also guarded by an outer bounded wait so startup cannot hang if the connector does not return promptly. The service does not duplicate MySQL retry policy; it only bounds the startup wait around the existing resiliency implementation.

Optional image inventory checks run behind their own short bounded wait. If the configured image folder is unavailable, throws a filesystem exception, or does not respond before the optional timeout, the inventory result is marked skipped and startup continues after required checks pass. Timed-out background tasks are observed so late exceptions do not become unhandled, and late optional work does not update splash UI state.

The login window is opened only by the splash window after a successful startup result, and `Application.Current.MainWindow` is updated to the login window before the splash closes.

## Repository Validation

Repository public methods validate parameters before opening MySQL connections wherever applicable. Numeric identities must be greater than zero, EmployeeNo values must be present, answer collections must be non-null and contain no null elements, answer values must be GOOD or NG, completed sessions must have valid start/end ordering, and report date ranges must be ordered.

Completed quiz persistence still runs through `SessionRepository.Save` and `AnswerRepository.SaveMany` in one transaction. Before inserting the session header, `SessionRepository` checks for an existing completion with the same EmployeeNo, StartTime second, EndTime second, TotalQuestions, and answer count. When that duplicate rule matches, the transaction rolls back and no new session or answers are saved.

The duplicate rule is also enforced by the database. `SessionRepository.EnsureTable` upgrades `tbl_training_session` with a nullable `DuplicateKey VARCHAR(64)` column and the unique index `UX_tbl_training_session_DuplicateKey`. The key is a SHA-256 hash of EmployeeNo, StartTime second, EndTime second, TotalQuestions, and answer count. Existing unique historical completion rows are backfilled before the index is created; historical duplicate groups are left with a null `DuplicateKey` so the unique index can be created without deleting data. A legacy duplicate lookup remains in the save transaction to reject new saves that match those historical null-key rows.

Pending review answers are represented by `CorrectAnswer IS NULL`. Repository statistics count correct answers only when `CorrectAnswer IS NOT NULL AND IsCorrect = 1`, and wrong answers only when `CorrectAnswer IS NOT NULL AND IsCorrect = 0`.

Report session rows aggregate answer data directly instead of relying only on stored summary columns, which keeps pending answers out of wrong-answer totals even when old session rows have stale summary values.

Dashboard metrics, report summaries, and admin review session recalculation use conditional aggregation to reduce repeated scans while preserving existing total, pending, reviewed, correct, wrong, and accuracy meanings.
