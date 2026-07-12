# CHANGELOG

## Unreleased

### Security

- Removed MySQL credentials and connection details from `App.config`.
- Added ignored local database configuration through `DatabaseSettings.local.config`.
- Added `DatabaseSettings.example.config` as a safe placeholder template.
- Added BCrypt.Net-Next password hashing support.
- Added migration for existing plain-text user passwords after successful login.
- Preserved compatibility with existing plain-text accounts during migration.
- Prevented incorrect password attempts from updating stored password values.
