# Administrator Guide

## Deployment responsibilities

Extract the portable application to a writable, access-controlled directory. Configure `DatabaseSettings.local.config` beside the executable using the safe example as the template. Do not place credentials in the example file, source control, screenshots, or public support requests.

The default relative folders are:

- `QuizImages` for authorized BMP training content.
- `Logs` for operational logs.
- `Exports` for CSV, XLSX, and PDF output.
- `Reports` for report output.

Keep the x86 and x64 native folders intact. Verify the ZIP and payload SHA-256 manifests before first use. Limit write access to authorized workstation users.

## User Management

Administrators can create, update, activate, deactivate, assign supported roles, reset passwords, and delete users through User Management. Review identity and role changes before saving. Inactive accounts cannot sign in, and Trainees must not receive administrator destinations.

## Review Workflow

Use Review Workflow to filter pending answers, inspect the selected image, and assign supported GOOD or NG truth. Null, empty, whitespace, or unsupported truth remains pending. Pending answers do not count as wrong or enter reviewed-only accuracy.

## Dashboard

Dashboard shows the configured local-day metrics and analytics from the production database. Reviewed accuracy uses only supported GOOD or NG review truth. GOOD and NG trainee counts include valid pending selections. Refresh replaces existing rows and values rather than appending duplicates.

## Reports and exports

Reports supports Today, This Week, monthly, and custom periods as available in the interface. Summary values and session rows are read from one consistent database snapshot. Interactive display and export safeguards remain enforced. Generate CSV, XLSX, or PDF only into an authorized writable location and review exported data before distribution.

## Logs and troubleshooting

Operational logs use the configured `Logs` folder when it is available and fall back to the current user's local application-data log folder when necessary. Entries use sanitized diagnostics, bounded detail, and unique error IDs. Logging failure must not prevent safe shutdown.

When diagnosing a problem:

1. Reproduce it through normal navigation if safe.
2. Record the time, role, destination, and non-sensitive displayed message.
3. Review logs only through an authorized channel.
4. Do not disclose passwords, hashes, tokens, connection strings, raw SQL parameters, configuration secrets, or unnecessary personal data.

## Database administration

The application does not replace a database backup or disaster-recovery system. MySQL account provisioning, least privilege, backups, restore testing, schema change control, and retention are external administrator responsibilities. Never run test suites against production. Dedicated database tests require the documented test-only schema, identity marker, restricted account, and user-scoped environment variables; credential values must remain outside the repository.

## Upgrade and rollback

Before replacing a portable release, close the application and preserve the local configuration, authorized images, required exports, and operational logs according to policy. Extract a new version into a new writable directory, copy only approved local data, validate it, and retain the prior package until acceptance completes. Do not mix dependency files from different releases.
