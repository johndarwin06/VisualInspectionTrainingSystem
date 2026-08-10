# DEVELOPMENT LOG

## 2026-08-10

### Issue #20 / GitHub Issue #24 - Version 1.0 Portable Release

- Changed the Version 1.0 delivery design from an installer to `VisualInspectionTrainingSystem-v1.0.0-win-portable.zip`. No installer dependency, script, shortcut, registry entry, uninstall entry, protected-directory deployment, or `Program Files` behavior was added.
- Set assembly/file versions to 1.0.0.0 and product/informational version to 1.0.0. Preserved the established executable spelling, .NET Framework 4.6.2 target, C# 7.3 language contract, MVVM architecture, package versions, and configuration loading behavior.
- Added `PortableReleaseFiles.psd1` as the explicit runtime/documentation allowlist, `Build-PortableRelease.ps1` for clean Release restore/build/staging/ZIP/checksum generation, and `Validate-PortableRelease.ps1` for exact payload, framework, version, native x86/x64, path, placeholder, secret, forbidden-file, extraction, and SHA-256 verification.
- The ZIP safely represents `QuizImages`, `Logs`, `Exports`, and `Reports`, retains relative paths beside the executable and the existing LocalAppData log fallback, and includes a placeholder-only configuration example, README, first-run guide, user manual, administrator guide, and Version 1.0 release notes. Local configuration, credentials, production/test data, images, logs, exports, reports, symbols, XML documentation, ARM64 assets, test assemblies, NuGet packages, source, repository metadata, and generated output are excluded.
- Automated package acceptance validated the allowlisted payload in two independent extraction directories and proved that a forbidden file and a checksum-tampered ZIP are rejected. Generated archives, checksum files, staging, validation output, and acceptance extractions remain untracked and are removed before delivery.
- Final verification started from a removed local package/build cache, restored production and test dependencies, and rebuilt Debug/Release AnyCPU with zero errors and one unchanged `MVVMTKCFG0002` warning per configuration. Functional regression passed 382/382, fail-closed preflight 4/4, required Debug/Release database tests 48/48, Release x64 performance 28/28, and cleanup verification 2/2, with zero failures or skips; supported x86/x64 native tests passed.
- Real visible acceptance passed in two independent writable extraction locations with no repository, Visual Studio, or NuGet dependency. Both roles, registration validation, administrator workspaces, trainee workflows, 10/20-question quizzes, Result/History/detail, analytics, reports/exports, themes, resizing, keyboard interaction, logout, shutdown, and portable output paths passed without a crash, freeze, raw database error, sensitive message, diagnostic, or unexpected dialog.
- Visible acceptance created only legitimate 10-question and 20-question quiz sessions. No user-management or review-truth action occurred. Protected database tests verify unchanged stable production schema/row fingerprints and zero synthetic rows.
- A clean machine or VM containing only the .NET Framework 4.6.2 runtime remains unavailable, so that genuine runtime test is **Not Run**. This does not block the draft delivery PR, but it blocks the `v1.0.0` tag and GitHub Release.

### Issue #18 / GitHub Issue #22 - Evidence-Based Performance Testing

- Added an explicit permanent `Performance` category and PowerShell runner modes for secret-free baseline workloads, protected database workloads, or the complete suite. Measurements use warm-up runs, repeated samples, Release x64 as the authoritative local configuration, and informational minimum/median/p95/maximum reporting rather than arbitrary hardware-dependent gates.
- The accepted local profile was a 64-bit Windows 10.0.26200 workstation with an AMD Ryzen 5 7600X-class processor, 12 logical cores, approximately 31.1 GiB RAM, and MySQL 8.0.45. Machine name, Windows account, private paths, connection values, and raw benchmark output are intentionally not recorded.
- Added deterministic coverage for configuration loading, WPF resource/window construction, quiz interaction, image enumeration/sampling/hashing/decoding at 100, 1,000, and 5,000 files, CSV/XLSX/PDF export at 100, 1,000, and 5,000 sessions, concurrent logging/rollover, repeated role-window lifecycles, and resource cleanup. Temporary images, exports, logs, and isolated settings are removed by teardown.
- Added protected MySQL performance coverage for administrator/trainee authentication, Dashboard and Reports over 500 sessions and 10,000 answers, a 10,000-answer review queue, 300 unique-image review propagation, 501-user management, and 120-session/2,400-answer History. The suite retains the Issue #19 marker/account/separation/grant checks, unique run ownership, production fingerprints, and deterministic cleanup.
- The measured image-selection bottleneck was repeated cache-validation filesystem metadata work for all 5,000 candidates before sampling. The accepted optimization samples metadata first, hashes only the selected 10 or 20 files, preserves duplicate identity and unbiased selection, adds cancellation through enumeration, and bounds the process hash cache with a 4,096-entry least-recently-used policy.
- Under identical 5,000-image workloads, 10-image selection improved from 1,375.733 ms median / 1,424.686 ms p95 to 31.370 ms / 36.779 ms; 20-image selection improved from 1,325.052 ms / 1,354.804 ms to 34.016 ms / 41.133 ms. The cache remained at its 4,096-entry bound after a 5,000-file workload. No database index or package change was made because measured repository timings were acceptable and query-plan evidence did not justify speculative changes.
- Corrected the database-performance fixture to use the production Dashboard convention `trendStart = dayEnd.AddDays(-7)` rather than an invalid one-day trend, added a focused midnight/exact-seven-day assertion, and included the 300 intentionally created truth rows in run-owned cleanup accounting. Production `DashboardRepository.ValidateTrendRange` and Result/analytics semantics were not weakened.
- Final clean restore and Debug/Release AnyCPU rebuilds passed with zero errors and one unchanged `MVVMTKCFG0002` warning each. Functional tests passed 382/382, explicit database tests 48/48, preflight 4/4, Release x64 performance 28/28, and cleanup 2/2, with zero skips. x86/x64 native checks, stable production fingerprints, and zero synthetic rows passed.
- Approved visible acceptance exercised both roles, repeated navigation/refresh, close-during-refresh, Training Setup with the normal large inventory, 10/20-question Quiz and exactly-one Result behavior, History/detail, themes, resizing, keyboard/pointer interaction, logout, and shutdown without a freeze, duplicate, crash, sensitive error, diagnostic, or test-data exposure.

## 2026-08-02

### Issue #19 / GitHub Issue #23 - Isolated Database Regression Testing

- Expanded the permanent NUnit database category from four fail-closed contracts to 24 integration tests in each build configuration. Coverage now exercises schema/privilege safety, parameter redaction, registration and authentication, administrator/trainee boundaries, session and answer persistence, review propagation and concurrency, Dashboard, Reports, Training History, rollback, and deterministic cleanup.
- Added a versioned dedicated-schema contract with an exact identity marker and a restricted test account. The runner reads `VITS_TEST_MYSQL_CONNECTION_STRING` and `VITS_TEST_MYSQL_SCHEMA` from Process or Windows User scope without displaying their values, disables pooling, bounds connection attempts, compares the live test identity with production, and refuses missing, ambiguous, production-looking, or over-privileged configurations.
- Synthetic fixtures use unique `I19T` identifiers and repository-compatible rows. Each fixture owns cleanup in `finally`, and separate Debug/Release cleanup gates independently require zero remaining synthetic users, sessions, answers, or review-truth rows. The harness never creates or drops a database and never deletes unrelated records.
- Added a safe provisioning example while ignoring the populated local provisioning file. The dedicated schema, marker, restricted account, and User-scope variables remain available for future regression runs; no credentials, connection strings, local configuration, or database data are tracked.
- Final clean Debug and Release AnyCPU rebuilds completed with zero errors and one existing `MVVMTKCFG0002` warning each. The complete two-configuration run passed 382 tests with 0 failures and 0 skips; the explicit required-database rerun passed 48/48, the two cleanup gates passed, and supported x86/x64 native-deployment checks passed.
- Approved visible WPF acceptance passed administrator Dashboard, Reports, User Management, and Review Workflow plus trainee authorization, Training Setup, Result, History, detail, logout, and shutdown. No test rows or credentials appeared in production or the UI.
- Read-only production reconciliation found the schema unchanged, repeated row fingerprints stable, no `I19T` rows, and acceptance-period session/answer/review activity consistent with the visible run. The operator confirmed exactly one external MySQL Workbench user deletion; it was accepted as an authorized non-application action, no personal details were recorded, and no other unexplained table change remained.

### Issue #16 / GitHub Issue #20 - Centralized Logging Framework

- Kept the already-installed Apache-2.0 `log4net` 3.3.2 package and made `ApplicationErrorLogger` the single production logging boundary. A bounded asynchronous sink writes complete UTF-8 entries without blocking UI callers, rolls `application.log` at 5 MiB, and retains five backups.
- The logger uses `PathSettings.LogFolder` only after successful configuration loading and otherwise falls back to `%LocalAppData%\VisualInspectionTrainingSystem\Logs`; fatal handlers perform no configuration discovery. Invalid, read-only, locked, and unavailable paths fail over safely, and simultaneous primary/fallback failure cannot escape or recursively trigger global handling.
- Added Debug, Information, Warning, Error, and Fatal levels with UTC timestamp, unique event ID, meaningful component, thread metadata, termination classification, bounded sanitized exception details, inner chain, and flattened aggregate types. Central redaction covers credentials, BCrypt hashes, tokens, connection strings, configuration secrets, paths in diagnostics, and oversized messages/stacks.
- Integrated safe logging across startup and shutdown, global dispatcher/task/AppDomain handlers, configuration and database initialization, authentication/session lifecycle, registration and administrator security actions, quiz persistence, export success/cancellation/failure, preview failure, and existing feature boundaries. User-facing errors remain fixed and non-sensitive.
- Added 17 permanent logging tests for formatting, level filtering, UTF-8, exception chains, redaction, duplicate suppression, 120-call concurrency, rollover/retention, configured and fallback paths, provider failure, bounded shutdown, task-observation behavior, and production integration contracts. Native-deployment tests now verify the managed `log4net.dll` output and the `net462` reference contract.
- Final Debug and Release AnyCPU rebuilds each completed with zero errors and one existing `MVVMTKCFG0002` warning. The complete two-configuration run passed 296 tests, skipped 8, and failed 0; x86 and x64 native-deployment checks passed.
- The eight skipped results are four database tests in each configuration. They were intentionally not run because no dedicated test-only schema was configured; planned database-testing Issue #23 owns that isolated environment, and these tests must never target production data.
- Approved real WPF acceptance passed administrator and trainee startup, navigation, data loading/refresh, logout, and shutdown. Read-only log inspection found complete structured entries through the final shutdown flush, meaningful levels/components, unique event IDs, no provider errors or unintended duplicates, and no passwords, hashes, tokens, connection strings, SQL values, sensitive configuration, or unnecessary personal data.
- Temporary logs, test results, build output, and generated artifacts were removed after verification. Automated verification did not modify local configuration or the production database.

## 2026-08-01

### Issue #17 - Permanent Regression Testing

- Added a permanent NUnit 4.4.0 / NUnit3TestAdapter 5.2.0 test project targeting .NET Framework 4.6.2 and C# 7.3, a repeatable PowerShell runner, and a pinned Windows CI workflow for secret-free categories. Tests are organized as Unit, Integration, WPF, Database, Export, NativeDeployment, and explicit ManualRuntime gates.
- Added a production-composition regression that loads actual application resources on an STA dispatcher, establishes controlled administrator and trainee sessions, constructs every authorized workspace, checks unauthorized routes, detects duplicate windows and `Workspace unavailable`, and closes all created windows without replacing the production composition path with broad mocks.
- Diagnosed the visible workspace failure as an invalid shared WPF-UI window contract: the shared style requested a backdrop while `ExtendsContentIntoTitleBar` was false. Corrected the shared style, retained fixed non-sensitive UI errors, and kept technical exception logging at the existing boundary.
- Added Fluent Back and Escape navigation to Training Setup and My Training History using the existing owner shell, with repeated-command and active-child safeguards. Reworked Quiz presentation into responsive Fluent cards and semantic GOOD/NG actions while preserving 10/20-question selection, keyboard behavior, persistence, pending review, and exactly-one Result opening.
- Final Debug and Release AnyCPU rebuilds each completed with zero errors and one existing `MVVMTKCFG0002` warning. The complete two-configuration run passed 262 tests, skipped 8, and failed 0; x86 and x64 native-deployment checks passed.
- The eight skipped results are four database tests in each build configuration. They were intentionally not run because no dedicated test-only schema was configured. They belong to planned database-testing Issue #23 and must not be pointed at production data.
- Independent read-only SQL matched the administrator Dashboard and Reports values. Approved real WPF acceptance passed administrator reporting, trainee Back/Escape and repeated navigation, both quiz sizes, light/dark and keyboard interaction, persistence, one Result per quiz, logout, and shutdown without `Workspace unavailable`, a crash, freeze, binding failure, sensitive error, or diagnostic dialog.
- Automated tests created no database rows. Temporary exports, logs, test results, build output, and workspace-composition configuration directories were removed; normal visible acceptance records were preserved as application data.

### Issue #15.3 - .NET Framework 4.6.2 Compatibility Migration

- Created GitHub issue #57 after merged PR #56 and retargeted from the verified `1552366` baseline without rewriting Fluent UI history or modifying `main` directly.
- Changed the application target and runtime SKU from .NET Framework 4.8.1 to 4.6.2 while making C# 7.3 explicit. Replaced WPF accessibility properties unavailable in 4.6.2 with supported semantic item types without changing application behavior or visual presentation.
- Verified the official .NET Framework 4.6.2 SDK, Developer/Targeting Pack, reference assemblies, facades, and framework list. Audited all 53 packages and selected their compatible assets; no downgrade was required. `System.ValueTuple` 4.6.2 replaced 4.5.0 to provide a compatible `net462` assembly, with a validated 4.0.5 binding redirect and project reference resolution.
- Corrected the existing `System.Security.Cryptography.Pkcs` reference identity to match the restored signed assembly. Clean NuGet restore and vulnerability audit passed, and no `net48` or `net481` assembly asset remains referenced.
- Final compatibility automation passed 50 assertions covering BCrypt hashing and verification, reviewed/pending Result semantics, 10/20 quiz sizes, report periods and consistent snapshots, ordering and safeguards, MySQL cleanup, and real CSV/XLSX/PDF generation. Generated exports, probes, platform outputs, and temporary database identifiers were removed.
- Local visible WPF acceptance passed Splash, authentication and registration, inactive rejection, all administrator and trainee destinations, Dashboard and Reports periods/charts, real XLSX/PDF opening, 10- and 20-question quizzes, exactly one Result per quiz, Training History/detail, themes, dialogs, DataGrids, resizing, keyboard navigation, logout, and shutdown without a missing assembly, type/method/file load failure, XAML parse failure, crash, freeze, or sensitive diagnostic.
- The application was compiled against genuine 4.6.2 reference assemblies and executed successfully on the development machine's newer in-place CLR. An isolated machine or VM containing only the 4.6.2 runtime was unavailable, so genuine 4.6.2-only runtime acceptance remains **Not Run** and is an explicit deployment limitation.
- Microsoft support for .NET Framework 4.6.2 ends January 12, 2027; plan migration to a supported runtime before that date.

## 2026-07-29

### Issue #15.2 - Fluent UI Migration

- Created the follow-up to Issue #53 from the merged PR #54 baseline without rewriting or reverting that history. Replaced the presentation layer with WPF-UI 4.3.0 and WPF-UI.Violeta 4.3.0.3 while keeping .NET Framework 4.8.1, C# 7.3, MVVM, MySQL, CommunityToolkit.Mvvm, Microsoft.Xaml.Behaviors.Wpf, and LiveChartsCore 2.0.5.
- Established WPF-UI and Violeta theme/control dictionaries before project semantic resources. `ApplicationThemeService` now coordinates Fluent light/dark state and the existing chart theme event, while subtle navigation/chart motion respects disabled Windows client-area animations.
- Migrated every production surface and shared state: Splash, authentication and registration, the role-aware shell, administrator and trainee workspaces, Training Setup, Quiz, Result, Training History/detail, Review Workflow, User Management, Dashboard, Reports, dialogs, loading overlays, validation, empty, and error states.
- Preserved single-flight owned navigation, role authorization, 10/20-question behavior, exactly-one Result presentation, responsive cancellation, late-result rejection, safe close/logout, DataGrid sorting/filtering/selection/virtualization, normalized GOOD/NG analytics, reviewed-only nullable accuracy, repeatable-read Reports, and existing export generation.
- Removed MahApps.Metro, MaterialDesignThemes, MaterialDesignColors, MaterialDesignThemes.MahApps, and ControlzEx from `packages.config`, project references/imports, themes, styles, XAML namespaces, code-behind base types, and production resources. No obsolete production reference remains.
- Passed 147 temporary assertions: 37 Fluent resource/construction, 58 source/architecture, 11 controlled MySQL analytics and exact cleanup, and 41 Result/quiz-size/Reports/order/safeguard regressions. AnyCPU Debug and Release and supported x86/x64 builds completed with zero errors and the one existing `MVVMTKCFG0002` warning each; native WPF-UI, Violeta, LiveChartsCore, SkiaSharp, and HarfBuzz deployment was verified.
- Real WPF acceptance passed all administrator and trainee destinations, both themes, window controls/resizing, keyboard focus, dialogs, loading/empty states, repeated navigation/refresh, close during refresh, DataGrid interaction, both quiz sizes, Result/history previews, logout, and shutdown. No clipping, crash, freeze, binding failure, raw database error, sensitive message, or unexpected diagnostic dialog occurred.
- Removed the controlled session and six answers with zero residual rows, then removed all temporary probes, configs, executables, `TestResults`, and architecture-specific test output. Final production scope review found no unrelated source, secret, local configuration, log, export, or generated binary.

## 2026-07-28

### Issue #15.1 - Material Design UI and Analytics Overhaul

- Replaced the competing authenticated navigation surfaces with one role-aware MahApps shell. The shell exposes only authorized administrator or trainee destinations, owns one modeless workspace at a time, restores minimized workspaces instead of duplicating them, transfers `Application.MainWindow` safely during logout, and suppresses workspace reactivation while shutting down.
- Composed `BundledTheme`, Material Design 3 defaults, MahApps controls/fonts/theme, and the Material/MahApps bridge before custom dictionaries. Added process-wide light/dark switching and rebuilt every application surface with consistent Material fields, actions, cards, tables, status chips, dialogs, focus visuals, responsive scrolling, modern window chrome, and subtle state animations.
- Added reusable chart-neutral `ChartPoint`, `AnalyticsChartData`, and snapshot models. LiveChartsCore ViewModels translate those models into replaceable Cartesian and pie series, follow application theme changes, preserve nullable reviewed accuracy, provide bounded safe empty states, and release theme subscriptions on disposal.
- Extended Dashboard to load headline metrics, deterministic recent sessions, and 7/30-day charts in one repository-owned `RepeatableRead` snapshot. Session and answer aggregates remain separate so answer cardinality cannot multiply duration, and normalized supported GOOD/NG semantics keep pending truth out of reviewed/wrong values.
- Added authenticated trainee-only 7/30-day progress analytics. History rows remain available when the optional chart query fails; the chart shows a fixed unavailable state and the technical exception is logged without exposing credentials or connection details.
- Added bounded Reports chart aggregation inside the existing summary/session `RepeatableRead` transaction. The 500-row interactive disclosure, 10,000-session export safeguard, deterministic ordering, CSV/XLSX/PDF generation, and nullable reviewed accuracy remain unchanged.
- Final automation passed 555 Material UI/resource assertions, 173 shell/navigation assertions, 11,685 controlled MySQL analytics assertions, 76 Result Module assertions, 29 configurable quiz-size assertions, and independent 20- and 11-assertion optional-history-chart failure checks. Cleanup verified zero temporary answer, session, and user rows while preserving existing accounts.
- Native deployment passed 96 assertions for each of Debug x64, Debug x86, Release x64, and Release x86. Real WPF Cartesian/pie rendering, Material/MahApps resource construction, light-dark-light refresh, architecture-correct SkiaSharp/HarfBuzz loading, and clean chart disposal all passed.
- Final Debug and Release rebuilds completed with zero errors and one existing `MVVMTKCFG0002` warning each. `git diff --check` passed, and the complete production diff contained no unrelated module, secret, local configuration, log, probe, export, or generated binary.
- Real WPF acceptance passed every redesigned administrator and trainee surface, role authorization, theme switching, window chrome, resizing, keyboard interaction, Review Workflow, Dashboard, Reports, User Management, Login/Registration, Training Setup, both quiz sizes, Result, Training History/detail, refresh, logout, and shutdown. No clipping, inconsistent/plain controls, crash, freeze, binding failure, raw database error, sensitive message, or unexpected diagnostic dialog occurred.

## 2026-07-26

### Issue #15 - UI Polish

- Audited every production window, shared resource dictionary, reusable control, command path, and existing Material Design usage before changing the UI. The implementation keeps the existing Visual Inspection identity and the intentionally misspelled `Resources/DesignTokems` path.
- Added the shared `ApplicationStyles` dictionary and corrected light-theme composition so application colors, typography, spacing, focus, inputs, buttons, status presentation, ToolTips, and DataGrid virtualization resolve consistently at runtime.
- Added a reusable `ModernProgressBar` busy overlay contract and applied it only to operations that already expose loading state. Existing asynchronous work, cancellation, generation checks, and busy command guards remain owned by the ViewModels.
- Added `ApplicationDialogService` and its accessible WPF dialog, preserving destructive/security confirmation behavior while preventing duplicate dialogs and keeping raw exceptions, SQL details, paths, credentials, and internal diagnostics out of user messages.
- Reworked Splash, Login, Registration, Administration, User Management, Review Workflow, Home, Quiz, Result, Dashboard, Reports, Loading, and fallback layouts for resizing, long content, keyboard focus, accessible labels, semantic icons plus text, and proportional images without changing business behavior.
- Passed 268 UI/resource assertions and 162 cross-module regression assertions (430 total). Coverage included XAML parsing/instantiation, effective resources, command bindings, loading/close lifecycle, rapid navigation/dialog suppression, virtualization, long text, authorization, quiz/statistics, Dashboard, Reports, CSV/XLSX/PDF generation, Review Workflow, and read-only MySQL access.
- Real WPF acceptance passed Splash/Login, Registration, administrator navigation, User Management, Review Workflow, Dashboard, Today/This Week Reports, all export dialogs, trainee Home, real 10- and 20-question quizzes, one Result window per completion, Result filters/NG analysis/image preview, logout, and shutdown. No crash, freeze, raw database error, missing resource, or unexpected diagnostic dialog occurred.
- Current-scale compact and large layouts were visibly exercised. Separate 125% and 150% Windows scaling and a deliberately prolonged visible loading operation were Not Run; deterministic tests covered their layout resources, busy visibility, duplicate prevention, and prompt close behavior.
- Removed the two visible test sessions and their 30 answers and verified no rows remained. Temporary probes, exports, logs, screenshots, and generated build output were removed from the delivery set.
- Added secure trainee My Training History without changing existing quiz persistence or Result Module behavior. The service captures the active session identity internally; repository list, summary, and answer-detail queries are parameterized and always constrain that identity. Completed sessions page deterministically in groups of 50 with bounded search/date/status filters.
- Added normalized GOOD/NG statistics with invalid truth remaining pending, reviewed-only nullable accuracy, automatic/administrator provenance without reviewer identity, and a read-only detail surface with lazy bounded image preview. ViewModels perform database and filesystem work away from the dispatcher, disable repeated work, observe abandoned tasks, and reject late publication after cancellation or close.
- Passed 123 focused history assertions and 182 expanded cross-module assertions (305 additional assertions) against controlled MySQL records. Coverage included cross-user and administrator isolation, 10/20-question details, malformed/lowercase/padded values, deterministic two-page loading, refresh de-duplication, filters, empty states, provenance, safe error text, close during blocked work, every production XAML file, and Result/Dashboard/Reports/Review/User Management regressions.
- Real trainee WPF verification passed current-user history, single-window navigation, filters, empty state, duplicate-free refresh, a real 10-answer detail with reviewed statistics and image preview, and stable closing. Administrator Review, Dashboard, Reports, User Management, logout, and shutdown also passed without a crash, freeze, raw database error, or unexpected diagnostic dialog. Separate 125%/150% scaling and the remaining deliberately adverse visible history cases were Not Run and are recorded as manual follow-up checks.

### Issue #14 - User Management

- Added administrator user management for creating accounts, activating and deactivating access, changing canonical Admin/User roles, and resetting passwords. Repository-owned serialized transactions preserve unique Employee Numbers, rollback on failure, and enforce self/final-administrator safeguards.
- Added public trainee registration from the existing Login window. The registration API accepts identity and password fields only, always stores a BCrypt hash, always creates an inactive User account, and requires administrator activation before login.
- Kept Login and registration asynchronous with one-operation busy guards, prompt cancellation and close behavior, abandoned-task observation, stale-result protection, fixed non-sensitive messages, and technical diagnostics through `ApplicationErrorLogger`.
- Passed 186 temporary automated assertions: 60 User Management, 45 Registration, and 81 cross-module regression assertions. Coverage included validation, duplicate and concurrent registration, rollback, legacy password upgrade, authentication, activation, roles, Dashboard, Reports, Review Workflow, Result Module, and 10/20-question quizzes.
- Final Debug and Release rebuilds completed with zero errors and one existing `MVVMTKCFG0002` warning in each configuration.
- Real visible WPF acceptance passed registration, inactive-login rejection, administrator activation, activated Trainee login without administrator functions, Review Workflow refresh, Dashboard open/close, Today and This Week Reports, one ResultWindow for both quiz sizes, logout, and normal shutdown. No crash, freeze, raw database error, unexpected diagnostic dialog, or sensitive message occurred.
- Temporary probe data and artifacts were removed. The newly registered accepted trainee account was deliberately preserved as requested. User Management and Registration were subsequently delivered by merged PR #51.

### Issue #13 - Review Workflow

- Added lowercase SHA-256 identity for exact image bytes. Quiz metadata hashing runs away from the WPF dispatcher, and completed answer persistence stores both `ImageHash` and `ImageFileName` without changing the existing public quiz APIs.
- Added the idempotent `tbl_image_review_truth` schema with one GOOD/NG truth per hash, reviewer/source/timestamp metadata, and a version used to reject stale truth corrections.
- New answer batches preload reusable truth once and grade matching images automatically. Manual individual and bulk review lock the relevant rows, propagate truth across exact hashes, recalculate every affected session, and commit the complete change atomically.
- Added administrator multi-selection, grouped bulk GOOD/NG review, clear selection counts, text search, and All, Pending, Reviewed, Auto Reviewed, Manual Reviewed, User GOOD, User NG, Correct, Wrong, Has Reusable Truth, and Missing Stable Identity filters.
- Preserved conservative legacy handling: a row without stable identity can be reviewed individually; an available preview requires administrator confirmation before its hash is attached; an unavailable file never propagates based on filename.
- Kept review loading and preview work asynchronous with busy-command guards, cancellation and operation generations, observed abandoned tasks, fixed non-sensitive UI errors, and prompt close during a deliberately blocked refresh.
- Passed the temporary Issue #13 probe with 190 assertions covering byte identity, cache behavior, automatic reuse, propagation, truth correction and concurrency, bulk and legacy review, search/filter state, session recalculation, Result/Dashboard/Reports semantics, 10/20-question quiz regression, connection cleanup, and zero residual rows.
- Visible WPF acceptance passed administrator login, one Review Workflow window, search and every filter, individual GOOD/NG review, later identical-image auto review, differing trainee answers, pending duplicate propagation, grouped bulk review, confirmed truth correction, available/unavailable legacy behavior, Dashboard and Reports navigation, disabled repeated commands while loading, close during a real database stall, normal logout, and normal shutdown.
- Final Debug and Release rebuilds completed with zero errors and one existing `MVVMTKCFG0002` warning in each configuration. Temporary sessions, answers, truth rows, lock markers, probes, logs, and generated build output were removed from the delivery set.
- Issue #13 Review Workflow is implemented and verified on `issue-13-review-workflow` and is awaiting pull-request review and merge; GitHub issue #17 remains open.

## 2026-07-24

### Issue #12 - Reports

- Implemented explicit Daily, true Monday-to-Sunday This Week, rolling Last 7 Days, This Month, inclusive Custom, and All Dates periods. Every bounded query uses parameterized `StartTime >= @StartTime` and `StartTime < @EndTime` predicates without applying a date function to `StartTime`.
- Reworked report SQL to aggregate answers before joining sessions, preserving deterministic `StartTime DESC, SessionID DESC` ordering without multiplying session totals. Only normalized GOOD or NG truth is reviewed; malformed truth is pending, valid truth with a missing/invalid/mismatching trainee answer is wrong, and no reviewed denominator maps to N/A.
- Retained a 500-row interactive list with explicit limit disclosure and added a separate complete export snapshot with a 10,000-session safeguard so CSV, Excel, and PDF never silently inherit the display limit.
- Added asynchronous refresh and export coordination with disabled overlapping commands, operation generations, stale-result rejection, observed abandoned tasks, lifecycle cancellation, fixed non-sensitive UI messages, and `ApplicationErrorLogger` diagnostics.
- Added `DocumentFormat.OpenXml` 3.5.1 for real three-sheet `.xlsx` documents and `PDFsharp-WPF` 6.2.4 for real A4 landscape multipage PDFs. Both packages support .NET Framework 4.8.1, work with `packages.config` and C# 7.3, and use the MIT license.
- Passed the temporary Issue #12 probe with 240 assertions: periods 25, models 10, Result regression 16, quiz regressions 76, Administration regression 8, MySQL reports 18, independent SQL 5, Dashboard parity 9, normalization 10, boundaries 8, CSV 10, Excel 6, PDF 4, N/A exports 3, export safety 3, ViewModel 11, ViewModel safety 17, and cleanup 1.
- Corrected draft PR #49 so each display or export snapshot reads its summary and session rows on one MySQL connection inside one repository-owned `RepeatableRead` transaction. The repository commits only after constructing the in-memory snapshot, rolls back incomplete reads, and closes the transaction and connection before any export generation.
- Passed 426 temporary correction assertions, including deterministic answer-review and session-insertion changes between the logical reads, matching display/export totals, `RepeatableRead` verification, rollback and connection cleanup, the 500-row disclosure, the 10,000-session safeguard, Dashboard semantics, Result and quiz regressions, and CSV/XLSX/PDF validation. The temporary concurrency hook was removed; the final repository then passed 394 seam-free regression assertions.
- Controlled MySQL verification covered daily/weekly/monthly/custom/all-date periods, completed and open sessions, multiple trainees, correct and wrong GOOD/NG answers, null/empty/whitespace/unsupported/lowercase/padded values, zero-reviewed behavior, independent aggregate comparison, and deterministic ordering. Cleanup reported zero residual probe sessions.
- Visible WPF acceptance passed administrator login, exactly one Reports window, every period action, empty and invalid ranges, N/A accuracy, repeated Refresh, a real close during a blocked database refresh, all save-dialog cancellations, CSV/XLSX/PDF generation and opening, four-page PDF rendering, Administration and Dashboard regressions, normal Reports close, and normal application shutdown.
- Correction verification visibly matched the controlled Today and This Week reports to independent MySQL values (1 session, 6 questions, 4 reviewed, 2 pending, 2 correct, 2 wrong, and 50.00% reviewed accuracy), opened the real three-sheet export in Excel, rendered the real PDF without layout defects, returned safely to Administration, and shut down normally.
- Final verification removed temporary database data, report files, render output, lock probes, and generated artifacts. Issue #12 Reports was delivered by merged PR #49 at merge commit `8b99f1cf50388e74e77558efc86fec7d3dac3300`; GitHub issue #16 is complete.

## 2026-07-23

### Issue #46 Post-Merge Finalization

- PR #47 merged Configurable Quiz Sample Size into `main` at merge commit `a13fbbea4d6d0ff27201a9378bca5109c259298c`, delivering feature commit `493848b1dda83cff0b361bd0a5e89facc1b4fa51` and navigation correction `b3f84152219ac60ffe1343f1fda4c98671d82f1f`.
- GitHub automatically closed Issue #46 as completed, and the remote feature branch was deleted after the merge.
- The navigation correction makes `HomeViewModel` validate and raise the training-navigation event while `HomeWindow` exclusively creates `QuizWindow`. One active quiz is permitted; Home hides during training and returns after completion or cancellation.
- The correction passed 280 focused assertions in addition to the original 1,140-assertion feature suite. Visible verification passed rapid-click duplicate prevention, selected sizes 10 and 20, Home restoration, early cancellation, one ResultWindow after completion, and normal shutdown.
- Local `main` was fast-forwarded to the merge commit, the local `issue-46-quiz-sample-size` branch was deleted, and this documentation branch was created from the merged baseline.
- The protected `stash@{0}` remained untouched. No Reports production work was started.

### Configurable Quiz Sample Size

- Created and assigned GitHub issue #46, `Feature - Configurable Quiz Sample Size`, then implemented it on `issue-46-quiz-sample-size` from `origin/main` commit `07fb441fb5262c10a189650c41affd64f6bd5e79`.
- Preserved the public `ImageService.LoadImages(string, bool)` signature and complete-catalog behavior. Added a quiz-only API that accepts only 10 or 20, rejects unsupported values before folder access, removes case-insensitive duplicate paths, performs one Fisher-Yates shuffle, and takes a bounded metadata sample.
- Added a native MVVM-bound 10/20 selector to Home with a default of 10 and passed the selected size explicitly through `QuizWindow` into `QuizViewModel`.
- Kept progress, completion, ResultWindow totals, session totals, and answer persistence driven by the actual sampled image count. Valid requests use every available unique image when fewer than requested are present and show a fixed non-sensitive notice.
- Preserved the current/upcoming two-bitmap cache and unrestricted administrator catalog loading; `AdminViewModel` was not changed and does not use the quiz sampler.
- Built Debug after every modified C# and XAML file. Final Debug and Release rebuilds each passed with 0 errors and the same 3 existing warning lines: one `MVVMTKCFG0002` line and two `CS0067` lines from temporary and final WPF compilation.
- Passed a temporary deterministic configurable-quiz probe with 1,140 assertions: ImageService 828, Home selection 15, quiz progress/completion 212, cache/cancellation 25, session/answer persistence 42, administrator inventory/preview 4, and login/Result/Dashboard regressions 14.
- Visible WPF acceptance passed normal startup to Login, trainee login, the Home 10/20 options and default, real 10- and 20-question quizzes, all images visibly distinct within each quiz, dynamic progress, exact final completion, one ResultWindow per quiz, Result totals of 10 and 20, and safe early cancellation.
- A temporary MySQL probe passed 9 assertions for the visible sessions: `TotalQuestions`, answer rows, and distinct image IDs were 10/10/10 and 20/20/20; the cancelled quiz created no incomplete session.
- Visible administrator acceptance passed unrestricted access to all 30 created answer rows, selected-image remapping between image IDs 8 and 18, Dashboard navigation, existing Reports navigation without modification, normal window closing, and normal application shutdown. The configured folder contained 20 BMP files, while the controlled 105-image automated catalog remained unrestricted.
- Controlled visible fewer-image folder tests were not run because they require preparing a separate local configured folder. Deterministic automation passed 7-of-10, 14-of-20, empty-folder, missing-folder, uniqueness, progress, result, and persistence behavior.
- Removed the two visible acceptance sessions and all 30 answer rows, verified zero remnants, and removed all temporary probe sources and executables. The known `stash@{0}` remained untouched and Reports was not modified.
- At the end of the initial implementation run, Issue #46 awaited pull-request review and merge; the post-merge finalization entry above records its subsequent completion through PR #47.

## 2026-07-19

### Dashboard Analytics

- Replaced the dashboard's all-time summary cards with five consistently scoped local-day metrics: Today's Training, reviewed-only Average Accuracy, Time Spent, GOOD Count, and NG Count.
- Used parameterized half-open boundaries (`StartTime >= @DayStart` and `StartTime < @DayEnd`) instead of applying a date function to the indexed session column.
- Kept session duration aggregation separate from answer aggregation so joining answer rows cannot multiply completed-session time.
- Excluded null, negative, and malformed completed durations; returned N/A for a zero reviewed-answer denominator; and retained pending trainee GOOD/NG selections without counting them as wrong.
- Preserved the deterministic recent-session order and limit, safe null mapping, existing public dashboard properties, and native WPF styling.
- Moved dashboard loading off the dispatcher, disabled repeated refresh while busy, replaced rather than appended recent rows, cleared stale values after failure, logged technical exceptions, and showed only a fixed non-sensitive error status.
- Passed the controlled MySQL dashboard probe with 49 assertions covering the expected 1 session, 10 minutes, GOOD 3, NG 3, reviewed 4, correct 2, wrong 2, and 50% reviewed accuracy, plus yesterday exclusion, incomplete sessions, empty days, half-open boundaries, invalid durations, refresh deduplication, safe failures, and recent ordering/limit.
- Passed the Result Module regression probe with 76 assertions and the Issue #9 regression probe with 29 assertions.
- Built Debug with 0 errors and 1 existing warning and Release with 0 errors and 3 existing warnings.
- In a visible WPF run, administrator login and normal navigation opened exactly one Dashboard. The live MySQL-backed values were 1 completed session, N/A reviewed accuracy with 0 reviewed and 10 pending, 4 seconds, GOOD 3, and NG 7. Refresh preserved the same values and newest-first rows without duplication, and closing Dashboard returned safely to Administration.
- The trainee quiz and ResultWindow workflow was covered by automated regressions but was not rerun visibly during this dashboard session. Computer control stopped after detecting user input during the separate application-close attempt.

### Issue #10 Result Module Acceptance Finalization

- Confirmed merged PR #41 delivered the Result Module and that `origin/main` contains diagnostic-dialog correction commit `bee4eb0`.
- Recorded successful real WPF acceptance: login, ten-question quiz completion, removal of the unexpected diagnostic dialog, exactly one ResultWindow, pending statistics and filters, selected-image preview, window closing, and no observed runtime error.
- Passed the controlled reviewed six-answer ResultWindow dataset with 6 total answers, 3 user GOOD, 3 user NG, 4 reviewed, 2 pending, 2 correct reviewed, 2 wrong reviewed, and 50% reviewed accuracy.
- Confirmed ResultWindow filters returned All 6, Reviewed Wrong 2, User NG 3, and Pending Review 2. NG analysis returned 2 reviewed actual NG, 1 detected NG, 1 false NG, 1 missed NG, a 50% detection rate, and a 50% false-NG rate.
- Confirmed the MySQL completed attempt contained exactly one session row and ten answer rows with five GOOD, five NG, and all ten pending administrator review.
- Reconfirmed that pending answers are never treated as wrong and remain excluded from reviewed-only accuracy and NG truth classifications.
- Passed the focused Result Module probe with 76 assertions, including WPF ResultWindow instantiation, display, dispatcher pumping, closing, and binding diagnostics.
- Passed the Issue #9 image regression probe with 29 assertions covering the bounded current/next cache, progress, input paths, stale-image prevention, source-file release, and close-during-load cleanup.
- Rebuilt Debug and Release with 0 errors and the same 3 existing warnings in each configuration.
- Removed all ignored temporary Result Module and Issue #9 probe sources and executables after verification.
- Issue #10 is implemented, manually accepted, and ready for finalization. Issue #11 Dashboard Analytics is the next planned task and has not started.

## 2026-07-18

### Result Module

- Moved cohesive result calculations into `StatisticsService` and added an immutable `ResultStatistics` snapshot so caller list or answer mutations cannot silently alter a displayed result.
- Added total, GOOD/NG distribution, review coverage, reviewed-only accuracy, valid timing, and NG-classification metrics with safe zero denominators and pending-review handling.
- Defined reviewed accuracy as correct reviewed answers divided by reviewed answers. Pending answers remain available for distribution and timing but are excluded from correct, wrong, detection, and false-NG classifications.
- Added read-only All, Reviewed Wrong, User NG, and Pending Review filters without modifying the source snapshot.
- Added a selected-answer detail view with one asynchronous detached preview. The shared decoder uses `BitmapCacheOption.OnLoad` and `Freeze`; cancellation, selection generation, and disposal checks prevent stale or post-close publication.
- Added native labeled WPF bars for GOOD/NG distribution, reviewed/pending coverage, reviewed correct/wrong outcomes, reviewed accuracy, NG detection, and false-NG rate. Each visual includes text rather than relying on color alone.
- Preserved the existing `ResultWindow(List<QuizAnswer>)` entry point, read-only result behavior, transactional save flow, and Issue #9 current/next cache.
- Built Debug after each completed C# or XAML change with 0 errors and only the existing Toolkit configuration and unused Home event warnings.
- Passed a temporary Result Module probe with 76 assertions covering explicit empty, pending, reviewed-correct, mixed, and invalid-timing datasets; snapshot isolation; filters; preview detachment and file release; missing/corrupt images; rapid selection; close cancellation; WPF window lifecycle; and binding diagnostics.
- Passed a temporary Issue #9 decoder regression probe with 29 assertions covering current and next loading, the two-entry cache bound, eviction, stale-image prevention, G/N action paths, progress, file release, and close-during-load cleanup.
- The automated probe instantiated, showed, pumped, and closed the real `ResultWindow` class with a controlled reviewed dataset. Full splash, login, quiz, MySQL persistence, and human visual checks in the real application remain to be run before merge.
- At the end of this implementation entry, Issue #10 was awaiting manual application testing; that acceptance completed successfully on 2026-07-19. Issue #9 was merged as PR #40 and is complete.

### Quiz Optimization

- Reworked quiz bitmap display so the active image is decoded off the WPF UI thread and only the active image plus one upcoming image are retained in a bounded two-entry least-recently-used cache.
- Bitmap loading reads the source into memory, uses `BitmapCacheOption.OnLoad`, and freezes each image before it crosses threads, releasing source files after successful or failed decode attempts.
- Added cancellation, generation checks, cache clearing, and one-time `IDisposable` cleanup so stale preload completion cannot replace a newer question or survive window closure.
- Corrected a post-decode cleanup race: active and upcoming image continuations now validate cancellation, disposal, generation, quiz state, and image ownership before caching, then validate again while holding the cache lock so cleanup cannot be followed by a stale cache insertion.
- Disabled answer commands while the active image is loading, unavailable, completed, or shutting down. Missing or corrupt images stop the incomplete quiz without saving it as completed.
- Hardened local G, N, and Escape handling to reuse existing commands, ignore repeat and queued shortcut input, handle the key once, and retain one exit confirmation.
- Added bindable question, total, answered, remaining, and answered-percentage values. The percentage is derived only from accepted answers and ranges from 0% before the first answer to 100% after the last answer.
- Verified bitmap detachment, missing/corrupt image failures, bounded cache eviction and cleanup, cache reuse, progress boundaries, duplicate protection, and incomplete-session save guarding with a temporary non-UI probe.
- Verified the active and upcoming cache-continuation cleanup races with a temporary non-UI probe that paused each continuation at cache validation, disposed the view model, and confirmed the settled task could not alter image state, status, progress, or the cleared cache. The probe also reconfirmed normal current/next caching, frozen image file release, decode failures, and pending-review engine state.
- Launched the real WPF application after correcting a discovered progress-binding error and observed the splash and Home windows remain running without a new application-error log. User input then occurred before login or quiz interaction could be verified; interactive quiz verification remains required.

### Global Error Handling

- Added controlled application-wide WPF dispatcher, task scheduler, and AppDomain exception handling.
- Dispatcher failures are logged once, show only a fixed non-sensitive message, are marked handled during controlled shutdown, and cannot open duplicate dialogs.
- Cached the validated configured log folder during startup configuration loading so fatal handlers do not perform configuration discovery.
- Added `%LocalAppData%\VisualInspectionTrainingSystem\Logs` as the safe fallback when the configured log folder is unavailable or unwritable.
- Added bounded, serialized diagnostic entries with UTC timestamps, unique error IDs, handler category, exception type, sanitized message, bounded stack trace, bounded inner exceptions, aggregate exception types, and termination classification.
- Redacted connection strings, passwords, user identifiers, tokens, API keys, and configuration-secret values from diagnostics.
- Restored deterministic optional-image inventory probes and made caller and startup cancellation win over a stalled optional filesystem operation.
- Built Debug and Release successfully with 0 errors and 3 existing warnings in each configuration.
- Verified logger formatting, redaction, configured-folder logging, concurrent writes, contained logger failure, task observation, AppDomain termination classification, optional image timeout/cancellation, required configuration failure, database timeout, and dispatcher responsiveness with temporary probes.
- Verified an actual terminating AppDomain exception in a bounded child probe. The test-only child set `SEM_NOGPFAULTERRORBOX`, exited with the expected non-zero CLR fatal code, and wrote exactly one terminating entry; this expected probe termination is distinct from a normal application failure.
- The sandbox did not expose the real WPF windows reliably, so a manual interactive splash/login launch and visible dispatcher fatal-dialog confirmation remain required outside this environment.

## 2026-07-14

### Splash Timeout Hardening

- Reworked `SystemInitializerService` optional image inventory so synchronous filesystem work is guarded by a true bounded wait.
- Added timeout handling that marks optional image inventory as skipped and allows startup to continue after required checks pass.
- Added abandoned-task observation for timed-out configuration, database, and image inventory tasks so later exceptions are consumed.
- Hardened configuration loading with the same bounded-wait pattern because local configuration discovery also performs synchronous filesystem work.
- Preserved the existing MySQL connection timeout and retry implementation in `MySqlService` without duplicating retry behavior.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug after the modified C# service file.
- Verified normal local image inventory, missing image folder handling, filesystem exception handling, optional image timeout, and cancellation during image inventory with temporary probes.
- Verified normal startup, required configuration failure, and existing database timeout behavior with temporary probes.
- Verified the WPF splash dispatcher remained responsive and opened exactly one login window when the optional image inventory timed out.
- Removed all temporary probe files and local placeholder configs after testing.

### Splash Screen Improvement

- Reworked `SystemInitializerService` so startup initialization runs asynchronously with cancellation support and duplicate initialization protection.
- Reused `MySqlService` for startup database resiliency and added an outer bounded wait so startup cannot hang when a MySQL handshake does not return promptly.
- Added non-sensitive startup result and diagnostic models for success, cancellation, timeout, configuration failure, service failure, and unexpected startup failure states.
- Updated `SplashViewModel` so initialization starts from the splash window lifecycle instead of the constructor, exposes status/diagnostics, and prevents duplicate completion events.
- Updated `SplashWindow` lifecycle handling so startup begins after the window is visible, close requests cancel initialization, and the login window opens exactly once.
- Updated the splash XAML to show diagnostics and an Exit action while keeping standard WPF controls and inline styling.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug after each modified C# file.
- Final build succeeded with 0 errors and 1 warning.
- Verified normal startup with valid local configuration and MySQL access using a temporary startup probe.
- Verified missing configuration, malformed configuration, unavailable database, bounded startup timeout, unexpected initialization exception handling, duplicate initialization prevention, and close-during-initialization cancellation with temporary probes.
- Verified the WPF splash dispatcher remained responsive and opened exactly one login window with a temporary WPF probe.
- Removed all temporary probe files and local placeholder configs after testing.

## 2026-07-13

### Repository Validation Hardening

- Updated `UserRepository` so null or malformed `IsActive` data maps to inactive instead of active.
- Confirmed `AuthenticationService` rejects a database user whose `IsActive` value is null.
- Added a nullable `DuplicateKey` column and unique index for the completed-session duplicate rule.
- Added duplicate-key migration handling that backfills only unique historical completion rows before creating the unique index.
- Preserved the existing transaction that saves a session header and all quiz answers atomically.
- Added duplicate-key handling that reports a clear non-sensitive duplicate-completion message.
- Rewrote `AnswerRepository.RecalculateSession` to update correct, wrong, and accuracy values from one conditional aggregate.
- Rewrote `DashboardRepository.GetMetrics` to consolidate session and answer scans.
- Rewrote `ReportRepository.GetSummary` to consolidate filtered session and answer totals.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug after each modified C# repository file.
- Verified null activation rejection, simultaneous duplicate save handling, forced answer insert rollback, pending answer loading, dashboard metrics, and report summary totals with a temporary repository hardening probe.

### Repository Validation

- Reviewed all repository classes for invalid parameters, unsafe null handling, duplicate persistence risks, `SELECT *` usage, deterministic ordering, and SQL parameterization.
- Rewrote `SessionRepository` validation for completed sessions, answer collections, score totals, completion dates, and already-saved sessions.
- Added database-backed duplicate completed-session detection inside the existing quiz save transaction.
- Rewrote `AnswerRepository` validation so null answer collections, null answer elements, invalid ImageID values, invalid GOOD/NG values, inconsistent reviewed answers, and invalid answer timing are rejected before SQL.
- Updated answer mapping so pending `CorrectAnswer` values load as null and are not counted as wrong.
- Updated admin review recalculation to require exactly one affected answer/session row and keep pending answers out of wrong-answer totals.
- Updated `UserRepository` to validate EmployeeNo and password-hash inputs before SQL and to map nullable user columns safely.
- Updated `DashboardRepository` to parameterize the recent-session limit and stop replacing required session values with misleading defaults.
- Updated `ReportRepository` to validate date ranges and calculate correct, wrong, pending, reviewed, and accuracy values from answer aggregates.
- Updated `ImageRepository` folder validation and deterministic file ordering before optional shuffling.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug after repository changes.
- Verified invalid input validation, successful quiz save, duplicate completion blocking, pending review loading, transaction rollback on forced answer insert failure, and normal connection behavior with a temporary repository probe.

### Connection Resiliency

- Added configurable MySQL connection timeout, retry count, and retry delay settings to the local XML configuration.
- Updated `ConfigurationService` to parse, validate, and apply connection resiliency settings when building the MySQL connection string.
- Rewrote `MySqlService` connection opening to use a limited retry policy for transient connection failures.
- Added asynchronous connection testing with cancellation support for startup checks.
- Prevented retries for configuration errors and detected authentication/setup failures.
- Updated `SystemInitializerService` so the splash startup database check awaits a bounded asynchronous connection test instead of blocking indefinitely.
- Added safe, non-sensitive connection failure messages without exposing passwords or full connection strings.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug.
- Final build succeeded with 0 errors and 1 warning.
- Verified valid database connectivity with a temporary probe.
- Verified invalid host retry behavior, simulated stopped MySQL behavior through an unavailable local port, bounded timeout behavior, invalid credential non-retry behavior, and invalid retry configuration handling with temporary probes.
- Launched the WPF application from the Debug build output and stopped it after confirming the process stayed running for 8 seconds.

### Database Transactions

- Updated completed quiz persistence so the session header and all answer rows are saved in one MySQL transaction.
- Moved table creation checks outside data transactions to avoid MySQL implicit DDL commits.
- Updated standalone answer batch persistence to use one MySQL transaction.
- Updated admin answer review so the answer update and parent session statistics recalculation use the same connection and transaction.
- Added transaction-aware repository methods that accept `MySqlConnection` and `MySqlTransaction`.
- Added row locking for admin review lookups and parent session recalculation.
- Added rollback behavior with non-sensitive exception context.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug.
- Verified successful quiz save, forced answer persistence rollback, successful admin review, and forced recalculation rollback with a temporary transaction probe.

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

### Secure Configuration

- Removed MySQL connection details from `App.config`.
- Added workstation-local database settings loading through `DatabaseSettings.local.config`.
- Added `DatabaseSettings.example.config` with placeholder values only.
- Updated `MySqlService` to build the MySQL connection string through `ConfigurationService`.
- Kept the existing repository and service APIs unchanged.
- Added a clear missing or invalid configuration error that does not include the database password.
- Added explicit ignore coverage for `DatabaseSettings.local.config`.
- Added local setup instructions to `README.md`.
- Built `VisualInpsectionTrainingSystem.slnx` in Debug.
- Final build succeeded with 0 errors and 1 warning.
- Verified missing local configuration handling with a temporary probe.
- Verified invalid local configuration handling with a temporary probe.
- Launched the WPF application from the Debug build output and stopped it after startup validation.
- Full valid-credential login validation was blocked because no accepted local MySQL password is available in this shell.
- Sanitized Git history check found older `App.config` commits containing a redacted `Pwd=` value; history was not rewritten automatically.

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
