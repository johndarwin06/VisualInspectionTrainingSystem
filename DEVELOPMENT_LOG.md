# DEVELOPMENT LOG

## 2026-07-18

### Result Module

- Moved cohesive result calculations into `StatisticsService` and added an immutable `ResultStatistics` snapshot so caller list or answer mutations cannot silently alter a displayed result.
- Added total, GOOD/NG distribution, review coverage, reviewed-only accuracy, valid timing, and NG-classification metrics with safe zero denominators and pending-review handling.
- Defined reviewed accuracy as correct reviewed answers divided by reviewed answers. Pending answers remain available for distribution and timing but are excluded from correct, wrong, detection, and false-NG classifications.
- Added read-only All, Reviewed Wrong, User NG, and Pending Review filters without modifying the source snapshot.
- Added a selected-answer detail view with one asynchronous detached preview. The shared decoder uses `BitmapCacheOption.OnLoad` and `Freeze`; cancellation, selection generation, and disposal checks prevent stale or post-close publication.
- Added native labeled WPF bars for GOOD/NG distribution, reviewed/pending coverage, reviewed correct/wrong outcomes, reviewed accuracy, NG detection, and false-NG rate. Each visual includes text rather than relying on color alone.
- Preserved the existing `ResultWindow(List<QuizAnswer>)` entry point, read-only result behavior, transactional save flow, and Issue #9 current/next cache.
- Built Debug after each completed C# or XAML change with 0 errors and only the existing Toolkit configuration and unused Home event warnings.
- Passed a temporary Result Module probe with 76 assertions covering explicit empty, pending, reviewed-correct, mixed, and invalid-timing datasets; snapshot isolation; filters; preview detachment and file release; missing/corrupt images; rapid selection; close cancellation; WPF window lifecycle; and binding diagnostics.
- Passed a temporary Issue #9 decoder regression probe with 29 assertions covering current and next loading, the two-entry cache bound, eviction, stale-image prevention, G/N action paths, progress, file release, and close-during-load cleanup.
- The automated probe instantiated, showed, pumped, and closed the real `ResultWindow` class with a controlled reviewed dataset. Full splash, login, quiz, MySQL persistence, and human visual checks in the real application remain to be run before merge.
- Issue #10 is awaiting draft pull request review and required manual application testing. Issue #9 was merged as PR #40 and is complete.

### Quiz Optimization

- Reworked quiz bitmap display so the active image is decoded off the WPF UI thread and only the active image plus one upcoming image are retained in a bounded two-entry least-recently-used cache.
- Bitmap loading reads the source into memory, uses `BitmapCacheOption.OnLoad`, and freezes each image before it crosses threads, releasing source files after successful or failed decode attempts.
- Added cancellation, generation checks, cache clearing, and one-time `IDisposable` cleanup so stale preload completion cannot replace a newer question or survive window closure.
- Corrected a post-decode cleanup race: active and upcoming image continuations now validate cancellation, disposal, generation, quiz state, and image ownership before caching, then validate again while holding the cache lock so cleanup cannot be followed by a stale cache insertion.
- Disabled answer commands while the active image is loading, unavailable, completed, or shutting down. Missing or corrupt images stop the incomplete quiz without saving it as completed.
- Hardened local G, N, and Escape handling to reuse existing commands, ignore repeat and queued shortcut input, handle the key once, and retain one exit confirmation.
- Added bindable question, total, answered, remaining, and answered-percentage values. The percentage is derived only from accepted answers and ranges from 0% before the first answer to 100% after the last answer.
- Verified bitmap detachment, missing/corrupt image failures, bounded cache eviction and cleanup, cache reuse, progress boundaries, duplicate protection, and incomplete-session save guarding with a temporary non-UI probe.
- Verified the active and upcoming cache-continuation cleanup races with a temporary non-UI probe that paused each continuation at cache validation, disposed the view model, and confirmed the settled task could not alter image state, status, progress, or the cleared cache. The probe also reconfirmed normal current/next caching, frozen image file release, decode failures, and pending-review engine state.
- Launched the real WPF application after correcting a discovered progress-binding error and observed the splash and Home windows remain running without a new application-error log. User input then occurred before login or quiz interaction could be verified; interactive quiz verification remains required.

### Global Error Handling

- Added controlled application-wide WPF dispatcher, task scheduler, and AppDomain exception handling.
- Dispatcher failures are logged once, show only a fixed non-sensitive message, are marked handled during controlled shutdown, and cannot open duplicate dialogs.
- Cached the validated configured log folder during startup configuration loading so fatal handlers do not perform configuration discovery.
- Added `%LocalAppData%\VisualInspectionTrainingSystem\Logs` as the safe fallback when the configured log folder is unavailable or unwritable.
- Added bounded, serialized diagnostic entries with UTC timestamps, unique error IDs, handler category, exception type, sanitized message, bounded stack trace, bounded inner exceptions, aggregate exception types, and termination classification.
- Redacted connection strings, passwords, user identifiers, tokens, API keys, and configuration-secret values from diagnostics.
- Restored deterministic optional-image inventory probes and made caller and startup cancellation win over a stalled optional filesystem operation.
- Built Debug and Release successfully with 0 errors and 3 existing warnings in each configuration.
- Verified logger formatting, redaction, configured-folder logging, concurrent writes, contained logger failure, task observation, AppDomain termination classification, optional image timeout/cancellation, required configuration failure, database timeout, and dispatcher responsiveness with temporary probes.
- Verified an actual terminating AppDomain exception in a bounded child probe. The test-only child set `SEM_NOGPFAULTERRORBOX`, exited with the expected non-zero CLR fatal code, and wrote exactly one terminating entry; this expected probe termination is distinct from a normal application failure.
- The sandbox did not expose the real WPF windows reliably, so a manual interactive splash/login launch and visible dispatcher fatal-dialog confirmation remain required outside this environment.

## 2026-07-14

### Splash Timeout Hardening

- Reworked `SystemInitializerService` optional image inventory so synchronous filesystem work is guarded by a true bounded wait.
- Added timeout handling that marks optional image inventory as skipped and allows startup to continue after required checks pass.
- Added abandoned-task observation for timed-out configuration, database, and image inventory tasks so later exceptions are consumed.
- Hardened configuration loading with the same bounded-wait pattern because local configuration discovery also performs synchronous filesystem work.
- Preserved the existing MySQL connection timeout and retry implementation in `MySqlService` without duplicating retry behavior.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug after the modified C# service file.
- Verified normal local image inventory, missing image folder handling, filesystem exception handling, optional image timeout, and cancellation during image inventory with temporary probes.
- Verified normal startup, required configuration failure, and existing database timeout behavior with temporary probes.
- Verified the WPF splash dispatcher remained responsive and opened exactly one login window when the optional image inventory timed out.
- Removed all temporary probe files and local placeholder configs after testing.

### Splash Screen Improvement

- Reworked `SystemInitializerService` so startup initialization runs asynchronously with cancellation support and duplicate initialization protection.
- Reused `MySqlService` for startup database resiliency and added an outer bounded wait so startup cannot hang when a MySQL handshake does not return promptly.
- Added non-sensitive startup result and diagnostic models for success, cancellation, timeout, configuration failure, service failure, and unexpected startup failure states.
- Updated `SplashViewModel` so initialization starts from the splash window lifecycle instead of the constructor, exposes status/diagnostics, and prevents duplicate completion events.
- Updated `SplashWindow` lifecycle handling so startup begins after the window is visible, close requests cancel initialization, and the login window opens exactly once.
- Updated the splash XAML to show diagnostics and an Exit action while keeping standard WPF controls and inline styling.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug after each modified C# file.
- Final build succeeded with 0 errors and 1 warning.
- Verified normal startup with valid local configuration and MySQL access using a temporary startup probe.
- Verified missing configuration, malformed configuration, unavailable database, bounded startup timeout, unexpected initialization exception handling, duplicate initialization prevention, and close-during-initialization cancellation with temporary probes.
- Verified the WPF splash dispatcher remained responsive and opened exactly one login window with a temporary WPF probe.
- Removed all temporary probe files and local placeholder configs after testing.

## 2026-07-13

### Repository Validation Hardening

- Updated `UserRepository` so null or malformed `IsActive` data maps to inactive instead of active.
- Confirmed `AuthenticationService` rejects a database user whose `IsActive` value is null.
- Added a nullable `DuplicateKey` column and unique index for the completed-session duplicate rule.
- Added duplicate-key migration handling that backfills only unique historical completion rows before creating the unique index.
- Preserved the existing transaction that saves a session header and all quiz answers atomically.
- Added duplicate-key handling that reports a clear non-sensitive duplicate-completion message.
- Rewrote `AnswerRepository.RecalculateSession` to update correct, wrong, and accuracy values from one conditional aggregate.
- Rewrote `DashboardRepository.GetMetrics` to consolidate session and answer scans.
- Rewrote `ReportRepository.GetSummary` to consolidate filtered session and answer totals.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug after each modified C# repository file.
- Verified null activation rejection, simultaneous duplicate save handling, forced answer insert rollback, pending answer loading, dashboard metrics, and report summary totals with a temporary repository hardening probe.

### Repository Validation

- Reviewed all repository classes for invalid parameters, unsafe null handling, duplicate persistence risks, `SELECT *` usage, deterministic ordering, and SQL parameterization.
- Rewrote `SessionRepository` validation for completed sessions, answer collections, score totals, completion dates, and already-saved sessions.
- Added database-backed duplicate completed-session detection inside the existing quiz save transaction.
- Rewrote `AnswerRepository` validation so null answer collections, null answer elements, invalid ImageID values, invalid GOOD/NG values, inconsistent reviewed answers, and invalid answer timing are rejected before SQL.
- Updated answer mapping so pending `CorrectAnswer` values load as null and are not counted as wrong.
- Updated admin review recalculation to require exactly one affected answer/session row and keep pending answers out of wrong-answer totals.
- Updated `UserRepository` to validate EmployeeNo and password-hash inputs before SQL and to map nullable user columns safely.
- Updated `DashboardRepository` to parameterize the recent-session limit and stop replacing required session values with misleading defaults.
- Updated `ReportRepository` to validate date ranges and calculate correct, wrong, pending, reviewed, and accuracy values from answer aggregates.
- Updated `ImageRepository` folder validation and deterministic file ordering before optional shuffling.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug after repository changes.
- Verified invalid input validation, successful quiz save, duplicate completion blocking, pending review loading, transaction rollback on forced answer insert failure, and normal connection behavior with a temporary repository probe.

### Connection Resiliency

- Added configurable MySQL connection timeout, retry count, and retry delay settings to the local XML configuration.
- Updated `ConfigurationService` to parse, validate, and apply connection resiliency settings when building the MySQL connection string.
- Rewrote `MySqlService` connection opening to use a limited retry policy for transient connection failures.
- Added asynchronous connection testing with cancellation support for startup checks.
- Prevented retries for configuration errors and detected authentication/setup failures.
- Updated `SystemInitializerService` so the splash startup database check awaits a bounded asynchronous connection test instead of blocking indefinitely.
- Added safe, non-sensitive connection failure messages without exposing passwords or full connection strings.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug.
- Final build succeeded with 0 errors and 1 warning.
- Verified valid database connectivity with a temporary probe.
- Verified invalid host retry behavior, simulated stopped MySQL behavior through an unavailable local port, bounded timeout behavior, invalid credential non-retry behavior, and invalid retry configuration handling with temporary probes.
- Launched the WPF application from the Debug build output and stopped it after confirming the process stayed running for 8 seconds.

### Database Transactions

- Updated completed quiz persistence so the session header and all answer rows are saved in one MySQL transaction.
- Moved table creation checks outside data transactions to avoid MySQL implicit DDL commits.
- Updated standalone answer batch persistence to use one MySQL transaction.
- Updated admin answer review so the answer update and parent session statistics recalculation use the same connection and transaction.
- Added transaction-aware repository methods that accept `MySqlConnection` and `MySqlTransaction`.
- Added row locking for admin review lookups and parent session recalculation.
- Added rollback behavior with non-sensitive exception context.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug.
- Verified successful quiz save, forced answer persistence rollback, successful admin review, and forced recalculation rollback with a temporary transaction probe.

### Configuration System

- Replaced the empty `ConfigurationService` with a strongly typed XML configuration loader.
- Added `ApplicationSettings`, `DatabaseSettings`, and `PathSettings` models.
- Removed MySQL connection settings from `App.config`.
- Added `DatabaseSettings.example.config` as the safe tracked template.
- Kept the real local file name as `DatabaseSettings.local.config` and ensured it remains ignored by Git.
- Removed the unused `Configuration\Settings.json` file to avoid a second unrelated configuration system.
- Replaced hardcoded quiz image folder access with configured path access.
- Updated startup validation to load application settings and validate configured directories before continuing.
- Required the quiz image folder to exist.
- Automatically creates the configured log, export, and report folders when safe.
- Updated report CSV export to start in the configured export folder.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug.
- Final build succeeded with 0 errors and 1 warning.
- Verified valid path configuration, quiz image loading, output directory creation, missing quiz folder handling, invalid path handling, and WPF startup launch with a temporary ignored local configuration.

### Secure Configuration

- Removed MySQL connection details from `App.config`.
- Added workstation-local database settings loading through `DatabaseSettings.local.config`.
- Added `DatabaseSettings.example.config` with placeholder values only.
- Updated `MySqlService` to build the MySQL connection string through `ConfigurationService`.
- Kept the existing repository and service APIs unchanged.
- Added a clear missing or invalid configuration error that does not include the database password.
- Added explicit ignore coverage for `DatabaseSettings.local.config`.
- Added local setup instructions to `README.md`.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug.
- Final build succeeded with 0 errors and 1 warning.
- Verified missing local configuration handling with a temporary probe.
- Verified invalid local configuration handling with a temporary probe.
- Launched the WPF application from the Debug build output and stopped it after startup validation.
- Full valid-credential login validation was blocked because no accepted local MySQL password is available in this shell.
- Sanitized Git history check found older `App.config` commits containing a redacted `Pwd=` value; history was not rewritten automatically.

## 2026-07-12

### User Password Hashing and Migration

- Added BCrypt.Net-Next 4.2.0 for .NET Framework password hashing.
- Added `PasswordHashService` for BCrypt hash creation, hash detection, and verification.
- Updated `AuthenticationService` to verify BCrypt hashes, support temporary plain-text login, and migrate plain-text passwords after successful login.
- Added parameterized `UserRepository.UpdatePasswordHash`.
- Preserved the public login API.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug.
- Build succeeded with 0 errors and 1 warning.
- Tested a temporary plain-text database user:
  - Plain-text login succeeded.
  - Stored value migrated to BCrypt.
  - BCrypt login succeeded.
  - Existing BCrypt value was not rehashed on second login.
  - Incorrect password failed.
  - Incorrect password did not update the stored value.
- Launched the WPF application from the Debug build output and stopped it after startup validation.
