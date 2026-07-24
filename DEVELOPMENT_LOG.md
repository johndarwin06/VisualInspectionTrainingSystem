# DEVELOPMENT LOG

## 2026-07-24

### Issue #12 - Reports

- Implemented explicit Daily, true Monday-to-Sunday This Week, rolling Last 7 Days, This Month, inclusive Custom, and All Dates periods. Every bounded query uses parameterized `StartTime >= @StartTime` and `StartTime < @EndTime` predicates without applying a date function to `StartTime`.
- Reworked report SQL to aggregate answers before joining sessions, preserving deterministic `StartTime DESC, SessionID DESC` ordering without multiplying session totals. Only normalized GOOD or NG truth is reviewed; malformed truth is pending, valid truth with a missing/invalid/mismatching trainee answer is wrong, and no reviewed denominator maps to N/A.
- Retained a 500-row interactive list with explicit limit disclosure and added a separate complete export snapshot with a 10,000-session safeguard so CSV, Excel, and PDF never silently inherit the display limit.
- Added asynchronous refresh and export coordination with disabled overlapping commands, operation generations, stale-result rejection, observed abandoned tasks, lifecycle cancellation, fixed non-sensitive UI messages, and `ApplicationErrorLogger` diagnostics.
- Added `DocumentFormat.OpenXml` 3.5.1 for real three-sheet `.xlsx` documents and `PDFsharp-WPF` 6.2.4 for real A4 landscape multipage PDFs. Both packages support .NET Framework 4.8.1, work with `packages.config` and C# 7.3, and use the MIT license.
- Passed the temporary Issue #12 probe with 240 assertions: periods 25, models 10, Result regression 16, quiz regressions 76, Administration regression 8, MySQL reports 18, independent SQL 5, Dashboard parity 9, normalization 10, boundaries 8, CSV 10, Excel 6, PDF 4, N/A exports 3, export safety 3, ViewModel 11, ViewModel safety 17, and cleanup 1.
- Corrected draft PR #49 so each display or export snapshot reads its summary and session rows on one MySQL connection inside one repository-owned `RepeatableRead` transaction. The repository commits only after constructing the in-memory snapshot, rolls back incomplete reads, and closes the transaction and connection before any export generation.
- Passed 426 temporary correction assertions, including deterministic answer-review and session-insertion changes between the logical reads, matching display/export totals, `RepeatableRead` verification, rollback and connection cleanup, the 500-row disclosure, the 10,000-session safeguard, Dashboard semantics, Result and quiz regressions, and CSV/XLSX/PDF validation. The temporary concurrency hook was removed; the final repository then passed 394 seam-free regression assertions.
- Controlled MySQL verification covered daily/weekly/monthly/custom/all-date periods, completed and open sessions, multiple trainees, correct and wrong GOOD/NG answers, null/empty/whitespace/unsupported/lowercase/padded values, zero-reviewed behavior, independent aggregate comparison, and deterministic ordering. Cleanup reported zero residual probe sessions.
- Visible WPF acceptance passed administrator login, exactly one Reports window, every period action, empty and invalid ranges, N/A accuracy, repeated Refresh, a real close during a blocked database refresh, all save-dialog cancellations, CSV/XLSX/PDF generation and opening, four-page PDF rendering, Administration and Dashboard regressions, normal Reports close, and normal application shutdown.
- Correction verification visibly matched the controlled Today and This Week reports to independent MySQL values (1 session, 6 questions, 4 reviewed, 2 pending, 2 correct, 2 wrong, and 50.00% reviewed accuracy), opened the real three-sheet export in Excel, rendered the real PDF without layout defects, returned safely to Administration, and shut down normally.
- Final verification removed temporary database data, report files, render output, lock probes, and generated artifacts. Issue #12 Reports is implemented and verified on `issue-12-reports` and is awaiting pull-request review and merge; GitHub issue #16 remains open.

## 2026-07-23

### Issue #46 Post-Merge Finalization

- PR #47 merged Configurable Quiz Sample Size into `main` at merge commit `a13fbbea4d6d0ff27201a9378bca5109c259298c`, delivering feature commit `493848b1dda83cff0b361bd0a5e89facc1b4fa51` and navigation correction `b3f84152219ac60ffe1343f1fda4c98671d82f1f`.
- GitHub automatically closed Issue #46 as completed, and the remote feature branch was deleted after the merge.
- The navigation correction makes `HomeViewModel` validate and raise the training-navigation event while `HomeWindow` exclusively creates `QuizWindow`. One active quiz is permitted; Home hides during training and returns after completion or cancellation.
- The correction passed 280 focused assertions in addition to the original 1,140-assertion feature suite. Visible verification passed rapid-click duplicate prevention, selected sizes 10 and 20, Home restoration, early cancellation, one ResultWindow after completion, and normal shutdown.
- Local `main` was fast-forwarded to the merge commit, the local `issue-46-quiz-sample-size` branch was deleted, and this documentation branch was created from the merged baseline.
- The protected `stash@{0}` remained untouched. No Reports production work was started.

### Configurable Quiz Sample Size

- Created and assigned GitHub issue #46, `Feature - Configurable Quiz Sample Size`, then implemented it on `issue-46-quiz-sample-size` from `origin/main` commit `07fb441fb5262c10a189650c41affd64f6bd5e79`.
- Preserved the public `ImageService.LoadImages(string, bool)` signature and complete-catalog behavior. Added a quiz-only API that accepts only 10 or 20, rejects unsupported values before folder access, removes case-insensitive duplicate paths, performs one Fisher-Yates shuffle, and takes a bounded metadata sample.
- Added a native MVVM-bound 10/20 selector to Home with a default of 10 and passed the selected size explicitly through `QuizWindow` into `QuizViewModel`.
- Kept progress, completion, ResultWindow totals, session totals, and answer persistence driven by the actual sampled image count. Valid requests use every available unique image when fewer than requested are present and show a fixed non-sensitive notice.
- Preserved the current/upcoming two-bitmap cache and unrestricted administrator catalog loading; `AdminViewModel` was not changed and does not use the quiz sampler.
- Built Debug after every modified C# and XAML file. Final Debug and Release rebuilds each passed with 0 errors and the same 3 existing warning lines: one `MVVMTKCFG0002` line and two `CS0067` lines from temporary and final WPF compilation.
- Passed a temporary deterministic configurable-quiz probe with 1,140 assertions: ImageService 828, Home selection 15, quiz progress/completion 212, cache/cancellation 25, session/answer persistence 42, administrator inventory/preview 4, and login/Result/Dashboard regressions 14.
- Visible WPF acceptance passed normal startup to Login, trainee login, the Home 10/20 options and default, real 10- and 20-question quizzes, all images visibly distinct within each quiz, dynamic progress, exact final completion, one ResultWindow per quiz, Result totals of 10 and 20, and safe early cancellation.
- A temporary MySQL probe passed 9 assertions for the visible sessions: `TotalQuestions`, answer rows, and distinct image IDs were 10/10/10 and 20/20/20; the cancelled quiz created no incomplete session.
- Visible administrator acceptance passed unrestricted access to all 30 created answer rows, selected-image remapping between image IDs 8 and 18, Dashboard navigation, existing Reports navigation without modification, normal window closing, and normal application shutdown. The configured folder contained 20 BMP files, while the controlled 105-image automated catalog remained unrestricted.
- Controlled visible fewer-image folder tests were not run because they require preparing a separate local configured folder. Deterministic automation passed 7-of-10, 14-of-20, empty-folder, missing-folder, uniqueness, progress, result, and persistence behavior.
- Removed the two visible acceptance sessions and all 30 answer rows, verified zero remnants, and removed all temporary probe sources and executables. The known `stash@{0}` remained untouched and Reports was not modified.
- At the end of the initial implementation run, Issue #46 awaited pull-request review and merge; the post-merge finalization entry above records its subsequent completion through PR #47.

## 2026-07-19

### Dashboard Analytics

- Replaced the dashboard's all-time summary cards with five consistently scoped local-day metrics: Today's Training, reviewed-only Average Accuracy, Time Spent, GOOD Count, and NG Count.
- Used parameterized half-open boundaries (`StartTime >= @DayStart` and `StartTime < @DayEnd`) instead of applying a date function to the indexed session column.
- Kept session duration aggregation separate from answer aggregation so joining answer rows cannot multiply completed-session time.
- Excluded null, negative, and malformed completed durations; returned N/A for a zero reviewed-answer denominator; and retained pending trainee GOOD/NG selections without counting them as wrong.
- Preserved the deterministic recent-session order and limit, safe null mapping, existing public dashboard properties, and native WPF styling.
- Moved dashboard loading off the dispatcher, disabled repeated refresh while busy, replaced rather than appended recent rows, cleared stale values after failure, logged technical exceptions, and showed only a fixed non-sensitive error status.
- Passed the controlled MySQL dashboard probe with 49 assertions covering the expected 1 session, 10 minutes, GOOD 3, NG 3, reviewed 4, correct 2, wrong 2, and 50% reviewed accuracy, plus yesterday exclusion, incomplete sessions, empty days, half-open boundaries, invalid durations, refresh deduplication, safe failures, and recent ordering/limit.
- Passed the Result Module regression probe with 76 assertions and the Issue #9 regression probe with 29 assertions.
- Built Debug with 0 errors and 1 existing warning and Release with 0 errors and 3 existing warnings.
- In a visible WPF run, administrator login and normal navigation opened exactly one Dashboard. The live MySQL-backed values were 1 completed session, N/A reviewed accuracy with 0 reviewed and 10 pending, 4 seconds, GOOD 3, and NG 7. Refresh preserved the same values and newest-first rows without duplication, and closing Dashboard returned safely to Administration.
- The trainee quiz and ResultWindow workflow was covered by automated regressions but was not rerun visibly during this dashboard session. Computer control stopped after detecting user input during the separate application-close attempt.

### Issue #10 Result Module Acceptance Finalization

- Confirmed merged PR #41 delivered the Result Module and that `origin/main` contains diagnostic-dialog correction commit `bee4eb0`.
- Recorded successful real WPF acceptance: login, ten-question quiz completion, removal of the unexpected diagnostic dialog, exactly one ResultWindow, pending statistics and filters, selected-image preview, window closing, and no observed runtime error.
- Passed the controlled reviewed six-answer ResultWindow dataset with 6 total answers, 3 user GOOD, 3 user NG, 4 reviewed, 2 pending, 2 correct reviewed, 2 wrong reviewed, and 50% reviewed accuracy.
- Confirmed ResultWindow filters returned All 6, Reviewed Wrong 2, User NG 3, and Pending Review 2. NG analysis returned 2 reviewed actual NG, 1 detected NG, 1 false NG, 1 missed NG, a 50% detection rate, and a 50% false-NG rate.
- Confirmed the MySQL completed attempt contained exactly one session row and ten answer rows with five GOOD, five NG, and all ten pending administrator review.
- Reconfirmed that pending answers are never treated as wrong and remain excluded from reviewed-only accuracy and NG truth classifications.
- Passed the focused Result Module probe with 76 assertions, including WPF ResultWindow instantiation, display, dispatcher pumping, closing, and binding diagnostics.
- Passed the Issue #9 image regression probe with 29 assertions covering the bounded current/next cache, progress, input paths, stale-image prevention, source-file release, and close-during-load cleanup.
- Rebuilt Debug and Release with 0 errors and the same 3 existing warnings in each configuration.
- Removed all ignored temporary Result Module and Issue #9 probe sources and executables after verification.
- Issue #10 is implemented, manually accepted, and ready for finalization. Issue #11 Dashboard Analytics is the next planned task and has not started.

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
- At the end of this implementation entry, Issue #10 was awaiting manual application testing; that acceptance completed successfully on 2026-07-19. Issue #9 was merged as PR #40 and is complete.

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
