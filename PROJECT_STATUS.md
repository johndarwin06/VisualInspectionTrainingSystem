# PROJECT STATUS

Project: Visual Inspection Training System

Current Version: 0.9 Beta

Current Module: Issue #46 Configurable Quiz Sample Size - merged, implemented, tested, and complete

Build Status: Debug and Release successful

Last Build: 2026-07-23

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
- None. Issue #46 is merged, implemented, tested, and complete.

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
- Issue #12 - Reports is the next planned development issue. Implementation has not started.
