# VisualInspectionTrainingSystem
Industrial AI Visual Inspection Training System built with C# WPF .NET Framework 4.8.

## Local Application Configuration

Application settings are loaded from an ignored workstation-local XML file. Database credentials and machine-specific folders must not be stored in `App.config` or committed to Git.

To configure a workstation:

1. Copy `DatabaseSettings.example.config` to `DatabaseSettings.local.config`.
2. Edit `DatabaseSettings.local.config` with the local MySQL values and folder paths.
3. Create the configured quiz image folder and place the training `.bmp` files there.
4. Build and run `VisualInpsectionTrainingSystem.slnx`.

`DatabaseSettings.local.config` is ignored by Git. Keep real passwords and workstation paths there only.

## Configuration Format

```xml
<?xml version="1.0" encoding="utf-8"?>
<applicationSettings>
  <mysql
    server="localhost"
    port="3306"
    database="visualinspectionquiz"
    username="YOUR_DATABASE_USER"
    password="YOUR_DATABASE_PASSWORD"
    sslMode="Disabled"
    connectionTimeoutSeconds="5"
    retryCount="2"
    retryDelayMilliseconds="500" />

  <paths
    quizImageFolder=".\QuizImages"
    logFolder=".\Logs"
    exportFolder=".\Exports"
    reportFolder=".\Reports" />
</applicationSettings>
```

## Directory Behavior

- `quizImageFolder` is required and must already exist.
- `logFolder`, `exportFolder`, and `reportFolder` are created automatically when they are missing and the application has permission.
- Invalid or missing values stop startup with a clear non-sensitive configuration error.
- Report CSV export opens in the configured `exportFolder`.
- `connectionTimeoutSeconds` bounds each MySQL connection attempt.
- `retryCount` controls how many transient connection retries happen after the first attempt.
- `retryDelayMilliseconds` controls the short delay between retries.
- Authentication failures and invalid configuration values are not retried.
- Startup database checks are bounded so the splash screen does not wait indefinitely for MySQL.

If a real database password was previously committed to Git history, change that MySQL password before continuing normal use. Removing it from current files does not remove it from older commits.
