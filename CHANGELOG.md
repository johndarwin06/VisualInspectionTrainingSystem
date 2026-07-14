# CHANGELOG

## Unreleased

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
