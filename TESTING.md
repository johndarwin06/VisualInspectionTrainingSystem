# Regression Testing

This repository has one permanent regression project for the .NET Framework 4.6.2 baseline. It uses NUnit 4.4.0 and NUnit3TestAdapter 5.2.0, targets C# 7.3, and keeps test dependencies out of the production project.

## Required tools

- Windows with Visual Studio Test Platform and MSBuild.
- Visual Studio 2022 or later with .NET desktop development tools.
- The official .NET Framework 4.6.2 Developer Pack and targeting reference assemblies.
- PowerShell 5.1 or later.
- NuGet access for the first restore.
- MySQL only for the opt-in `Database` and protected database-performance categories.

The development machine may run a newer in-place .NET Framework CLR. A successful local run proves that the assemblies were compiled against genuine 4.6.2 reference assemblies, but it does not prove execution on a machine containing only the 4.6.2 runtime.

## Restore and build

Run from the repository root in a Developer PowerShell prompt:

```powershell
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'

& $msbuild VisualInspectionTrainingSystem.csproj /t:Restore /p:RestorePackagesConfig=true /p:SolutionDir="$PWD" /p:RestorePackagesPath="$PWD\packages"
& $msbuild VisualInspectionTrainingSystem.Tests\VisualInspectionTrainingSystem.Tests.csproj /t:Restore /p:RestorePackagesPath="$PWD\packages"
& $msbuild VisualInpsectionTrainingSystem.slnx /t:Rebuild /p:Configuration=Debug /p:Platform="Any CPU"
& $msbuild VisualInpsectionTrainingSystem.slnx /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU"
```

Adjust the Visual Studio edition in the path when necessary. `Run-RegressionTests.ps1` locates Visual Studio automatically and is the preferred full command:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration All
```

Every test-host invocation has a five-minute outer timeout. Asynchronous tests also use controlled task completion or bounded waits instead of arbitrary sleeps.

## Test categories

| Category | Purpose | External state |
| --- | --- | --- |
| `Unit` | Authentication, validation, authorization, quiz/result calculations, analytics models, periods, filters, and presentation state | None |
| `Integration` | Repository SQL contracts, image behavior, repeated refresh, cancellation, safe closing, non-sensitive failure behavior, and centralized logging/redaction/rollover/concurrency contracts | Temporary local files only |
| `WPF` | STA resource construction, Fluent/Violeta resources, theme switching, XAML contracts, charts, resizing, keyboard metadata, and DataGrid virtualization | Interactive desktop libraries; no MySQL |
| `Database` | Fail-closed schema validation, required schema contracts, parameter behavior, and rollback cleanup | Explicit test-only MySQL schema |
| `Export` | CSV, XLSX, PDF validation, cancellation cleanup, and export limits | Unique temporary directory removed in teardown |
| `NativeDeployment` | Debug/Release managed files, AnyCPU contract, x86/x64 native files, and process-architecture native loads | Built output only |
| `Performance` | Warm-up/repeated timing evidence plus functional, lifecycle, cleanup, and resource-safety assertions | Temporary local files; dedicated test schema only for database mode |
| `ManualRuntime` | Genuine 4.6.2-only host qualification | External machine or VM; explicit and never inferred locally |

The test project is organized under matching folders in `VisualInspectionTrainingSystem.Tests`. Tests do not depend on execution order. Permanent source-contract tests guard parameterized SQL, normalized GOOD/NG review semantics, deterministic ordering, half-open date boundaries, transactions, concurrency checks, and row safeguards without connecting to production data.

## Running selected tests

The full runner groups output by category and runs both Debug and Release. It runs the normal suites in an x86 test host and repeats native deployment qualification in an x64 host.

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration Debug
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration Release
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration All -SkipRestore -SkipBuild
```

For direct Visual Studio Test Platform use, locate `vstest.console.exe` under the active Visual Studio installation and pass the adapter folder in the global NuGet cache:

```powershell
vstest.console.exe VisualInspectionTrainingSystem.Tests\bin\Debug\VisualInspectionTrainingSystem.Tests.dll `
  /TestAdapterPath:"$PWD\packages\nunit3testadapter\5.2.0\build\net462" `
  /Platform:x86 `
  /TestCaseFilter:"TestCategory=Unit"
```

Use one of the category names from the table. `ManualRuntime` is intentionally marked explicit.

## Performance testing

Performance workloads are excluded from normal functional and CI runs. Use Release x64 for authoritative local evidence; Debug and x86 are diagnostic configurations. Results report minimum, median, p95, maximum, and sample count after warm-up, but timing values are informational rather than universal pass/fail thresholds. Functional output, authorization, bounded resources, cancellation, cleanup, and database safety remain hard assertions.

```powershell
# Secret-free configuration, WPF, image, quiz, export, logging, and lifecycle workloads
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration Release -PerformanceMode Baseline -PerformancePlatform x64 -SkipRestore -SkipBuild

# Protected repository workloads; requires the validated dedicated test schema
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration Release -PerformanceMode Database -PerformancePlatform x64 -SkipRestore -SkipBuild

# Both groups in one process after the same fail-closed database preconditions
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration Release -PerformanceMode All -PerformancePlatform x64 -SkipRestore -SkipBuild
```

Baseline coverage includes configuration, real application resources, administrator/trainee workspace construction, 10/20-question interaction, 100/1,000/5,000-image inventories and requested-image hashing, 100/1,000/5,000-row CSV/XLSX/PDF exports, concurrent logging, cache bounds, cancellation, and repeated close/disposal checks. Database mode covers authentication, 501 users, 500 sessions, 10,000 answers, Dashboard, Reports, review, and 120-session History through production repositories. It captures production fingerprints read-only, inspects query plans, uses unique test-run ownership, and cleans in `finally`.

Machine-specific performance output, logs, exports, images, and result files are generated artifacts and must not be committed. Record only a safely generalized machine profile and reviewed aggregate evidence in project documentation.

## Safe MySQL test configuration

Database tests fail closed unless both variables are present:

- `VITS_TEST_MYSQL_CONNECTION_STRING`
- `VITS_TEST_MYSQL_SCHEMA`

Never commit either value. The runner reads Process scope first and Windows User scope second without printing values. The declared schema must exactly match the live database and identity marker, contain `test` in its name, use the dedicated restricted account, and remain distinct from the production endpoint/schema/account identity. The boundary inspects grants, disables pooling and persisted security information, caps connection establishment at five seconds, never creates or drops a database, and stops before tests when any safety check fails.

Use `DatabaseTesting/Provision-TestDatabase.sql.example` to prepare the retained dedicated schema locally; keep the populated `DatabaseTesting/Provision-TestDatabase.sql` ignored. The tests use unique `I19T` run identifiers, parameterized payloads, repository-owned transactions, rollback checks, and `finally` cleanup. Explicit cleanup gates verify zero synthetic rows. They must never point to an operational schema or receive global privileges.

```powershell
$env:VITS_TEST_MYSQL_CONNECTION_STRING = '<untracked test-only connection string>'
$env:VITS_TEST_MYSQL_SCHEMA = '<matching test-only schema name>'
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration Debug -SkipRestore -SkipBuild
Remove-Item Env:VITS_TEST_MYSQL_CONNECTION_STRING
Remove-Item Env:VITS_TEST_MYSQL_SCHEMA
```

Use the runner modes to prove the boundary before functional execution and to reject database skips:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration All -DatabaseMode Preflight -SkipRestore -SkipBuild
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration All -DatabaseMode Required -SkipRestore -SkipBuild
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Run-RegressionTests.ps1 -Configuration All -DatabaseMode Cleanup -SkipRestore -SkipBuild
```

## Continuous integration

`.github/workflows/regression-tests.yml` uses pinned `actions/checkout` and `microsoft/setup-msbuild` commits on `windows-2022`. It restores and rebuilds Debug and Release, then runs `Unit`, `Integration`, `Export`, and `NativeDeployment` without credentials.

Database tests are excluded from CI because no test database secret is supplied. WPF construction and real visible acceptance remain local gates because a hosted runner is not evidence of an interactive desktop session. The workflow never reads local image configuration, uploads private images, or publishes database/build artifacts.

Issue #19 / GitHub issue #23 expands the category to 24 database tests per configuration. The accepted dedicated environment runs all 48 with zero skips, while `Required` mode rejects any skipped result. CI still excludes the category because no database secret is supplied; this omission must never be bypassed by redirecting CI or local tests to production. Performance modes are also rejected under `-ContinuousIntegration` so shared runners cannot become hardware-dependent timing gates.

Issue #16 / GitHub issue #20 adds 17 permanent logging tests. They use isolated temporary directories and cover all five levels, UTF-8 output, bounded diagnostic fields, secrets redaction, duplicate exception suppression, 120 concurrent calls, rollover and retention, configured/fallback paths, read-only and locked destinations, simultaneous sink failure, bounded shutdown, task-observation behavior, and production integration contracts. The logger is disabled when each isolated test scope ends, and tests never read the local production configuration.

## Visible WPF acceptance

After automated tests pass:

1. Start `bin\Debug\VisualInpsectionTrainingSystem.exe` normally.
2. Confirm Splash, Login, and Registration render without binding or resource errors.
3. Sign in as an administrator and visit Review Workflow, User Management, Dashboard, and Reports; refresh data-heavy pages and close one during refresh.
4. Sign out, sign in as a trainee, and verify Training Setup, 10-question and 20-question Quiz, one Result per quiz, Result filters/image preview, Training History, and Session Detail.
5. Check light/dark themes, navigation ownership, resizing, DataGrid sorting/selection/scrolling, keyboard focus, dialogs, loading/empty states, logout, and normal shutdown.
6. Record separately whether there was any crash, freeze, raw database error, sensitive message, unexpected diagnostic dialog, or binding failure.

This is a manual test. Automated WPF construction is not reported as visible acceptance.

For centralized logging acceptance, inspect the newly generated normal-session log read-only after shutdown. Verify structured startup, configuration, authenticated-session, feature activity, logout, and final shutdown records; meaningful severity/component values; unique event IDs; complete entry boundaries; and absence of credentials, hashes, tokens, connection strings, SQL values, sensitive configuration, unnecessary personal data, provider errors, or unintended duplicate exception entries. Delete only the generated acceptance log during final artifact cleanup.

## Genuine .NET Framework 4.6.2-only qualification

Use a clean Windows machine or VM that contains the supported 4.6.2 runtime and does not rely on a newer in-place CLR:

1. Copy the complete Release deployment directory without any local configuration, credentials, logs, or test output.
2. Add an authorized target-machine database configuration outside source control.
3. Verify startup, authentication, inactive-account rejection, MySQL access, administrator navigation, Dashboard and Reports charts, CSV/XLSX/PDF exports, trainee navigation, 10- and 20-question quizzes, Result, Training History, logout, and shutdown.
4. Verify x86 and x64 native assets load on the supported deployment architecture.
5. Record missing/file-load/type-load/XAML/native exceptions explicitly.

Until that external run occurs, report `ManualRuntime` as **Not Run**. Local execution on a newer CLR must not be relabeled as genuine 4.6.2-only qualification.

## Cleanup

Tests remove their own temporary files in teardown or `finally` blocks. After an interrupted run:

1. Verify the dedicated test schema contains no reserved synthetic test or performance rows before removing anything.
2. Delete only known test-created rows; never truncate or delete operational data.
3. Remove temporary exports, rendered pages, logs, probes, `TestResults`, coverage output, `bin`, and `obj`.
4. Clear temporary Process-scope overrides. Preserve the intentionally retained User-scope test variables unless the dedicated environment is being deprovisioned.
5. Run `git status --short` and `git diff --check`.

The repository ignores build output, packages, local configuration, credentials, logs, test results, and coverage output. Permanent test source, this guide, and the CI workflow remain tracked.

## Known limitations

- The database category is Not Run when a separately provisioned safe schema is unavailable.
- Repository source-contract tests protect important SQL invariants but do not replace controlled MySQL integration tests.
- Automated WPF resource construction does not replace visible interaction testing.
- A newer in-place CLR cannot qualify the application on a genuine 4.6.2-only host.
- CI intentionally excludes secret-backed database tests and interactive WPF acceptance.
