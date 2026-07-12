# CHANGELOG

## Unreleased

### Security

- Added BCrypt.Net-Next password hashing support.
- Added migration for existing plain-text user passwords after successful login.
- Preserved compatibility with existing plain-text accounts during migration.
- Prevented incorrect password attempts from updating stored password values.
