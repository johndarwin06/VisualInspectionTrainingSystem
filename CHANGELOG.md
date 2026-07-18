# CHANGELOG

## Unreleased

### Result Module

- Delivered the Result Module through merged PR #41.
- Removed the accidental quiz-startup diagnostic dialog in commit `bee4eb0` and retained one fixed, non-sensitive startup failure message with safe application-error logging.
- Completed manual acceptance for real login and ten-question quiz completion, exactly one ResultWindow, pending statistics and filters, selected-image preview and closing, a controlled six-answer reviewed dataset, and MySQL session and answer-row persistence.
- Confirmed the reviewed-data calculations, NG analysis, and filters remain correct and that pending answers are not treated as wrong.
- Added snapshot-based result statistics for answer distribution, review coverage, reviewed-only accuracy, valid timing, and NG analysis.
- Added read-only All, Reviewed Wrong, User NG, and Pending Review answer filters with selected-answer details.
- Added asynchronous, detached single-image result previews that release source files and reject stale or post-close updates.
- Added native labeled WPF bars for GOOD/NG distribution, reviewed outcomes, review coverage, reviewed accuracy, NG detection, and false-NG rate.
- Kept pending answers out of correct/wrong and reviewed-truth metrics while retaining their user distribution and valid timing values.
- Preserved the existing quiz-to-result constructor and flow, transactional persistence, and Issue #9 two-image quiz cache.

### Quiz Experience

- Added bounded asynchronous quiz image loading with a two-image current/next cache, frozen detached bitmaps, cancellation, stale-load protection, and lifecycle cleanup.
- Disabled answers until the current image is ready, prevented incomplete image-failure sessions from saving, and retained the existing engine duplicate-answer protection.
- Added answered-based quiz progress values and display for question, total, answered, remaining, and completion percentage.
- Hardened local G, N, and Escape shortcuts so repeated input cannot create duplicate answer paths and confirmed exit cleans up incomplete quizzes.

### Application Reliability

- Added controlled WPF dispatcher, task scheduler, and AppDomain global exception handling.
- Added one non-sensitive fatal-error notification before dispatcher-driven application shutdown and suppressed duplicate notifications.
- Added bounded, sanitized global error diagnostics with UTC timestamps, unique error IDs, handler categories, stack traces, inner exceptions, aggregate exception classifications, and termination status.
- Redacted passwords, connection strings, user identifiers, tokens, and configuration secrets from error diagnostics.
- Cached the validated configured log folder during startup and added a LocalAppData fallback when it cannot be used.
- Strengthened optional image-inventory cancellation so caller and startup cancellation return promptly while abandoned filesystem work is observed.

### Database

- Added repository input validation before SQL execution for sessions, answers, users, dashboard limits, report date ranges, and image folders.
- Added duplicate completed-session protection inside the quiz persistence transaction.
- Added a database-enforced completed-session duplicate key and unique index while preserving transactional session and answer persistence.
- Added duplicate-key migration handling that backfills unique historical completion rows and leaves historical duplicate groups nullable so the unique index can be created safely.
- Changed user activation mapping to fail closed when `IsActive` is null or malformed.
- Updated repository null handling so pending quiz answers keep `CorrectAnswer` as null and are not counted as wrong.
- Updated report calculations to use answer aggregates for correct, wrong, pending, reviewed, and accuracy values.
- Optimized dashboard metrics, report summaries, and session recalculation with conditional aggregation to reduce repeated table scans.
- Parameterized dashboard recent-session limits and kept deterministic ordering on session queries.
- Made completed quiz session persistence atomic with MySQL transactions.
- Made answer batch persistence atomic with MySQL transactions.
- Made admin answer review and session statistics recalculation atomic with MySQL transactions.
- Prevented partial session, answer, or recalculated-result writes when a related operation fails.
- Added configurable MySQL connection timeout and limited transient retry behavior.
- Prevented authentication and invalid configuration failures from being retried.

### Startup

- Updated splash startup database validation to use a bounded asynchronous connection check.
- Added clear non-sensitive database unavailable messages when MySQL remains unreachable.
- Changed splash startup initialization to begin asynchronously after the splash window is visible.
- Added bounded startup timeout handling for database checks that do not return promptly.
- Added non-sensitive splash diagnostics for configuration failures, unavailable services, timeouts, cancellation, and unexpected startup failures.
- Prevented duplicate startup initialization and duplicate login window opening from the splash screen.
- Added a splash Exit action that safely cancels startup initialization.
- Hardened configuration loading with a bounded wait so required startup initialization cannot hang on stalled filesystem discovery.
- Hardened optional image inventory with a bounded wait; unavailable or stalled image folders are skipped without blocking login.

### Configuration

- Added a unified local XML configuration system for database and application path settings.
- Added strongly typed application, database, and path settings.
- Removed hardcoded quiz image folder usage from source code.
- Added configurable log, export, and report folders.
- Added configurable MySQL connection timeout, retry count, and retry delay settings.
- Report CSV export now uses the configured export folder.
- Removed the unused JSON settings file.

### Security

- Removed MySQL credentials and connection details from `App.config`.
- Added ignored local database configuration through `DatabaseSettings.local.config`.
- Added `DatabaseSettings.example.config` as a safe placeholder template.
- Added BCrypt.Net-Next password hashing support.
- Added migration for existing plain-text user passwords after successful login.
- Preserved compatibility with existing plain-text accounts during migration.
- Prevented incorrect password attempts from updating stored password values.
