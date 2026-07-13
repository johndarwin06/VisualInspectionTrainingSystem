# DEVELOPMENT LOG

## 2026-07-13

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
