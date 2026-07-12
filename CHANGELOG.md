# CHANGELOG

## Unreleased

### Security

- Removed real MySQL credentials from tracked `App.config`.
- Added secure MySQL configuration resolution through `App.local.config` or environment variables.
- Added `App.local.config.example` as a safe local configuration template.
- Added clear setup errors when MySQL credentials are missing or invalid.
- Documented local MySQL configuration setup in `README.md`.

### Repository

- Added `.gitignore` rules for Visual Studio workspace files, build outputs, test output, diagnostic logs, and local machine configuration.
- Removed generated `.vs`, `bin`, and `obj` artifacts from Git tracking.
- No application runtime behavior changed.
