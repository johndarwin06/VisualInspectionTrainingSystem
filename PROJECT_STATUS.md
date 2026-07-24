# PROJECT STATUS

Project: Visual Inspection Training System

Current Version: 0.9 Beta

Current Module: Issue #12 Reports - implemented and verified on `issue-12-reports`; awaiting pull-request review and merge

Build Status: Debug and Release successful

Last Build: 2026-07-24

Build Warnings: 1 existing `MVVMTKCFG0002` warning in each configuration

Completed:
- Quiz Engine
- Quiz ViewModel
- Quiz Window
- Result ViewModel
- Result Window
- Result Module manual acceptance
- Session Repository
- Answer Repository
- MySQL Integration
- Admin Module
- Dashboard
- Reports
- User Password Hashing and Migration
- Secure Configuration
- Configuration System
- Database Transactions
- Connection Resiliency
- Repository Validation
- Repository Validation Hardening
- Splash Screen Improvement
- Splash Timeout Hardening
- Global Error Handling
- Quiz Optimization
- Dashboard Analytics
- Configurable Quiz Sample Size (Issue #46, delivered by merged PR #47)

In Progress:
- Issue #12 Reports is implemented and verified on `issue-12-reports` and is awaiting pull-request review and merge.

Issue #12 Verification:
- Added explicit Daily, Monday-to-Sunday This Week, rolling Last 7 Days, This Month, inclusive Custom, and All Dates periods using parameterized half-open database boundaries.
- Aligned report summaries, session rows, and every export with Dashboard Analytics: only normalized GOOD or NG truth is reviewed, malformed truth remains pending, and zero reviewed answers display N/A.
- Preserved the 500-row interactive display with visible limit disclosure. CSV, Excel, and PDF load a separate complete snapshot with a documented 10,000-session safety limit and deterministic `StartTime DESC, SessionID DESC` ordering.
- Added background report loading and document generation with busy guards, stale-result rejection, observed abandoned tasks, safe close-during-refresh behavior, fixed non-sensitive errors, and existing technical logging.
- Added complete UTF-8 CSV export, a validated three-sheet Open XML workbook, and a real A4 landscape multipage PDF with repeated headers and page numbers.
- The Issue #12 probe passed 240 assertions, including controlled MySQL periods and normalization, independent SQL comparison, Dashboard parity, document validation, asynchronous lifecycle behavior, Result Module regression, 10/20-question quiz regression, and Administration regression.
- Visible WPF acceptance passed every required Reports period and state, all three save-dialog cancellation paths, CSV/XLSX/PDF export and opening, four-page PDF layout, close during a genuinely blocked refresh, Administration and Dashboard navigation, normal Reports close, and normal shutdown.
- Temporary report rows were removed with zero residual sessions. Generated exports, probes, rendered pages, and build output are excluded from the change set.
- GitHub issue #16 remains open until the draft pull request is reviewed and merged.

Issue #46 Verification:
- GitHub issue #46 tracks the configurable 10- or 20-question trainee quiz feature; 10 is the default.
- `ImageService.LoadImages(string, bool)` retains its complete-catalog behavior. The separate quiz sampler removes case-insensitive duplicate paths, applies one Fisher-Yates shuffle, and returns at most the requested 10 or 20 metadata rows.
- A valid request with fewer unique images uses every available image once and drives progress, completion, results, and persistence from the actual count. Zero images retain the existing safe no-image flow.
- Administrator inventory remains unrestricted and continues to use the complete catalog. The two-entry current/upcoming bitmap cache remains bounded.
- The configurable quiz probe passed 1,140 assertions: ImageService 828, Home selection 15, progress/completion 212, cache/cancellation 25, persistence 42, administrator inventory/preview 4, and login/Result/Dashboard regressions 14.
- Navigation correction commit `b3f84152219ac60ffe1343f1fda4c98671d82f1f` made `HomeViewModel` raise the training-navigation event and `HomeWindow` the sole `QuizWindow` owner. It permits only one active quiz, hides Home during training, and restores Home after completion or cancellation.
- The navigation-correction probe passed 280 assertions. Visible WPF verification passed a rapid default-10 double-click with one quiz, one 20-question quiz, correct selected-size propagation, Home visibility and restoration, early cancellation, exact 20-question completion with one ResultWindow, and normal shutdown.
- Visible WPF testing passed trainee and administrator login, Home selection, real 10- and 20-question quizzes, unique displayed images within each quiz, exact completion and ResultWindow totals, early cancellation, administrator review/preview mapping, Dashboard and Reports navigation, and normal shutdown.
- MySQL verification passed 9 assertions: the visible sessions persisted totals, answer counts, and distinct image counts of 10 and 20; the cancelled quiz did not persist. The two sessions and all 30 answer rows were then removed and verified absent.
- Controlled visible fewer-image folder tests were not run because they require temporary local folder configuration; the 7-of-10, 14-of-20, empty-folder, and missing-folder cases passed deterministic automation.
- Merged PR: #47
- Merge commit: `a13fbbea4d6d0ff27201a9378bca5109c259298c`
- Issue #46: closed as completed
- Feature branch: deleted after merge

Issue #11 Verification:
- Merged PR #43 delivered Dashboard Analytics.
- Today's Training counts completed sessions in the local half-open day range, and Time Spent sums only valid completed-session durations.
- Reviewed truth requires a normalized supported GOOD or NG value. Null, empty, whitespace, and unsupported truth values remain pending and never count as wrong.
- Average Accuracy uses reviewed answers only and displays N/A when no reviewed rows exist; normalized trainee GOOD and NG counts include valid pending selections.
- The complete Dashboard Analytics recovery probe passed 111 assertions, including malformed truth values, the controlled six-answer dataset, empty days, boundaries, invalid durations, refresh behavior, failure handling, ordering, and limits.
- Result Module and Issue #9 regression probes passed 76 and 29 assertions respectively.
- Visible administrator navigation opened exactly one Dashboard. Its five values matched an independent SQL query (1 training session, 50.00% reviewed accuracy, 10 minutes, GOOD 3, NG 3), Refresh did not duplicate rows, Dashboard closed safely, and normal application shutdown succeeded.

Next Task:
- No subsequent project issue has started. Issue #12 awaits pull-request review and merge.
