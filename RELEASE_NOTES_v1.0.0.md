# Visual Inspection Training System 1.0.0

## Release format

Version 1.0.0 is a portable Windows ZIP. It is not an installer and does not write application files to `Program Files`. Extract the full package to a writable directory before use.

## Highlights

- Fluent WPF interface with light and dark themes, keyboard focus, responsive layouts, role-aware navigation, and safe window lifecycle behavior.
- Trainee registration, administrator activation, authentication, user management, and authorization boundaries.
- Configurable 10-question and 20-question GOOD/NG training with persisted answers and a single Result presentation.
- Reviewed-only statistics that exclude pending truth from correct/wrong accuracy calculations.
- Training history, session detail, personal analytics, administrator Dashboard, Review Workflow, Reports, charts, and CSV/XLSX/PDF exports.
- Consistent report snapshots, bounded startup/database operations, sanitized global error handling, and structured operational logging.
- Dedicated fail-closed database regression and performance testing that does not run against production data.

## Runtime and dependencies

- Target framework: .NET Framework 4.6.2.
- Language compatibility: C# 7.3.
- Database: MySQL 8 compatible deployment.
- UI: WPF-UI/Violeta and LiveChartsCore/SkiaSharp with packaged x86 and x64 native libraries.

.NET Framework 4.6.2 support ends on January 12, 2027. Plan an upgrade to a supported framework before that date.

## Security and configuration

The release contains only a placeholder configuration example. It does not include credentials, workstation-local configuration, logs, exports, database data, test records, symbols, source code, or test assemblies. Create `DatabaseSettings.local.config` locally and protect it according to organizational policy.

## Qualification status

The application has been built against genuine .NET Framework 4.6.2 reference assemblies and visibly exercised on a development machine with a later compatible in-place .NET Framework 4.x runtime. Execution on a clean machine or VM containing only the .NET Framework 4.6.2 runtime remains **Not Run** and is required before the final Version 1.0 tag and GitHub Release are published.

The portable ZIP must also pass visible acceptance from an extracted writable directory, including both roles, quizzes, results, analytics, exports, logout, shutdown, and creation of logs/exports beside the application as configured.

## Upgrade notes

Close the previous version before upgrading. Extract 1.0.0 into a new writable directory, verify checksums, and copy only the approved local configuration and content. Do not overwrite the new dependency set with files from an earlier build. Back up the production database through established MySQL administration procedures before deployment changes.

## Known limitations

- A compatible MySQL schema, restricted operational account, and authorized image set are deployment prerequisites and are not included.
- The portable application does not register file associations, create shortcuts, install .NET Framework, or configure MySQL.
- Genuine .NET Framework 4.6.2-only runtime qualification remains an explicit release gate until performed.
