# Version 1.0 First-Run Guide

## Before starting

Obtain the portable ZIP, its `.sha256` file, authorized MySQL settings, and an authorized BMP image set from your administrator.

1. Verify the ZIP SHA-256 value against the separately supplied checksum.
2. Extract the ZIP to a writable location such as `D:\Apps\VisualInspectionTrainingSystem`.
3. Do not run from inside the ZIP and do not install beneath `Program Files`.

## Configure the workstation

1. Copy `DatabaseSettings.example.config` to `DatabaseSettings.local.config` beside the executable.
2. Replace only the documented database placeholders with the assigned values.
3. Keep the default relative paths unless an administrator has approved alternatives.
4. Copy authorized `.bmp` images into `QuizImages`. Provide at least 20 usable images when both quiz sizes are required.
5. Keep `DatabaseSettings.local.config` private. It is not needed by another workstation unless separately secured and updated.

## Start and verify

1. Run `VisualInpsectionTrainingSystem.exe`.
2. Confirm the splash screen completes without a configuration or connection error.
3. Sign in with an authorized account.
4. Confirm the destinations permitted for that role open correctly.
5. For a trainee workstation, open Training Setup and verify the intended quiz sizes are available.
6. For an administrator workstation, open Dashboard and Reports and confirm configured database data loads.

## Portable folders

- Keep `QuizImages`, `Logs`, `Exports`, and `Reports` writable.
- Do not remove the x86 or x64 folders; they contain required chart-rendering native libraries.
- Generated logs and exports are local operational artifacts and are not part of the original release checksum set.

If startup fails, do not paste credentials, connection strings, database details, or full logs into a public issue. Provide the displayed non-sensitive message and the generated error ID to authorized support.
