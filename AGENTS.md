# AGENTS.md

# Visual Inspection Training System

## Technology Stack

- Language: C#
- Framework: WPF (.NET Framework 4.8.1)
- Pattern: MVVM
- Database: MySQL
- IDE: Visual Studio 2026
- AI Developer: ChatGPT Codex

---

# Development Rules

## General

- Always modify the existing project.
- Never create duplicate classes.
- Preserve the project architecture.
- Keep compatibility with C# 7.3+ and .NET Framework 4.8.1.
- Rewrite the ENTIRE file when modification is required.
- Use #region blocks.
- Add XML documentation for public members.
- Use async/await where appropriate.
- Avoid breaking public APIs.

---



# Build and Validation

After every implementation task:

1. Save all modified files.
2. Attempt to build the solution using MSBuild when MSBuild is available.
3. Use the correct solution file:
   `VisualInpsectionTrainingSystem.slnx`
4. Use the Debug configuration unless another configuration is requested.
5. Do not treat unavailable MSBuild as a source-code error.

## Successful Build

When the build completes with:

- 0 compile errors
- 0 XAML errors
- No blocking warnings

Then:

1. Attempt to run the application.
2. Perform a focused test of the updated feature.
3. Confirm that the application starts without crashing.
4. Confirm that the modified workflow behaves correctly.
5. Stop the application after testing.
6. Present a complete update report.

## Failed Build

When the build fails:

1. Read the complete compiler output.
2. Fix errors caused by the current task.
3. Rebuild the solution.
4. Repeat until the build succeeds or the issue requires user input.
5. Do not hide, ignore, or suppress legitimate compiler errors.
6. Do not test-run the application while compile errors remain.

## Test-Run Limitations

If the application cannot be tested automatically because of:

- Missing database access
- Missing network paths
- Missing image folders
- Required credentials
- Interactive login
- Desktop environment limitations

Report the limitation clearly and provide the exact manual test steps for the user.

## Completion Report

After successful implementation, build, and testing, report:

- Task completed
- Files created
- Files modified
- Important implementation changes
- Build command used
- Build result
- Warning count
- Test performed
- Test result
- Known limitations
- Remaining issues
- Recommended Git commit message
- Next recommended task

Do not claim that testing succeeded unless the application or feature was actually tested.