# VisualInspectionTrainingSystem
Industrial AI Visual Inspection Training System built with C# WPF .NET Framework 4.8

## Local Database Configuration

Database credentials are not stored in `App.config`.

To configure a workstation:

1. Copy `DatabaseSettings.example.config` to `DatabaseSettings.local.config`.
2. Edit `DatabaseSettings.local.config` with the local MySQL connection values.
3. Keep `DatabaseSettings.local.config` untracked. It is ignored by Git.
4. Build and run `VisualInpsectionTrainingSystem.slnx`.

Configuration format:

```xml
<?xml version="1.0" encoding="utf-8"?>
<databaseSettings>
  <mysql
    server="localhost"
    port="3306"
    database="visualinspectionquiz"
    username="YOUR_DATABASE_USER"
    password="YOUR_DATABASE_PASSWORD"
    sslMode="Disabled" />
</databaseSettings>
```

If the local file is missing or invalid, the application reports a configuration error without displaying the database password.

If a real database password was previously committed to Git history, change that MySQL password before continuing normal use. Removing it from current files does not remove it from older commits.
