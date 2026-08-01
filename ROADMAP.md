# Sprint 1

- [x] QuizEngine
- [x] QuizViewModel
- [x] QuizWindow
- [x] ResultViewModel
- [x] ResultWindow

# Sprint 2

- [x] SessionRepository
- [x] AnswerRepository
- [x] MySQL Integration

# Sprint 3

- [x] Admin Module

# Sprint 4

- [x] Dashboard
- [x] Reports

# Sprint 5

- [x] User Password Hashing and Migration
- [x] Secure Configuration
- [x] Configuration System
- [x] Database Transactions
- [x] Connection Resiliency
- [x] Repository Validation
- [x] Repository Validation Hardening
- [x] Splash Screen Improvement
- [x] Splash Timeout Hardening
- [x] Global Error Handling

# Sprint 6

- [x] Quiz Optimization (Issue #9, merged as PR #40)

# Sprint 7

- [x] Result Module (Issue #10, delivered by merged PR #41 and manually accepted after diagnostic-dialog correction `bee4eb0`)

# Sprint 8

- [x] Dashboard Analytics (Issue #11, delivered by merged PR #43; implemented, tested, and complete)

# Sprint 9

- [x] Configurable Quiz Sample Size (Issue #46, delivered by merged PR #47; implemented, tested, and complete)

# Sprint 10

- [x] Reports enhancements (Issue #12 / GitHub issue #16, delivered by merged PR #49)

# Sprint 11

- [x] Review Workflow (Issue #13 / GitHub issue #17, delivered by merged PR #50)

# Sprint 12

- [x] User Management (Issue #14 / GitHub issue #18, delivered by merged PR #51; implemented, tested, and manually accepted)

# Sprint 13

- [x] UI Polish and secure trainee My Training History (Issue #15 / GitHub issue #19; implemented, tested, and manually accepted)

# Sprint 14

- [x] Material Design UI and analytics overhaul (Issue #15.1 / GitHub issue #53; implemented, tested, and manually accepted)

# Sprint 15

- [x] Fluent UI migration (Issue #15.2 / GitHub issue #55; follow-up to Issue #53 and PR #54; implemented, tested, and manually accepted)

# Sprint 16

- [x] .NET Framework 4.6.2 compatibility migration (Issue #15.3 / GitHub issue #57; follow-up to Issue #55 and PR #56; implemented and locally accepted)
- [ ] Qualify deployment on an isolated machine or VM containing only the .NET Framework 4.6.2 runtime; currently Not Run
- [ ] Plan migration to a supported runtime before .NET Framework 4.6.2 support ends on January 12, 2027

# Sprint 17

- [x] Permanent regression-testing system (Issue #17 / GitHub issue #21; implemented, tested, and manually accepted)
- [x] Production-composition WPF coverage for administrator and trainee workspaces, authorization, ownership, resources, and safe closing
- [x] Debug/Release AnyCPU and supported x86/x64 native-deployment regression gates
- [ ] Run the database category only after planned database-testing Issue #23 provisions an isolated test-only schema; the eight current Debug/Release skips must never be redirected to production data

# Sprint 18

- [x] Centralized safe logging for .NET Framework 4.6.2 (Issue #16 / GitHub issue #20; implemented, tested, and manually accepted)
- [x] Bounded asynchronous UTF-8 file logging with configured-path preference, LocalAppData fallback, 5 MiB rollover, five-backup retention, concurrency serialization, bounded shutdown flush, and fail-safe provider behavior
- [x] Sanitized lifecycle, global-handler, authentication, security-operation, persistence, export, cancellation, and feature-failure coverage with permanent regression tests
- [ ] Provision the isolated schema under planned database-testing Issue #23 before enabling the eight currently skipped Debug/Release database tests

No subsequent implementation issue has started. Issue #23 remains planned only.
