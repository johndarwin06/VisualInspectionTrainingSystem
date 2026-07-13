# CHANGELOG

## Unreleased

### Database

- Made completed quiz session persistence atomic with MySQL transactions.
- Made answer batch persistence atomic with MySQL transactions.
- Made admin answer review and session statistics recalculation atomic with MySQL transactions.
- Prevented partial session, answer, or recalculated-result writes when a related operation fails.
- Added configurable MySQL connection timeout and limited transient retry behavior.
- Prevented authentication and invalid configuration failures from being retried.

### Startup

- Updated splash startup database validation to use a bounded asynchronous connection check.
- Added clear non-sensitive database unavailable messages when MySQL remains unreachable.

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
