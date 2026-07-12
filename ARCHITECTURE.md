# ARCHITECTURE

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
