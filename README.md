# Visual Inspection Training System

Visual Inspection Training System is a Windows desktop application for industrial GOOD/NG inspection training. It provides role-based trainee and administrator workspaces, configurable 10- or 20-question quizzes, review workflows, results, history, dashboards, reports, exports, and operational logging.

Version 1.0.0 is distributed as a portable ZIP for Windows. It targets .NET Framework 4.6.2 and uses MySQL 8 for application data.

## Application roles

- Trainees can register, sign in after activation, complete training, review results, and inspect their personal history and analytics.
- Administrators can manage users, review pending answers, inspect dashboard analytics, and generate daily, weekly, monthly, or custom reports.

## Portable release requirements

- Windows with .NET Framework 4.6.2 or a compatible later in-place .NET Framework 4.x runtime.
- Access to a compatible MySQL 8 database prepared for the application.
- An authorized set of BMP quiz images.
- A writable extraction directory. For example: `D:\Apps\VisualInspectionTrainingSystem`.

Do not run the application from inside the ZIP or extract it beneath `Program Files`. The application writes logs and exports beside the executable by default.

## First run

1. Extract `VisualInspectionTrainingSystem-v1.0.0-win-portable.zip` to a writable directory.
2. Copy `DatabaseSettings.example.config` to `DatabaseSettings.local.config` beside `VisualInpsectionTrainingSystem.exe`.
3. Replace the database placeholders in the local configuration. Never share or commit that file.
4. Keep the default relative folders or configure other authorized locations.
5. Place authorized BMP images in `QuizImages` when using the default path.
6. Start `VisualInpsectionTrainingSystem.exe`.

See [FIRST_RUN.md](FIRST_RUN.md) for the deployment checklist, [USER_MANUAL.md](USER_MANUAL.md) for trainee use, and [ADMINISTRATOR_GUIDE.md](ADMINISTRATOR_GUIDE.md) for administrator operations.

## Portable directory behavior

- `QuizImages` contains authorized `.bmp` training images and is required for quizzes.
- `Logs` receives operational logs when the configured path is writable.
- `Exports` receives CSV, XLSX, and PDF exports.
- `Reports` is reserved for report output.
- `DatabaseSettings.local.config` contains workstation-specific settings and is intentionally excluded from the release ZIP and source control.

The included example configuration uses `./QuizImages`, `./Logs`, `./Exports`, and `./Reports` semantics through Windows relative paths. Relative paths resolve from the extracted application directory.

## Build and verification

The solution uses C# 7.3, WPF, and .NET Framework 4.6.2. Restore NuGet packages before building:

```powershell
.\Restore-NuGet-Packages.ps1 -CleanBuildArtifacts
MSBuild .\VisualInpsectionTrainingSystem.slnx /t:Rebuild /p:Configuration=Debug
MSBuild .\VisualInpsectionTrainingSystem.slnx /t:Rebuild /p:Configuration=Release
```

Create and validate the portable release with:

```powershell
.\Build-PortableRelease.ps1
.\Validate-PortableRelease.ps1
```

The build script uses a committed explicit allowlist. The validator verifies contents, target framework, native x86/x64 dependencies, SHA-256 checksums, safe configuration placeholders, forbidden files, and extraction into two independent writable locations.

## Troubleshooting

- If startup reports a configuration problem, compare the local file with `DatabaseSettings.example.config` and keep all XML names unchanged.
- If database access fails, verify MySQL availability and the account permissions without exposing credentials in screenshots or support messages.
- If a quiz cannot start, confirm the configured image folder exists and contains enough readable BMP images for the selected quiz size.
- If writing logs or exports fails, move the complete extracted application to a writable directory and retry.
- Technical logs are intended for authorized support personnel. Do not publish logs without reviewing them for operational data.

## Security and data handling

The portable ZIP contains no database credentials, local configuration, logs, exports, database data, or test records. Passwords belong only in `DatabaseSettings.local.config` on the target workstation. Database backup, access control, and credential rotation remain administrator responsibilities.

## Release status

See [RELEASE_NOTES_v1.0.0.md](RELEASE_NOTES_v1.0.0.md) for Version 1.0 capabilities, qualification status, and known deployment limitations.
