# Version 0.9 Stabilization Audit

Date: 2026-07-12
Project: Visual Inspection Training System
Solution: VisualInpsectionTrainingSystem.slnx
Configuration: Debug

## Build Result

Command used:

```powershell
C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe VisualInpsectionTrainingSystem.slnx /t:Build /p:Configuration=Debug
```

Result: Passed
Errors: 0
Warnings: 1

Warning:
- `MVVMTKCFG0002` from `packages/CommunityToolkit.Mvvm.8.4.2/build/CommunityToolkit.Mvvm.SourceGenerators.targets`: MVVM Toolkit source generators may not load correctly while the project uses `packages.config`.

## Summary

The application currently compiles successfully, but Version 0.9 stabilization should prioritize repository hygiene, secret removal, configuration cleanup, and database consistency hardening before new feature work.

No duplicate source classes were found outside generated/build folders. No XAML inline event handlers were found in XAML files during the scan. Dashboard and Reports use real MySQL queries, not fake placeholder data, but both still have incomplete v0.9 analytics/export behavior.

## Issues

### 1. Committed database credentials

- Severity: Critical
- Affected file: `App.config:9-15`
- Problem: The MySQL connection string contains `Uid=root` and a real `Pwd` value.
- Risk: Anyone with repository access can see the database password. The application also encourages running with the root MySQL account.
- Recommended fix: Move the connection string to a local untracked config, environment-specific config transform, Windows Credential Manager, or encrypted configuration. Use a least-privilege MySQL account.
- Suggested implementation order: 1

### 2. Visual Studio and build artifacts are tracked by Git

- Severity: Critical
- Affected files: `.vs/**`, `bin/**`, `obj/**`
- Problem: `git ls-files` shows `.vs`, `bin`, and `obj` files are tracked. The repository has no `.gitignore`.
- Risk: Developer-specific state, generated binaries, stale build outputs, and private IDE metadata can pollute commits and cause noisy or broken diffs.
- Recommended fix: Add a Visual Studio `.gitignore`, remove `.vs`, `bin`, and `obj` from the Git index without deleting local files, and commit the cleanup.
- Suggested implementation order: 2

### 3. Hardcoded quiz image path

- Severity: High
- Affected files: `AppConstants.cs:5-6`, `Services/SystemInitializerService.cs:126-129`
- Problem: The image folder is hardcoded as `D:\QuizImages` in two places.
- Risk: The app fails or silently reports zero images on machines that do not use that exact path. Duplicating the path also creates drift risk.
- Recommended fix: Move the image path to configuration, read it through one service, validate it once, and show a clear setup message when missing.
- Suggested implementation order: 3

### 4. Unstable image identity mapping

- Severity: High
- Affected files: `Services/ImageService.cs`, `ViewModels/AdminViewModel.cs:486-511`, `Repositories/AnswerRepository.cs:56-69`
- Problem: Quiz answers store only `ImageID`, while image IDs are assigned by current folder enumeration. Admin preview reconstructs the image by loading the folder again and matching the same numeric ID.
- Risk: If file order changes, images are added, or the folder differs, Admin may preview the wrong image for a saved answer.
- Recommended fix: Persist stable image identity with each answer, such as file name, image key, or imported image table ID. Use that stable key for admin review and reports.
- Suggested implementation order: 4

### 5. Admin answer review is not atomic

- Severity: High
- Affected file: `Repositories/AnswerRepository.cs:93-130`
- Problem: `ReviewAnswer` updates `tbl_quiz_answer`, closes the connection, then recalculates the parent session in a separate operation.
- Risk: If recalculation fails after the answer update, answer rows and session accuracy totals can become inconsistent.
- Recommended fix: Wrap answer update and session recalculation in one transaction, using one connection and rollback on failure.
- Suggested implementation order: 5

### 6. Splash startup can stall with no recovery path

- Severity: High
- Affected file: `Services/SystemInitializerService.cs:52-58`
- Problem: If MySQL connection fails, initialization returns before raising `InitializationCompleted`.
- Risk: The app can remain on the splash screen with no retry, settings, offline mode, or exit path.
- Recommended fix: Raise a failure state event, show actionable retry/exit/setup controls, or allow login only after the operator acknowledges the database issue.
- Suggested implementation order: 6

### 7. Plain-text password compatibility remains active

- Severity: High
- Affected files: `Services/AuthenticationService.cs:74-101`, `Repositories/UserRepository.cs:86-105`
- Problem: Authentication now supports PBKDF2 hashes but still accepts legacy plain-text password values and upgrades them only after successful login.
- Risk: Dormant accounts can remain plain text indefinitely. If a hash upgrade database write fails, a valid legacy login can fail.
- Recommended fix: Add a one-time migration/admin tool to upgrade all plain-text passwords, record migration status, then plan removal of plain-text fallback. Consider allowing login if hash upgrade fails but log a warning.
- Suggested implementation order: 7

### 8. Null-reference risks around current user state

- Severity: Medium
- Affected file: `ViewModels/HomeViewModel.cs:37-53`
- Problem: `WelcomeMessage` and `AdminVisibility` dereference `SessionService.CurrentUser` directly.
- Risk: Opening Home without a login session or after session loss can crash with `NullReferenceException`.
- Recommended fix: Guard `CurrentUser`, redirect to Login when missing, and avoid direct static session access in display properties.
- Suggested implementation order: 8

### 9. Missing global exception handling and structured logging

- Severity: Medium
- Affected files: `App.xaml.cs`, `Services/MySqlService.cs:187-209`, multiple ViewModels
- Problem: Exceptions are handled with scattered `MessageBox.Show` calls, and no central logging exists.
- Risk: Production errors are hard to diagnose, repeated dialogs can be noisy, and service classes are coupled to WPF UI.
- Recommended fix: Add an application-level exception handler, introduce a logging service, and keep user-facing dialogs in ViewModels or a dialog service.
- Suggested implementation order: 9

### 10. Empty placeholder and unused classes remain compiled

- Severity: Medium
- Affected files: `Services/StatisticsService.cs`, `Services/QuizService.cs`, `Repositories/QuizRepository.cs`, `Services/ConfigurationService.cs`, `Services/NavigationService.cs`
- Problem: Several compiled classes are empty or not referenced by active source.
- Risk: Dead code obscures the architecture and can mislead future development.
- Recommended fix: Remove unused placeholders or implement them as part of a planned architecture pass. Keep only classes with a current responsibility.
- Suggested implementation order: 10

### 11. Dashboard query builds SQL with string concatenation

- Severity: Medium
- Affected file: `Repositories/DashboardRepository.cs:76-97`
- Problem: `GetRecentSessions(int limit)` concatenates `LIMIT` into SQL.
- Risk: The current value comes from internal code, but the pattern is unsafe and can become SQL injection if later exposed to user input.
- Recommended fix: Clamp the limit and use a parameterized query where supported, or keep a strict whitelist for allowed limits.
- Suggested implementation order: 11

### 12. Reports are capped and CSV-only

- Severity: Medium
- Affected files: `Repositories/ReportRepository.cs:104-138`, `ViewModels/ReportsViewModel.cs`
- Problem: Reports load at most 500 rows and export CSV only. Excel/PDF roadmap items remain incomplete.
- Risk: Large date ranges can silently omit records, and operators may assume the export is complete.
- Recommended fix: Add visible row-limit messaging, pagination, and explicit Excel/PDF export tasks.
- Suggested implementation order: 12

### 13. Dashboard analytics are not yet live or trend-based

- Severity: Low
- Affected files: `Repositories/DashboardRepository.cs:46-113`, `ViewModels/DashboardViewModel.cs:177-209`
- Problem: Dashboard values are real MySQL data but refresh only manually and do not include trend charts or today's accuracy.
- Risk: Dashboard may not meet Version 0.9 roadmap expectations for live statistics.
- Recommended fix: Add today's metrics, trend data, and an optional timed refresh after core stabilization issues are resolved.
- Suggested implementation order: 13

### 14. Runtime table creation is mixed into repositories

- Severity: Low
- Affected files: `Repositories/SessionRepository.cs`, `Repositories/AnswerRepository.cs`
- Problem: Repositories create tables at runtime using `CREATE TABLE IF NOT EXISTS`.
- Risk: Schema changes are implicit, hard to review, and may not match production MySQL migration practices.
- Recommended fix: Move schema creation to explicit migrations or documented SQL scripts; keep repositories focused on data access.
- Suggested implementation order: 14

### 15. MVVM Toolkit warning remains

- Severity: Low
- Affected file: `VisualInspectionTrainingSystem.csproj`
- Problem: The project uses `packages.config`, causing `MVVMTKCFG0002`.
- Risk: Source generators may not behave as expected, and the build remains noisy.
- Recommended fix: Either migrate packages to `PackageReference` or remove unused MVVM Toolkit/source-generator dependency if not needed.
- Suggested implementation order: 15

## Checks Without Current Findings

- Duplicate source classes: No duplicate source class names detected outside generated/build folders.
- XAML inline event handlers: No inline XAML event handlers detected by scan.
- Placeholder dashboard/report data: No fake in-memory placeholder data found; current Dashboard and Reports query MySQL.

## Recommended First Stabilization Task

Remove committed secrets and build artifacts:

1. Add a Visual Studio `.gitignore`.
2. Remove `.vs`, `bin`, and `obj` from the Git index.
3. Move the MySQL password out of `App.config`.
4. Add a safe local configuration template without real credentials.

This should be done before additional features because it protects the repository and reduces noisy diffs.
