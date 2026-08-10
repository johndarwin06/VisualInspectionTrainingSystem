# ARCHITECTURE

## Runtime and Language Compatibility

The application targets .NET Framework 4.6.2 and explicitly compiles with C# 7.3. Development builds require the official .NET Framework 4.6.2 Developer/Targeting Pack and reference assemblies; runtime computers require .NET Framework 4.6.2 or a compatible newer in-place .NET Framework 4.x runtime.

The `packages.config` dependency set is resolved only to audited `net462`, `net461`, `net46`, `net452`, `net45`, `net20`, or verified `netstandard2.0` assets. The project does not reference `net48` or `net481` assemblies. Package-provided support assemblies and generated binding redirects are copied and validated with each clean restore, including the NuGet `System.ValueTuple` 4.0.5 assembly required by the Fluent dependency graph.

WPF accessibility metadata uses properties available in 4.6.2. Heading and live-status intent is retained through supported automation item types where newer heading-level and live-setting attached properties are unavailable. This is a compatibility adaptation only; MVVM ownership, public APIs, authorization, database behavior, analytics meanings, and UI workflows remain unchanged.

Local acceptance compiles against genuine 4.6.2 reference assemblies but executes on the development machine's newer in-place CLR. An isolated machine or VM containing only the .NET Framework 4.6.2 runtime has not been available, so genuine 4.6.2-only runtime qualification remains **Not Run** and is an explicit deployment limitation.

Microsoft support for .NET Framework 4.6.2 ends January 12, 2027. Deployment planning must include migration to a supported runtime before that date.

## Configuration System

The application uses a single local XML configuration file for machine-specific settings:

- Tracked template: `DatabaseSettings.example.config`
- Ignored local file: `DatabaseSettings.local.config`
- Loader: `Services/ConfigurationService.cs`

`App.config` only points to the local configuration file name through `ApplicationSettingsFile`; it does not store database credentials or application folder paths.

## Settings Models

`ConfigurationService` exposes strongly typed settings:

- `ApplicationSettings`
- `DatabaseSettings`
- `PathSettings`

The service centralizes XML parsing, path normalization, required-value validation, directory checks, and MySQL connection string construction.

## Directory Validation

The configured quiz image folder is required to exist. The log, export, and report folders are output folders and are created automatically when missing and when the application has permission.

Startup validation occurs in `SystemInitializerService`. Quiz and admin image loading use the configured quiz image folder through `AppConstants.QuizImageFolder` for compatibility. Report CSV export uses the configured export folder.

## Database Transactions

Completed quiz persistence is coordinated by `SessionRepository.Save`. The session row and all related answer rows are inserted with the same `MySqlConnection` and `MySqlTransaction`; the transaction commits only after every insert succeeds.

Standalone answer batch persistence is coordinated by `AnswerRepository.SaveMany`. All answer rows in the batch use one connection and transaction.

Administrator answer review remains repository-owned. `AnswerRepository.ReviewAnswer` and grouped bulk review lock the selected answers and reusable truth rows, update every answer with the same stable image hash, and recalculate every affected parent session inside one transaction. Truth correction checks the locked version before updating, so stale concurrent work rolls back instead of overwriting newer truth.

Table creation checks run outside data transactions because MySQL DDL can cause implicit commits. Data-changing workflows roll back on exceptions and rethrow non-sensitive context.

## Connection Resiliency

MySQL connection resiliency is centralized in `Services/MySqlService.cs` and configured through the local XML configuration loaded by `ConfigurationService`.

`DatabaseSettings` includes:

- `ConnectionTimeoutSeconds`
- `RetryCount`
- `RetryDelayMilliseconds`

Each connection attempt uses the configured timeout in the generated MySQL connection string. Transient network and server availability failures are retried up to the configured retry count with a short configured delay. Authentication failures, invalid configuration, cancellation, and startup timeout failures are not retried.

Startup database validation runs through `SystemInitializerService` using the asynchronous MySQL connection test and a bounded cancellation token derived from the configured timeout and retry policy. Errors shown to startup flow are non-sensitive and do not include passwords or full connection strings.

## Global Error Handling

`App.xaml` routes WPF dispatcher exceptions to `App.xaml.cs`. The application also subscribes once per application instance to `TaskScheduler.UnobservedTaskException` and `AppDomain.CurrentDomain.UnhandledException`, and removes those subscriptions during normal exit.

A dispatcher exception is treated as fatal: the application records a sanitized diagnostic, shows one fixed non-sensitive message, marks the exception handled as part of controlled shutdown, and requests shutdown once. Reentrant or duplicate dispatcher notifications neither log another dispatcher failure nor show another dialog. Task scheduler exceptions are recorded and explicitly observed; AppDomain exceptions are recorded with their terminating classification.

`ApplicationErrorLogger` does not discover configuration while handling an error. After validated startup configuration is loaded, `SystemInitializerService` supplies the already-known `PathSettings.LogFolder`. The logger first attempts that configured directory and falls back to `%LocalAppData%\VisualInspectionTrainingSystem\Logs` when the primary path is missing, invalid, read-only, locked, or unavailable. Logging errors are contained inside the logging boundary so they cannot recursively reach a global handler.

The repository retains Apache-2.0 `log4net` 3.3.2 as the single dispatch provider. `ApplicationErrorLogger` owns a bounded 2,048-entry queue and one background file writer, so production callers never wait on normal filesystem I/O. The current UTF-8 file is `application.log`; it rolls at 5 MiB and retains five numbered backups. Flush and shutdown use bounded waits, while failures safely abandon logging rather than blocking application exit. No native logging dependency is introduced.

Every entry is written as one complete serialized record with a UTC timestamp, unique event ID, Debug/Information/Warning/Error/Fatal severity, source component, managed thread metadata, termination status, and sanitized message. Exception records add a bounded type/message, stack trace, inner-exception chain, and flattened aggregate types. Credential-like values, password hashes, full connection strings, tokens, configuration secrets, diagnostic filesystem paths, and oversized text are removed or redacted before enqueueing. A weak exception-identity registry prevents the same exception from being recorded again by multiple global boundaries.

Lifecycle, initialization, authentication/session, security-sensitive account actions, persistence, exports, cancellation, and module failures log at their existing service or ViewModel boundaries. Repository failures are not redundantly logged again when the owning feature boundary already records them. Production UI continues to display only fixed non-sensitive messages.

## Application UI Resources and Dialogs

`App.xaml` loads WPF-UI 4.3.0 and WPF-UI.Violeta 4.3.0.3 theme and control dictionaries before `LightTheme`. The application theme then composes the established design tokens, typography, card resources, `Resources/Styles/ApplicationStyles.xaml`, and Fluent-specific resources. `ApplicationThemeService` applies WPF-UI/Violeta light or dark state and the branded accent on the WPF dispatcher, replaces only the application semantic theme dictionary, and publishes one process-wide theme event for chart refresh; it performs no blocking configuration discovery. The intentionally misspelled `Resources/DesignTokems` directory remains for compatibility.

`ModernProgressBar` is the reusable busy overlay. Windows bind its `IsActive` and `Message` properties only to existing ViewModel loading state, so the visual never simulates work or owns business behavior. The overlay is not hit-testable, while the ViewModels retain asynchronous execution, cancellation, duplicate-command guards, generation checks, and late-result rejection.

`ApplicationDialogService` owns ordinary feature confirmations and notifications. It selects the active application window as owner, allows at most one application dialog at a time, activates an existing dialog after repeated input, and uses the existing logger plus a native fixed-message fallback if the styled dialog itself fails. `ApplicationDialogWindow` uses WPF-UI Fluent chrome, semantic `SymbolIcon` values with visible text, Enter for the primary action where safe, Escape for cancellation, and accessible names. Fatal dispatcher handling remains independently owned by `App.xaml.cs` so dialog failure cannot weaken controlled shutdown.

Production feature windows use WPF-UI `FluentWindow` chrome with Mica-compatible backdrops and rounded Windows 11 presentation where supported. Splash retains specialized transparent startup behavior, while Loading and the application dialog use compact Fluent owned-window chrome. Responsive Grid, DockPanel, shared sizing, and bounded scrolling provide practical minimum dimensions. Long names, departments, filenames, validation messages, and report text wrap or trim with ToolTips; quiz and result images preserve aspect ratio. GOOD/NG, pending/reviewed, correct/wrong, enabled/disabled, warning, and error states include labels or semantic symbols and do not depend on color alone. Code-behind remains limited to window ownership, focus and keyboard access, accessibility, save-dialog interaction, and close lifecycle.

The shared production `FluentAppWindowStyle` keeps `ExtendsContentIntoTitleBar` enabled whenever a WPF-UI backdrop is requested. This invariant is covered by production-composition tests because violating it causes workspace construction to fail before an individual feature ViewModel can load. Trainee Training Setup and History return through their existing owner shell; Back and Escape close the owned workspace rather than constructing another shell, and active quiz, Result, History, or detail children retain their close safeguards.

## Regression Test Architecture

`VisualInspectionTrainingSystem.Tests` is a separate NUnit project targeting .NET Framework 4.6.2 and C# 7.3. Production assemblies expose only narrowly scoped internals to that test assembly. Unit and source-contract integration tests remain independent of external services; export tests use unique temporary directories; native-deployment tests inspect and load the supported x86/x64 output assets.

WPF composition tests run on an STA thread with a real dispatcher, load the actual `App.xaml` resource graph, establish controlled role sessions, and create authorized production workspaces through the complete composition path. They verify fail-closed authorization, constructor/resource/service resolution, single-window ownership, Back/Escape lifecycle, and deterministic cleanup. The test-only configuration override accepts only an existing settings file below the operating-system temporary directory and is scoped and disposable, so production configuration discovery is unchanged.

Database integration is an explicit fail-closed boundary delivered by Issue #19 / GitHub issue #23. It reads `VITS_TEST_MYSQL_CONNECTION_STRING` and `VITS_TEST_MYSQL_SCHEMA` from Process or Windows User scope without exposing values. Before any fixture runs, it authenticates with a dedicated account, validates the exact live schema and identity marker, rejects system/ambiguous/production-looking names, proves test and operational endpoint/schema/account separation, inspects grants, caps connection time, disables pooling and persisted security information, and refuses to start when any condition is uncertain.

The dedicated schema is provisioned outside the application and retained between runs. `TestDatabaseSchema` owns only versioned table/index/foreign-key/check contracts inside that already-validated schema; it never creates, selects, or drops a database. `TestDatabaseRunContext` assigns each test a unique reserved `I19T` identity, inserts repository-compatible fixtures, and deletes only run-owned review truth, answers, sessions, and users in dependency order from `finally`. A separate cleanup category independently requires zero reserved rows after Debug and Release execution.

Database tests use the production repositories and services with narrowly scoped injected `MySqlService` settings rather than replacing SQL behavior with broad mocks. They cover registration/authentication, fail-closed user activity conversion, transaction rollback, duplicate/concurrent session and review behavior, parameterized logging redaction, Dashboard, Reports, and trainee History analytics. Production is used only for read-only identity/fingerprint separation gates and visible acceptance; test writes cannot target it. The dedicated schema, marker, restricted account, and User-scope variables are intentionally preserved for future regression runs, while their credential values and populated provisioning script remain ignored.

## Performance Test Architecture

The permanent `Performance` category is evidence-oriented and separate from normal functional and CI execution. `Run-RegressionTests.ps1` exposes explicit `Baseline`, `Database`, and `All` modes plus an x86/x64 process selection. Each measured workload performs warm-up iterations before multiple samples and reports minimum, median, p95, maximum, and sample count. Release x64 is the authoritative local timing configuration, but timings remain informational because shared hardware cannot support a stable universal threshold; functional, cleanup, authorization, and safety assertions remain hard gates.

Secret-free fixtures cover validated configuration loading, real WPF resource and role-window construction on an STA dispatcher, in-memory quiz interaction, temporary image inventories, real CSV/XLSX/PDF generation, concurrent bounded logging, repeated close/disposal, and diagnostic managed-memory deltas. Forced garbage collection is confined to isolated diagnostic checks and is not treated as proof that the CLR or WPF released every framework allocation. Generated benchmark output is intentionally not committed.

Database performance reuses the fail-closed Issue #19 boundary and production repositories against only the dedicated test schema. Each run owns uniquely identified synthetic rows, captures read-only production schema and row-count fingerprints before and after, inspects selected query plans, and removes only its own rows in `finally`. Authentication, Dashboard, Reports, review, user management, and trainee History workloads preserve their normal parameters, transaction boundaries, ordering, normalized GOOD/NG meanings, and role restrictions. Performance mode never falls back to production.

`ImageService` enumerates and sorts lightweight BMP metadata, forms an unbiased quiz sample, and then hashes only the selected 10 or 20 files. Full administrative image loading still computes hashes for every returned item. SHA-256 identity and duplicate detection are unchanged. The shared hash cache validates selected-file length and write time, serializes access under its existing lock, promotes successful reads, removes stale entries, and evicts least-recently-used paths above 4,096 entries so long-running processes cannot retain an unbounded path history.

## Authenticated Application Shell

`MainWindow` is the single authenticated, role-aware application shell. `LoginViewModel` publishes a successful authenticated user without constructing role-specific windows; `LoginWindow` transfers `Application.Current.MainWindow` to the shell before closing. `MainShellViewModel` exposes only navigation requests and live fail-closed role checks. Administrator and trainee destination groups are mutually exclusive in the UI, and `MainWindow` repeats authorization before constructing a workspace.

The shell owns at most one modeless feature window. It disables global navigation while that workspace is active, activates or restores the existing workspace after a repeated request, and re-enables itself only after the child closes. Logout creates and shows Login before clearing the session and closing the shell. Shell closing state is established before WPF closes owned windows so child close callbacks never attempt to reactivate a closing shell. Home and Administration remain focused feature workspaces and no longer provide competing global navigation.

## Analytics Chart Layer

`ChartPoint` and `AnalyticsChartData` are chart-neutral daily models. Repositories own parameterized SQL aggregation, local half-open boundaries, zero-filled ascending day series, normalized supported GOOD/NG semantics, valid-duration filtering, and authorization constraints. `AnalyticsChartViewModel` clones repository data and replaces complete LiveChartsCore series/axis arrays after refresh or theme changes, preventing duplicate or stale series. `ChartThemeService` creates independent semantic SkiaSharp paints for light and dark surfaces, and disposal removes the process-wide theme subscription.

`AnalyticsChartsPanel` is the shared Fluent WPF presentation for completed sessions, trainee GOOD/NG selections, reviewed-only accuracy, valid training time, and reviewed/pending coverage. It uses LiveChartsCore Cartesian and pie charts inside WPF-UI cards, theme-neutral semantic paints, bounded tooltips, nullable gaps for N/A accuracy, legends, system-aware subtle animation, and explicit empty or unavailable states. Dashboard uses 7- or 30-day administrator data, Training History uses authenticated trainee-only 7- or 30-day data, and Reports provides charts only for bounded periods of at most 366 local days. CSV, Excel, and PDF generation never holds or depends on a live chart or database transaction.

## Splash Startup Flow

The splash screen is coordinated by `Views/Splash/SplashWindow.xaml.cs`, `ViewModels/SplashViewModel.cs`, and `Services/SystemInitializerService.cs`.

`SplashWindow` starts initialization from the WPF `Loaded` event after the splash is visible. `SplashViewModel` keeps one initialization task per splash instance, exposes progress, status, and diagnostic properties, and raises completion only once. Closing the splash or pressing Exit cancels the initialization token and prevents the login window from opening afterward.

`SystemInitializerService` returns an `InitializationResult` for required startup checks. Configuration loading runs off the UI thread and is guarded by a bounded wait because configuration discovery performs synchronous filesystem work. If configuration loading exceeds the required-startup timeout, startup fails safely with a non-sensitive timeout result.

MySQL validation reuses `MySqlService`, and the database check is also guarded by an outer bounded wait so startup cannot hang if the connector does not return promptly. The service does not duplicate MySQL retry policy; it only bounds the startup wait around the existing resiliency implementation.

Optional image inventory checks run behind their own short bounded wait. If the configured image folder is unavailable, throws a filesystem exception, or does not respond before the optional timeout, the inventory result is marked skipped and startup continues after required checks pass. Timed-out background tasks are observed so late exceptions do not become unhandled, and late optional work does not update splash UI state.

The login window is opened only by the splash window after a successful startup result, and `Application.Current.MainWindow` is updated to the login window before the splash closes.

## Quiz Image Lifecycle

`ImageService.LoadImages(string, bool)` remains the complete-catalog API used by administrator and inventory workflows. Trainee initialization uses the separate `LoadQuizImages(string, int)` API, which accepts only 10 or 20, loads complete metadata through the existing API without an initial shuffle, removes case-insensitive duplicate paths, applies one Fisher-Yates shuffle, and takes at most the requested count. Metadata sampling does not decode every bitmap. If fewer unique images are available, the sample contains every available image once; the default requested size is 10.

`HomeViewModel` exposes the supported 10/20 choices and owns the selected value. Home passes that value explicitly through `QuizWindow` to `QuizViewModel`; unsupported values are rejected before normal quiz initialization. `QuizViewModel` remains the owner of quiz display state and continues to submit answers only through `QuizEngine`. It builds progress, completion, results, and persistence from the actual sample count, so fewer-image sessions never duplicate questions or persist a requested count that was not used.

`QuizViewModel` loads the active bitmap off the WPF UI thread and preloads only one upcoming image. The cache is a two-entry least-recently-used cache that retains the current and upcoming images; it never loads an entire quiz into memory.

Each bitmap is read into memory, decoded with `BitmapCacheOption.OnLoad`, and frozen before it is shared with the UI or cache. This releases the source file without reducing image decode fidelity. The ViewModel attaches a cancellation token and generation value to image work, observes task failures, and ignores late completion after a question changes or the window closes.

Answer commands are enabled only when the active bitmap is ready. A failed active-image load stops and cleans up the incomplete quiz without persisting it as completed. `QuizWindow` routes local G and N input through the existing commands, prevents repeated/queued keyboard input from becoming a second answer, and retains one Escape exit confirmation. It owns and disposes its ViewModel once when closed.

Quiz progress is derived from accepted answers: `CurrentQuestion`, `TotalQuestions`, `AnsweredQuestions`, `RemainingQuestions`, and `CompletionPercentage` are synchronized by the ViewModel. The percentage is zero before the first accepted answer, 100 after the final accepted answer, and `AnsweredQuestions + RemainingQuestions` always equals `TotalQuestions`.

## Review Workflow

`ImageService` calculates a lowercase SHA-256 digest over exact image bytes and caches it only while the path and file metadata remain unchanged. Quiz image metadata is hashed off the WPF dispatcher. `QuizAnswer` persists the stable `ImageHash` and display-only `ImageFileName`; filename is never treated as identity.

`tbl_image_review_truth` stores one current normalized GOOD/NG truth per hash with the source answer, reviewer, review time, update time, and version. `AnswerRepository.SaveMany` preloads all matching truth rows once per answer batch and automatically grades new matching answers before insertion. A pending trainee GOOD or NG selection remains pending when no reusable truth exists.

Manual individual and bulk review use one repository transaction. Matching answer rows and truth rows are locked, the truth is inserted or corrected, every exact-hash answer is updated with MANUAL or AUTO provenance as appropriate, and all affected session totals are recalculated before commit. Bulk work is grouped by unique stable hash so duplicate selections do not repeat propagation. Version checking turns a stale concurrent correction into a fixed safe failure.

Legacy answers with no hash are deliberately conservative. They can be reviewed individually without propagation. If the original image is available, the administrator may confirm its preview-derived hash before attaching identity; an unavailable legacy file never propagates through a filename guess.

`AdminViewModel` owns asynchronous queue loading, preview selection, filtering, selection, and review commands. A single busy state disables navigation, search, refresh, individual review, and bulk review during database work. Cancellation and operation generations prevent late UI publication, abandoned tasks are observed, and `AdminWindow` disposes the ViewModel when closing. Technical failures go to `ApplicationErrorLogger`; the UI receives only fixed non-sensitive messages.

## User Management and Registration

`UserRepository` owns user persistence, schema compatibility, and transaction boundaries. Administrative mutations run in serialized repository transactions, enforce unique Employee Numbers, and commit only after all validation and authorization invariants succeed. Activation mapping fails closed, roles normalize to the canonical Admin or User values, and password values are stored only as BCrypt hashes. Self-deactivation, self-demotion, and removal of the final active administrator are rejected before commit.

`UserManagementService` is the application boundary for administrator account creation, activation, deactivation, role changes, and password resets. `UserManagementViewModel` exposes these operations asynchronously and supplies only fixed non-sensitive errors to the administrator UI; technical failures use `ApplicationErrorLogger`.

Public registration is a separate least-privilege path. `RegistrationService` accepts Employee Number, full name, department, password, and password confirmation only. It never accepts a role or activation flag: `UserRepository.RegisterInactiveTrainee` always persists the canonical User role with inactive status. Registration never authenticates the applicant or creates a session; the existing Login window remains open, and authentication succeeds only after an administrator activates the account.

`LoginViewModel` and `RegistrationViewModel` perform database work away from the WPF dispatcher. One-operation busy guards, lifecycle cancellation, observed abandoned tasks, and operation-version checks prevent duplicate work and late UI publication. Password controls are cleared after use, raw exceptions never reach a dialog, and the Login window owns at most one Registration window.

## Result Module

`ResultWindow(List<QuizAnswer>)` remains the quiz-to-result entry point. `ResultViewModel` immediately passes the supplied answers to `StatisticsService`, which clones each non-null answer into a read-only `ResultStatistics` snapshot. The result module never writes answers, assigns `CorrectAnswer`, or persists a session; administrator truth remains owned by the Admin module.

Answer distribution uses all snapshot rows with valid user GOOD or NG selections. GOOD and NG percentages divide their respective counts by total snapshot answers. Timing includes only finite, non-negative elapsed values; total, average, fastest, and slowest use that valid-timing subset, and missing timing is displayed as N/A where appropriate.

Review coverage divides reviewed answers by total answers. An answer is reviewed only when `CorrectAnswer` contains a supported GOOD or NG value. Reviewed accuracy divides matching user/truth answers by reviewed answers, never total answers. Pending answers are shown as Pending Review, remain available for distribution and timing, and are excluded from correct and wrong counts.

NG analysis distinguishes trainee selection from reviewed truth:

- User NG rate is trainee NG selections divided by total answers.
- Correctly detected NG is user NG with reviewed truth NG.
- False NG is user NG with reviewed truth GOOD.
- Missed NG is user GOOD with reviewed truth NG.
- NG detection rate is correctly detected NG divided by reviewed actual NG.
- False NG rate is false NG divided by reviewed actual GOOD.

Zero reviewed-truth denominators display N/A rather than a misleading percentage. The All, Reviewed Wrong, User NG, and Pending Review filters replace only the displayed read-only collection and never mutate the statistics snapshot.

The ResultWindow uses native labeled WPF bars for user answer distribution, reviewed correct/wrong outcomes, reviewed/pending coverage, reviewed accuracy, NG detection, and false-NG rate. Every visual also presents its metric name, count, and percentage; zero values remain bounded and pending reviewed accuracy displays Pending Review.

Selected-answer preview uses the shared `ImageService` decoder. It reads the requested file on a worker task, uses `BitmapCacheOption.OnLoad`, freezes the bitmap, and releases the source stream before publication. `ResultViewModel` keeps only one preview, cancels the previous selection token, checks a generation and selected-answer identity, observes task completion, and disposes preview work when the window closes. Missing, unreadable, deleted, or corrupt images produce a fixed non-sensitive unavailable status.

## Trainee Training History

`TrainingHistoryService` is the public authorization boundary for read-only trainee history. It accepts only a query or session ID, captures the active `SessionService.CurrentUser` internally, validates the active canonical role and employee number, and passes that identity to the internal repository. Callers cannot supply another employee number. `TrainingHistoryRepository` repeats the employee constraint in completed-session list, session-summary, and answer-detail queries so direct navigation to another session returns no data; administrator access still resolves only the administrator's own history.

The list uses parameterized half-open completion-date boundaries, bounded exact-session or image-name search, normalized review-status filtering, deterministic `StartTime DESC, SessionID DESC` ordering, and 50-row incremental pages with a bounded offset. Refresh replaces the collection and Load More appends only unseen session IDs. Detail summary and ordered answer rows use one connection and a repository-owned `RepeatableRead` transaction, producing one internally consistent read-only snapshot before the transaction and connection close.

Reviewed truth is only normalized GOOD or NG. Null, empty, whitespace, and unsupported truth remains pending and never counts as wrong. Correct requires matching supported user and truth values; valid truth with a null, unsupported, or mismatching user value is reviewed wrong. Accuracy is correct reviewed answers divided by reviewed answers and remains null for an empty reviewed denominator. The UI shows automatic or administrator review provenance without reviewer identity.

`TrainingHistoryRepository.GetProgressChartData` accepts only the employee identity supplied by the authorized service plus a fixed 7- or 30-day range. Separate completed-session and answer queries run inside one repository-owned `RepeatableRead` transaction, use local half-open `StartTime` boundaries, exclude incomplete sessions, and merge into zero-filled daily points without multiplying duration. The public service derives the employee identity exclusively from the active canonical trainee session.

`TrainingHistoryViewModel` and `TrainingHistoryDetailViewModel` perform database and file work away from the WPF dispatcher, serialize repeated loading, race blocking work against cancellation, observe abandoned tasks, and use operation versions to reject late publication after refresh or close. A failure limited to optional progress analytics does not clear a successfully loaded history page: the chart receives a fixed unavailable state and the technical failure is logged safely. Detail images load lazily for the current selection, reduce stored paths to the filename, enforce the configured image-folder boundary, release source files, and expose only fixed non-sensitive unavailable/error states. Window code-behind owns single-window navigation, ownership restoration, and ViewModel disposal only.

## Dashboard Analytics

`DashboardRepository` calculates the five dashboard cards for one local calendar day using caller-supplied `@DayStart` and `@DayEnd` parameters. The range is half-open: `StartTime >= @DayStart` and `StartTime < @DayEnd`. SQL does not apply `DATE()` or another function to `StartTime`, preserving index-friendly filtering.

Session metrics and answer metrics are calculated in separate aggregate subqueries and combined only after each produces one row. This prevents the session-to-answer relationship from multiplying session durations. Today's Training counts only sessions with an `EndTime`; Time Spent sums only completed rows whose end is not earlier than their start. Incomplete and negative durations contribute no time.

Answer distribution counts normalized supported trainee GOOD and NG selections for sessions started in the selected day, including valid pending review selections. An answer is reviewed only when `UPPER(TRIM(CorrectAnswer))` is supported GOOD or NG. Correct requires a supported normalized `UserAnswer` that matches the supported truth; a supported truth with a null, unsupported, or mismatching user answer is reviewed wrong. Null, empty, whitespace, and unsupported truth values remain pending and never count as wrong. A zero reviewed denominator maps to null so the ViewModel displays N/A.

`DashboardRepository.GetSnapshot` loads headline metrics, deterministic recent sessions, and separated session/answer daily chart aggregates through one connection and one `RepeatableRead` transaction. The chart range is exactly 7 or 30 local days and is zero-filled in ascending date order. `DashboardViewModel` performs this snapshot read on a worker task so normal WPF navigation remains responsive. One busy flag disables repeated command refresh, successful refresh replaces recent rows and chart series instead of appending them, disposal cancels publication promptly, and a failed refresh records the technical exception while exposing only a fixed non-sensitive status message.

## Reports

`ReportsViewModel` owns period selection, asynchronous loading, export coordination, and presentation state. `ReportPeriod` converts Daily, Monday-to-Sunday Weekly, rolling Last Seven Days, Monthly, inclusive Custom, and All Dates selections into local half-open boundaries. `ReportRepository` applies those boundaries with parameters and preserves deterministic `StartTime DESC, SessionID DESC` ordering.

The report query aggregates normalized answers by session before joining them to training sessions, so answer cardinality cannot multiply session totals. Reviewed truth is only normalized GOOD or NG. Unsupported, empty, whitespace, and null truth remains pending; valid truth with a missing, unsupported, or mismatching trainee answer is reviewed wrong. Summary, session-row, CSV, Excel, and PDF accuracy all use correct reviewed answers divided by reviewed answers and represent an empty denominator as N/A.

Interactive display and export loading are intentionally separate. The window shows at most 500 sessions and discloses when more matching sessions exist. Export requests load the complete deterministic snapshot and reject a selection over the 10,000-session safeguard instead of silently truncating it. For either path, `ReportRepository` owns one MySQL connection and one `RepeatableRead` transaction for summary, session-row, and bounded daily chart queries. The in-memory `ReportSnapshot` is constructed before commit, failures roll back, and the transaction and connection end before CSV, Excel, or PDF generation begins. Periods longer than 366 local days retain their summary/table/export behavior and expose a fixed chart-unavailable state rather than issuing an unbounded chart query.

`ReportExportService` is the single document-generation boundary. CSV uses UTF-8 with a byte-order mark and RFC-style field escaping. Excel uses Open XML to produce Report Information, Summary, and Sessions sheets with typed numeric/date cells, percentage formats, fixed column widths, and a frozen session header. PDFsharp produces a real A4 landscape PDF with summary metadata, repeated session headers, page numbers, and continuation pages.

Database queries and document generation run on worker tasks. One operation version and lifecycle cancellation prevent stale or post-close UI publication; abandoned tasks are observed, and commands remain disabled while work is active. Technical failures use `ApplicationErrorLogger`, while the window exposes only fixed non-sensitive database/export messages. Code-behind is limited to window lifecycle and save-dialog interaction.

## Repository Validation

Repository public methods validate parameters before opening MySQL connections wherever applicable. Numeric identities must be greater than zero, EmployeeNo values must be present, answer collections must be non-null and contain no null elements, answer values must be GOOD or NG, completed sessions must have valid start/end ordering, and report date ranges must be ordered.

Completed quiz persistence still runs through `SessionRepository.Save` and `AnswerRepository.SaveMany` in one transaction. Before inserting the session header, `SessionRepository` checks for an existing completion with the same EmployeeNo, StartTime second, EndTime second, TotalQuestions, and answer count. When that duplicate rule matches, the transaction rolls back and no new session or answers are saved.

The duplicate rule is also enforced by the database. `SessionRepository.EnsureTable` upgrades `tbl_training_session` with a nullable `DuplicateKey VARCHAR(64)` column and the unique index `UX_tbl_training_session_DuplicateKey`. The key is a SHA-256 hash of EmployeeNo, StartTime second, EndTime second, TotalQuestions, and answer count. Existing unique historical completion rows are backfilled before the index is created; historical duplicate groups are left with a null `DuplicateKey` so the unique index can be created without deleting data. A legacy duplicate lookup remains in the save transaction to reject new saves that match those historical null-key rows.

Reviewed truth is represented only by normalized supported GOOD or NG `CorrectAnswer` values. Null, empty, whitespace, and unsupported truth values remain pending. Dashboard correctness requires a supported normalized `UserAnswer` and `CorrectAnswer` that match; a supported truth with a null, unsupported, or mismatching user answer is reviewed wrong, while unsupported truth never counts as wrong.

Report session rows aggregate answer data directly instead of relying only on stored summary columns, which keeps pending answers out of wrong-answer totals even when old session rows have stale summary values.

Dashboard metrics, report summaries, and admin review session recalculation use conditional aggregation to reduce repeated scans while preserving existing total, pending, reviewed, correct, wrong, and accuracy meanings.
