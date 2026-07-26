# CHANGELOG

## Unreleased

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
