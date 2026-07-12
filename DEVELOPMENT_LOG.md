# DEVELOPMENT LOG

## 2026-07-13

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
