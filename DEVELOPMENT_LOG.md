# DEVELOPMENT LOG

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
