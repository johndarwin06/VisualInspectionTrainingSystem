# CHANGELOG

## Unreleased

### Version 1.0 Portable Release

- Added allowlisted PowerShell build and fail-closed validation tooling for `VisualInspectionTrainingSystem-v1.0.0-win-portable.zip`; no installer or installer dependency is included.
- Set assembly/file metadata to 1.0.0.0 and product/informational metadata to 1.0.0 while retaining .NET Framework 4.6.2, C# 7.3, the existing executable spelling, package versions, MVVM behavior, and configuration architecture.
- Packaged only the verified Release executable/configuration, required managed libraries, x86/x64 SkiaSharp and HarfBuzz native files, placeholder-only example configuration, portable directory markers, and Version 1.0 documentation. Local settings, credentials, production/test data, images, logs, exports, symbols, XML documentation, ARM64, tests, source, NuGet packages, repository metadata, and generated output are excluded.
- Added internal per-file SHA-256 and external ZIP checksum generation plus exact payload, framework/version, native dependency, portable path, placeholder, private-path, forbidden-content, tamper, cleanup, and two-location extraction validation.
- Visible acceptance passed all administrator and trainee workflows from two independent writable extracted locations, including both quiz sizes, exactly-one Result behavior, Dashboard, Reports and portable exports, History/detail, themes, keyboard/resizing, logout, shutdown, and output-path isolation.
- Execution on a clean machine or VM containing only .NET Framework 4.6.2 remains Not Run and is required before the `v1.0.0` tag or GitHub Release is created.

### Performance Testing

- Added permanent Issue #18 / GitHub issue #22 Release-oriented performance coverage for startup components, WPF workspace lifecycles, quiz interaction, image inventories through 5,000 files, 10/20-image sampling, CSV/XLSX/PDF exports through 5,000 rows, concurrent logging, memory/resource cleanup, and protected MySQL workloads through 501 users, 500 sessions, and 10,000 answers.
- Extended the regression runner with explicit `Baseline`, `Database`, and `All` performance modes plus x86/x64 selection. Normal functional and CI runs exclude performance workloads; protected database performance fails closed unless the retained Issue #19 test boundary is valid.
- Removed the proven 5,000-image selection bottleneck by sampling before cache metadata checks and hashing only selected files. Median/p95 improved from 1,375.733/1,424.686 ms to 31.370/36.779 ms for 10 images and from 1,325.052/1,354.804 ms to 34.016/41.133 ms for 20 images.
- Replaced the unbounded image-hash cache with a thread-safe 4,096-entry least-recently-used bound while preserving SHA-256 duplicate identity, cancellation, unbiased sampling, supported quiz sizes, and public APIs.
- Final verification passed clean Debug/Release builds, 382 functional tests, 48 required database tests, 4 database preflight checks, 28 Release x64 performance tests, two cleanup checks, and supported x86/x64 native checks with zero failures or skips. Production fingerprints remained stable, zero synthetic rows remained, and approved real WPF acceptance passed.

### Isolated Database Regression Testing

- Added a permanent fail-closed MySQL integration boundary for Issue #19 / GitHub issue #23. User-scope configuration is accepted only after live authentication, exact schema-marker validation, restricted-grant inspection, operational/test identity separation, and production-fingerprint protection; values are never logged or printed.
- Added versioned test-schema contracts and 24 database tests per configuration covering connection and parameter safety, rollback, cleanup, registration/authentication, administrator/trainee isolation, session and review transactions/concurrency, Dashboard, Reports, Training History, and normalized reviewed/pending analytics.
- Synthetic rows use unique reserved `I19T` ownership and are removed in `finally`; explicit Debug and Release cleanup gates verify zero residual rows. The harness never creates or drops a database and cannot redirect to an ambiguous or production-looking schema.
- Final Debug and Release builds completed with zero errors and one existing warning each. The complete run passed 382 tests with 0 failed and 0 skipped, the required-database rerun passed all 48 results, and x86/x64 native checks passed.
- Approved visible WPF acceptance passed both roles against normal production configuration. Production schema and repeated row fingerprints remained stable after reconciling legitimate acceptance activity and one operator-authorized external Workbench user deletion; no synthetic production row or unrelated delta remained.

### Centralized Logging Framework

- Standardized production diagnostics through the existing `ApplicationErrorLogger` and Apache-2.0 `log4net` 3.3.2 provider without adding packages or native dependencies.
- Added a bounded asynchronous UTF-8 file sink with 5 MiB rollover, five retained backups, serialized concurrent entries, bounded flush/shutdown, configured `PathSettings.LogFolder` preference, and `%LocalAppData%\VisualInspectionTrainingSystem\Logs` fallback.
- Added Debug through Fatal severity, UTC timestamps, unique event IDs, components, thread and termination metadata, bounded exception/inner/aggregate diagnostics, duplicate-exception suppression, and centralized redaction of credentials, hashes, tokens, connection strings, configuration secrets, diagnostic paths, and oversized text.
- Integrated safe technical logging into application lifecycle and global handlers, initialization, authentication/session events, security-sensitive account operations, quiz persistence, exports, cancellation, and feature failure boundaries while preserving fixed non-sensitive user messages.
- Added 17 permanent logging tests and native deployment contracts. Final Debug/Release verification passed 296 tests, skipped the 8 database results that require Issue #23's dedicated test-only schema, and failed 0; approved visible WPF acceptance and the read-only normal-log audit passed.

### Permanent Regression Testing

- Added a permanent NUnit regression project for the .NET Framework 4.6.2/C# 7.3 baseline, with categorized Unit, Integration, WPF, Database, Export, NativeDeployment, and explicit ManualRuntime coverage plus a repeatable local runner and pinned secret-free Windows CI workflow.
- Added an STA production-composition test that loads actual WPF resources and validates authorized administrator and trainee workspaces, role boundaries, window construction, ownership, duplicate prevention, and safe closing.
- Corrected the shared Fluent window backdrop/title-bar contract that prevented production workspaces from opening, added Back/Escape navigation to trainee Training Setup and History, and refreshed Quiz presentation while preserving functional and persistence contracts.
- Final Debug and Release validation passed 262 tests with 0 failures and 8 intentional database skips. The skipped tests require a dedicated test-only schema, belong to planned database-testing Issue #23, and must never run against production data.
- Approved visible acceptance passed real Dashboard/Reports data, both trainee Back/Escape flows, repeated navigation, 10/20-question quizzes, themes, mouse/keyboard input, persistence, exactly-one Result behavior, logout, and shutdown without a crash, freeze, sensitive error, diagnostic dialog, or `Workspace unavailable` state.

### .NET Framework 4.6.2 Compatibility

- Retargeted the complete application and runtime declaration from .NET Framework 4.8.1 to 4.6.2 while preserving C# 7.3, WPF, MVVM, MySQL, Fluent UI, security rules, analytics definitions, exports, and all existing workflows.
- Audited all 53 NuGet packages and selected compatible framework assets without downgrades. Updated `System.ValueTuple` from 4.5.0 to 4.6.2 for its supported `net462` assembly and validated generated binding redirects and output dependencies.
- Replaced only WPF accessibility properties unavailable on 4.6.2 with supported semantic equivalents. No production C# business logic, public API, database schema, or stored password hash changed.
- Passed Debug/Release AnyCPU and supported x86/x64 builds, clean restore and vulnerability audit, native WPF-UI/LiveCharts/SkiaSharp/HarfBuzz deployment inspection, 50 critical compatibility assertions, MySQL report checks, and real CSV/XLSX/PDF generation with exact cleanup.
- Local visible acceptance passed the complete administrator and trainee application on the development machine's newer in-place CLR. Testing on an isolated machine or VM containing only the 4.6.2 runtime remains **Not Run** and is a documented deployment limitation.
- .NET Framework 4.6.2 support ends January 12, 2027; migrate to a supported runtime before that date.

### Fluent UI Migration

- Replaced the merged MahApps/Material Design presentation with a cohesive Windows 11 Fluent interface using WPF-UI 4.3.0 and WPF-UI.Violeta 4.3.0.3 across every production screen, dialog, loading state, empty state, validation state, and error state.
- Preserved the role-aware shell, administrator and trainee authorization, MySQL behavior, MVVM command and lifecycle contracts, 10/20-question quizzes, exactly-one Result behavior, Training History, Review Workflow, User Management, Dashboard, Reports, exports, cancellation, and safe closing.
- Retained LiveChartsCore 2.0.5 and rethemed shared Dashboard, Reports, and personal analytics charts for Fluent light/dark surfaces with system-aware motion, replace-not-append refresh behavior, explicit empty states, and unchanged reviewed-only analytics meanings.
- Preserved native DataGrid sorting, filtering, virtualization, selection, scrolling, and commands while applying shared Fluent controls, cards, inputs, semantic status chips, accessible names, visible focus, responsive layouts, and native window chrome.
- Removed MahApps.Metro, MaterialDesignThemes, MaterialDesignColors, MaterialDesignThemes.MahApps, and ControlzEx along with obsolete references, imports, dictionaries, namespaces, attached properties, and production dependencies.
- Passed 147 focused assertions, controlled MySQL verification with zero residual rows, AnyCPU and supported x86/x64 Debug/Release builds, native WPF-UI/LiveCharts/SkiaSharp/HarfBuzz deployment checks, `git diff --check`, and complete real WPF administrator/trainee acceptance without a crash, freeze, clipping, binding failure, or sensitive diagnostic.

### Material Design UI and Analytics Overhaul

- Added one role-aware MahApps application shell for authenticated administrator and trainee navigation, with fail-closed destination commands, single-workspace ownership, safe logout handoff, and stable close/minimize/restore behavior.
- Integrated Material Design 3, the Material/MahApps bridge, light/dark application themes, modern window chrome, consistent cards, fields, actions, DataGrids, dialogs, status chips, focus visuals, responsive layouts, and subtle state animations across every production surface.
- Added reusable LiveChartsCore daily analytics for Dashboard, authenticated trainee progress, and bounded Reports periods. Repository aggregation remains parameterized and preserves normalized GOOD/NG, pending/reviewed, nullable accuracy, valid-duration, deterministic ordering, trainee isolation, and repeatable-read report semantics.
- Added safe optional-chart failure handling so valid trainee history remains visible when personal analytics cannot load, while technical details are logged and only a fixed unavailable chart state is shown.
- Verified Debug and Release with zero errors, the existing single MVVM Toolkit warning, 12,000+ focused assertions across UI construction, authorization/navigation, MySQL analytics, Result, quiz sizing, lifecycle, and native deployment, plus approved real WPF acceptance of all administrator and trainee workflows.
- Verified actual x86/x64 SkiaSharp and HarfBuzz native loading and real Cartesian/pie rendering in both Debug and Release. Temporary probes, database fixtures, logs, exports, and generated output are excluded from delivery.

### UI Polish

- Composed the existing design tokens, Material Design resources, and shared application styles into one reusable light theme with consistent colors, spacing, typography, focus visuals, inputs, actions, status treatments, and virtualized DataGrids.
- Added truthful busy overlays bound only to existing loading states, preserving asynchronous ViewModel work, command guards, responsive cancellation, and safe window closing.
- Replaced inconsistent feature dialogs with one keyboard-accessible application dialog that suppresses rapid duplicates, uses semantic icons plus text, retains every confirmation rule, logs technical failures, and displays only fixed non-sensitive messages.
- Improved responsive Grid and scroll layouts, declared minimum sizes, long-text trimming/wrapping and ToolTips, proportional image presentation, keyboard navigation, accessible names, visible focus, and non-color-only GOOD/NG and status communication across all production windows.
- Preserved MVVM ownership, public APIs, database behavior, .NET Framework 4.8.1, C# 7.3, package versions, export formats, authentication, review, Dashboard, Reports, and 10/20-question quiz behavior.
- Passed 430 temporary automated assertions and real WPF acceptance across administrator and trainee navigation, registration validation, dialogs, Dashboard, Reports/export dialogs, both quiz sizes, Result analysis and preview, logout, and shutdown. Separate Windows 125%/150% scale changes and a deliberately prolonged visible loading operation remain Not Run; deterministic layout and lifecycle coverage passed.
- Added secure, read-only My Training History and session details for the active trainee. The application derives identity from the signed-in session, applies it to every list/detail query, pages completed sessions deterministically, and supports bounded date, status, and session/image search without exposing another employee's data.
- Added normalized reviewed-only statistics, pending/correct/wrong presentation, review-source labels without reviewer identity, duplicate-free refresh and incremental loading, empty states, and lazy safe image preview. Database and preview operations remain asynchronous, cancellable, guarded against late UI updates, and limited to fixed non-sensitive errors.
- Passed 123 focused history assertions and 182 expanded regression assertions with controlled MySQL data and exact cleanup. Visible WPF checks passed current-user list/detail isolation, filtering, refresh, statistics, a real preview, navigation, administrator regressions, logout, and shutdown; separate 125%/150% scale changes and deliberately adverse visible history cases remain Not Run.

### User Management

- Added administrator workflows for account creation, activation, deactivation, canonical Admin/User role changes, and password resets with BCrypt hashing and repository-owned transactions.
- Added public trainee registration from Login. Registration accepts no role or activation choice, creates an inactive User account, and requires administrator activation before login.
- Added safe duplicate Employee Number handling, input validation, self/final-administrator protection, fail-closed authorization, fixed non-sensitive user messages, and technical error logging.
- Kept Login and registration responsive with asynchronous work, busy guards, cancellation, observed abandoned tasks, and protection against late UI updates or duplicate registration windows.
- Verified User Management and registration with 186 automated assertions, final Debug and Release rebuilds, and real WPF acceptance covering registration, activation, authorization, Review Workflow, Dashboard, Reports, 10/20-question quizzes, logout, and normal shutdown.

### Review Workflow

- Added SHA-256 identity over exact image bytes and persisted the stable hash plus display filename with every new quiz answer.
- Added one reusable administrator GOOD/NG truth row per stable image hash with reviewer, source answer, timestamps, and version metadata.
- Automatically grades later identical images from reusable truth, including a different trainee selection, while keeping missing or unsupported truth pending.
- Propagates manual truth and corrections to all matching answers and recalculates all affected sessions in one repository-owned transaction with row locking and stale-version protection.
- Added grouped bulk GOOD/NG review, multi-selection counts, search, eleven review filters, fixed non-sensitive errors, and asynchronous loading that disables conflicting work and ignores late completion after close.
- Preserved safe legacy behavior: rows without identity remain individually reviewable, available previews require explicit confirmation before attaching a hash, and unavailable files never propagate by filename.
- Verified stable identity, truth reuse, propagation, corrections, concurrency, bulk and legacy paths, downstream statistics, 10/20-question quizzes, lifecycle handling, and cleanup with 190 automated assertions.
- Visible WPF verification passed the complete administrator workflow, Dashboard and Reports regressions, blocked-refresh command suppression and prompt close, normal logout, and normal shutdown.

### Reports

- Added explicit local-calendar Daily, Monday-to-Sunday This Week, rolling Last 7 Days, This Month, inclusive Custom, and All Dates report periods with parameterized half-open MySQL boundaries.
- Aligned summaries, session rows, and all exports with Dashboard Analytics review semantics: only normalized GOOD or NG truth is reviewed, malformed truth remains pending, valid truth with a missing or unsupported trainee answer is wrong, and empty reviewed denominators display N/A.
- Preserved the bounded 500-row interactive session list with visible disclosure while loading a separate complete export snapshot in deterministic order, subject to a documented 10,000-session safeguard.
- Made each display and export snapshot internally consistent by reading its summary and session rows through one repository-owned MySQL `RepeatableRead` transaction, with rollback and connection cleanup on failure and no transaction held during document generation.
- Moved database loading and CSV/XLSX/PDF generation off the WPF dispatcher, disabled overlapping commands, rejected stale results, observed abandoned work, and made Reports safe to close while an operation is blocked.
- Added complete UTF-8 CSV export, a real three-sheet Open XML `.xlsx` workbook with typed values and frozen headers, and a real A4 landscape multipage PDF with repeated table headers and page numbers.
- Replaced report and export exception disclosure with existing technical logging and fixed non-sensitive user messages; save-dialog cancellation remains a normal non-error outcome.
- Verified the original 240-assertion Reports coverage plus a 426-assertion consistency/regression probe, including deterministic answer-review and session-insertion concurrency, transaction cleanup, controlled MySQL data, real Excel opening without repair, PDF rendering, visible Today/This Week reports, final Debug/Release rebuilds, and zero residual test sessions.

### Configurable Quiz Sample Size

- Added a native MVVM-bound 10/20 trainee quiz-size selector with a default of 10.
- Added a separate quiz-only metadata sampler that validates supported sizes, removes case-insensitive duplicate paths, performs one Fisher-Yates shuffle, and returns at most the selected count.
- Preserved the existing `LoadImages(string, bool)` API and its complete-catalog behavior for administrator review and inventory workflows.
- Made quiz notices, progress, completion, ResultWindow totals, session totals, and answer persistence use the actual selected count, including safe fewer-image and empty-folder handling.
- Preserved the bounded current/upcoming bitmap cache; only metadata is sampled, and the full quiz is not decoded into memory.
- Added fixed non-sensitive quiz startup and persistence diagnostics through the existing application logger.
- Verified the feature with 1,140 automated assertions, 9 visible-session MySQL assertions, final Debug/Release rebuilds, and real 10- and 20-question WPF quizzes. Controlled visible fewer-image folder tests remain Not Run; their behavior passed deterministic automation.

### Dashboard Analytics

- Added five local-day dashboard metrics: completed training sessions, reviewed-only average accuracy, valid completed-session time, trainee GOOD selections, and trainee NG selections.
- Applied parameterized half-open local-day boundaries without wrapping `StartTime` in a SQL date function.
- Separated session and answer conditional aggregates so answer joins cannot multiply duration totals.
- Excluded incomplete, negative, and malformed durations from Time Spent and displayed N/A when no reviewed answers exist.
- Kept pending answers out of reviewed accuracy and wrong counts while retaining pending trainee GOOD and NG selections.
- Added asynchronous refresh with a busy guard, atomic metric/session replacement, deterministic recent-session ordering, and stale-state clearing after failure.
- Replaced raw dashboard database failures with existing technical logging and one fixed non-sensitive status message.
- Updated the native WPF dashboard cards with explicit Today and reviewed-only labels while preserving resizing and normal administrator navigation.

### Result Module

- Delivered the Result Module through merged PR #41.
- Removed the accidental quiz-startup diagnostic dialog in commit `bee4eb0` and retained one fixed, non-sensitive startup failure message with safe application-error logging.
- Completed manual acceptance for real login and ten-question quiz completion, exactly one ResultWindow, pending statistics and filters, selected-image preview and closing, a controlled six-answer reviewed dataset, and MySQL session and answer-row persistence.
- Confirmed the reviewed-data calculations, NG analysis, and filters remain correct and that pending answers are not treated as wrong.
- Added snapshot-based result statistics for answer distribution, review coverage, reviewed-only accuracy, valid timing, and NG analysis.
- Added read-only All, Reviewed Wrong, User NG, and Pending Review answer filters with selected-answer details.
- Added asynchronous, detached single-image result previews that release source files and reject stale or post-close updates.
- Added native labeled WPF bars for GOOD/NG distribution, reviewed outcomes, review coverage, reviewed accuracy, NG detection, and false-NG rate.
- Kept pending answers out of correct/wrong and reviewed-truth metrics while retaining their user distribution and valid timing values.
- Preserved the existing quiz-to-result constructor and flow, transactional persistence, and Issue #9 two-image quiz cache.

### Quiz Experience

- Added bounded asynchronous quiz image loading with a two-image current/next cache, frozen detached bitmaps, cancellation, stale-load protection, and lifecycle cleanup.
- Disabled answers until the current image is ready, prevented incomplete image-failure sessions from saving, and retained the existing engine duplicate-answer protection.
- Added answered-based quiz progress values and display for question, total, answered, remaining, and completion percentage.
- Hardened local G, N, and Escape shortcuts so repeated input cannot create duplicate answer paths and confirmed exit cleans up incomplete quizzes.

### Application Reliability

- Added controlled WPF dispatcher, task scheduler, and AppDomain global exception handling.
- Added one non-sensitive fatal-error notification before dispatcher-driven application shutdown and suppressed duplicate notifications.
- Added bounded, sanitized global error diagnostics with UTC timestamps, unique error IDs, handler categories, stack traces, inner exceptions, aggregate exception classifications, and termination status.
- Redacted passwords, connection strings, user identifiers, tokens, and configuration secrets from error diagnostics.
- Cached the validated configured log folder during startup and added a LocalAppData fallback when it cannot be used.
- Strengthened optional image-inventory cancellation so caller and startup cancellation return promptly while abandoned filesystem work is observed.

### Database

- Added repository input validation before SQL execution for sessions, answers, users, dashboard limits, report date ranges, and image folders.
- Added duplicate completed-session protection inside the quiz persistence transaction.
- Added a database-enforced completed-session duplicate key and unique index while preserving transactional session and answer persistence.
- Added duplicate-key migration handling that backfills unique historical completion rows and leaves historical duplicate groups nullable so the unique index can be created safely.
- Changed user activation mapping to fail closed when `IsActive` is null or malformed.
- Updated repository null handling so pending quiz answers keep `CorrectAnswer` as null and are not counted as wrong.
- Updated report calculations to use answer aggregates for correct, wrong, pending, reviewed, and accuracy values.
- Optimized dashboard metrics, report summaries, and session recalculation with conditional aggregation to reduce repeated table scans.
- Parameterized dashboard recent-session limits and kept deterministic ordering on session queries.
- Made completed quiz session persistence atomic with MySQL transactions.
- Made answer batch persistence atomic with MySQL transactions.
- Made admin answer review and session statistics recalculation atomic with MySQL transactions.
- Prevented partial session, answer, or recalculated-result writes when a related operation fails.
- Added configurable MySQL connection timeout and limited transient retry behavior.
- Prevented authentication and invalid configuration failures from being retried.

### Startup

- Updated splash startup database validation to use a bounded asynchronous connection check.
- Added clear non-sensitive database unavailable messages when MySQL remains unreachable.
- Changed splash startup initialization to begin asynchronously after the splash window is visible.
- Added bounded startup timeout handling for database checks that do not return promptly.
- Added non-sensitive splash diagnostics for configuration failures, unavailable services, timeouts, cancellation, and unexpected startup failures.
- Prevented duplicate startup initialization and duplicate login window opening from the splash screen.
- Added a splash Exit action that safely cancels startup initialization.
- Hardened configuration loading with a bounded wait so required startup initialization cannot hang on stalled filesystem discovery.
- Hardened optional image inventory with a bounded wait; unavailable or stalled image folders are skipped without blocking login.

### Configuration

- Added a unified local XML configuration system for database and application path settings.
- Added strongly typed application, database, and path settings.
- Removed hardcoded quiz image folder usage from source code.
- Added configurable log, export, and report folders.
- Added configurable MySQL connection timeout, retry count, and retry delay settings.
- Report CSV export now uses the configured export folder.
- Removed the unused JSON settings file.

### Security

- Removed MySQL credentials and connection details from `App.config`.
- Added ignored local database configuration through `DatabaseSettings.local.config`.
- Added `DatabaseSettings.example.config` as a safe placeholder template.
- Added BCrypt.Net-Next password hashing support.
- Added migration for existing plain-text user passwords after successful login.
- Preserved compatibility with existing plain-text accounts during migration.
- Prevented incorrect password attempts from updating stored password values.
