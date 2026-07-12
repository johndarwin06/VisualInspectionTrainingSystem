#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml.Linq;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Loads and validates workstation-local application configuration.
    /// </summary>
    public static class ConfigurationService
    {
        #region Constants

        private const string SettingsFileKey = "ApplicationSettingsFile";
        private const string LegacySettingsFileKey = "DatabaseSettingsFile";
        private const string DefaultSettingsFile = "DatabaseSettings.local.config";
        private const string ExampleSettingsFile = "DatabaseSettings.example.config";

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads and validates all application settings.
        /// </summary>
        /// <returns>The validated application settings.</returns>
        public static ApplicationSettings GetApplicationSettings()
        {
            string settingsPath = FindSettingsFile();

            return ReadApplicationSettings(settingsPath);
        }

        /// <summary>
        /// Loads and validates configured application paths.
        /// </summary>
        /// <returns>The validated path settings.</returns>
        public static PathSettings GetPathSettings()
        {
            return GetApplicationSettings().Paths;
        }

        /// <summary>
        /// Builds the MySQL connection string from local configuration.
        /// </summary>
        /// <returns>A MySQL connection string.</returns>
        public static string GetMySqlConnectionString()
        {
            return BuildConnectionString(GetApplicationSettings().Database);
        }

        /// <summary>
        /// Validates the configured application settings.
        /// </summary>
        public static void ValidateApplicationSettings()
        {
            GetApplicationSettings();
        }

        #endregion

        #region File Discovery

        private static string FindSettingsFile()
        {
            string configuredFile =
                ConfigurationManager.AppSettings[SettingsFileKey];

            if (string.IsNullOrWhiteSpace(configuredFile))
            {
                configuredFile =
                    ConfigurationManager.AppSettings[LegacySettingsFileKey];
            }

            if (string.IsNullOrWhiteSpace(configuredFile))
            {
                configuredFile = DefaultSettingsFile;
            }

            foreach (string path in GetCandidateSettingsPaths(configuredFile))
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new ConfigurationErrorsException(
                "Application configuration is missing. Create " +
                DefaultSettingsFile +
                " from " +
                ExampleSettingsFile +
                " and keep it out of Git.");
        }

        private static IEnumerable<string> GetCandidateSettingsPaths(
            string configuredFile)
        {
            List<string> paths = new List<string>();

            if (Path.IsPathRooted(configuredFile))
            {
                AddCandidatePath(paths, configuredFile);

                return paths;
            }

            AddCandidatePath(
                paths,
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    configuredFile));

            AddCandidatePath(
                paths,
                Path.Combine(
                    Environment.CurrentDirectory,
                    configuredFile));

            AddParentDirectoryCandidates(
                paths,
                AppDomain.CurrentDomain.BaseDirectory,
                configuredFile);

            AddParentDirectoryCandidates(
                paths,
                Environment.CurrentDirectory,
                configuredFile);

            return paths;
        }

        private static void AddParentDirectoryCandidates(
            IList<string> paths,
            string startDirectory,
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(startDirectory))
            {
                return;
            }

            DirectoryInfo directory;

            try
            {
                directory = new DirectoryInfo(startDirectory);
            }
            catch
            {
                return;
            }

            for (int depth = 0;
                 directory != null && depth < 8;
                 depth++)
            {
                AddCandidatePath(
                    paths,
                    Path.Combine(
                        directory.FullName,
                        fileName));

                directory = directory.Parent;
            }
        }

        private static void AddCandidatePath(
            IList<string> paths,
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string fullPath;

            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            bool exists = paths.Any(
                item => string.Equals(
                    item,
                    fullPath,
                    StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                paths.Add(fullPath);
            }
        }

        #endregion

        #region Parsing

        private static ApplicationSettings ReadApplicationSettings(string path)
        {
            try
            {
                XDocument document = XDocument.Load(path);

                XElement root = document.Root;

                if (root == null ||
                    !IsSupportedRoot(root))
                {
                    throw CreateInvalidConfigurationException(
                        "Root element must be <applicationSettings>.");
                }

                XElement mysql = GetRequiredElement(root, "mysql");

                XElement paths = GetRequiredElement(root, "paths");

                ApplicationSettings settings =
                    new ApplicationSettings
                    {
                        Database = ReadDatabaseSettings(mysql),
                        Paths = ReadPathSettings(paths)
                    };

                ValidateSettings(settings);

                return settings;
            }
            catch (ConfigurationErrorsException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException(
                    "Application configuration is invalid. Check " +
                    DefaultSettingsFile +
                    " against " +
                    ExampleSettingsFile +
                    ". " +
                    ex.Message,
                    ex);
            }
        }

        private static bool IsSupportedRoot(XElement root)
        {
            return string.Equals(
                       root.Name.LocalName,
                       "applicationSettings",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       root.Name.LocalName,
                       "databaseSettings",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static DatabaseSettings ReadDatabaseSettings(XElement mysql)
        {
            DatabaseSettings settings =
                new DatabaseSettings
                {
                    Server = GetRequiredAttribute(mysql, "server"),
                    Port = GetRequiredPort(mysql),
                    Database = GetRequiredAttribute(mysql, "database"),
                    Username = GetRequiredAttribute(mysql, "username"),
                    Password = GetRequiredAttribute(
                        mysql,
                        "password",
                        true,
                        false),
                    SslMode = GetRequiredAttribute(mysql, "sslMode")
                };

            ValidateDatabaseSettings(settings);

            return settings;
        }

        private static PathSettings ReadPathSettings(XElement paths)
        {
            PathSettings settings =
                new PathSettings
                {
                    QuizImageFolder = NormalizeConfiguredPath(
                        GetRequiredAttribute(paths, "quizImageFolder"),
                        "quiz image folder"),
                    LogFolder = NormalizeConfiguredPath(
                        GetRequiredAttribute(paths, "logFolder"),
                        "log folder"),
                    ExportFolder = NormalizeConfiguredPath(
                        GetRequiredAttribute(paths, "exportFolder"),
                        "export folder"),
                    ReportFolder = NormalizeConfiguredPath(
                        GetRequiredAttribute(paths, "reportFolder"),
                        "report folder")
                };

            ValidatePathSettings(settings);

            return settings;
        }

        private static XElement GetRequiredElement(
            XElement root,
            string elementName)
        {
            XElement element = root.Elements()
                .FirstOrDefault(item => string.Equals(
                    item.Name.LocalName,
                    elementName,
                    StringComparison.OrdinalIgnoreCase));

            if (element == null)
            {
                throw CreateInvalidConfigurationException(
                    "Missing <" + elementName + "> settings element.");
            }

            return element;
        }

        private static string GetRequiredAttribute(
            XElement element,
            string attributeName)
        {
            return GetRequiredAttribute(
                element,
                attributeName,
                false,
                true);
        }

        private static string GetRequiredAttribute(
            XElement element,
            string attributeName,
            bool allowEmpty,
            bool trimValue)
        {
            XAttribute attribute = element.Attribute(attributeName);

            if (attribute == null)
            {
                throw CreateInvalidConfigurationException(
                    "Missing required '" + attributeName + "' value.");
            }

            string value = trimValue
                ? attribute.Value.Trim()
                : attribute.Value;

            if (!allowEmpty &&
                string.IsNullOrWhiteSpace(value))
            {
                throw CreateInvalidConfigurationException(
                    "Missing required '" + attributeName + "' value.");
            }

            return value;
        }

        private static uint GetRequiredPort(XElement element)
        {
            string value = GetRequiredAttribute(element, "port");
            uint port;

            if (!uint.TryParse(value, out port) ||
                port == 0 ||
                port > 65535)
            {
                throw CreateInvalidConfigurationException(
                    "Port must be a number from 1 to 65535.");
            }

            return port;
        }

        private static ConfigurationErrorsException CreateInvalidConfigurationException(
            string detail)
        {
            return new ConfigurationErrorsException(
                "Application configuration is invalid. " +
                detail +
                " Use " +
                ExampleSettingsFile +
                " as the template.");
        }

        #endregion

        #region Validation

        private static void ValidateSettings(ApplicationSettings settings)
        {
            if (settings == null)
            {
                throw CreateInvalidConfigurationException(
                    "Settings object could not be created.");
            }

            if (settings.Database == null)
            {
                throw CreateInvalidConfigurationException(
                    "Database settings are required.");
            }

            if (settings.Paths == null)
            {
                throw CreateInvalidConfigurationException(
                    "Path settings are required.");
            }
        }

        private static void ValidateDatabaseSettings(DatabaseSettings settings)
        {
            try
            {
                BuildConnectionString(settings);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException(
                    "Application configuration contains an invalid MySQL setting. " +
                    ex.Message,
                    ex);
            }
        }

        private static void ValidatePathSettings(PathSettings settings)
        {
            EnsureRequiredDirectoryExists(
                settings.QuizImageFolder,
                "quiz image folder");

            EnsureOutputDirectoryExists(
                settings.LogFolder,
                "log folder");

            EnsureOutputDirectoryExists(
                settings.ExportFolder,
                "export folder");

            EnsureOutputDirectoryExists(
                settings.ReportFolder,
                "report folder");
        }

        private static void EnsureRequiredDirectoryExists(
            string path,
            string settingName)
        {
            if (!Directory.Exists(path))
            {
                throw CreateInvalidConfigurationException(
                    "Configured " +
                    settingName +
                    " does not exist. Create the directory or update " +
                    DefaultSettingsFile +
                    ".");
            }
        }

        private static void EnsureOutputDirectoryExists(
            string path,
            string settingName)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException(
                    "Application configuration is invalid. The configured " +
                    settingName +
                    " could not be created or opened. " +
                    ex.Message,
                    ex);
            }
        }

        #endregion

        #region Paths

        private static string NormalizeConfiguredPath(
            string configuredPath,
            string settingName)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                throw CreateInvalidConfigurationException(
                    "The " +
                    settingName +
                    " path is required.");
            }

            if (configuredPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                throw CreateInvalidConfigurationException(
                    "The " +
                    settingName +
                    " path contains invalid characters.");
            }

            try
            {
                string path = Path.IsPathRooted(configuredPath)
                    ? configuredPath
                    : Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        configuredPath);

                return Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException(
                    "Application configuration is invalid. The " +
                    settingName +
                    " path is not valid. " +
                    ex.Message,
                    ex);
            }
        }

        #endregion

        #region Connection String

        private static string BuildConnectionString(DatabaseSettings settings)
        {
            MySqlConnectionStringBuilder builder =
                new MySqlConnectionStringBuilder();

            builder.Server = settings.Server;
            builder.Port = settings.Port;
            builder.Database = settings.Database;
            builder.UserID = settings.Username;
            builder.Password = settings.Password;
            builder["SslMode"] = settings.SslMode;

            return builder.ConnectionString;
        }

        #endregion
    }

    /// <summary>
    /// Strongly typed application configuration.
    /// </summary>
    public sealed class ApplicationSettings
    {
        /// <summary>
        /// MySQL database settings.
        /// </summary>
        public DatabaseSettings Database
        {
            get;
            internal set;
        }

        /// <summary>
        /// Application path settings.
        /// </summary>
        public PathSettings Paths
        {
            get;
            internal set;
        }
    }

    /// <summary>
    /// Strongly typed MySQL configuration.
    /// </summary>
    public sealed class DatabaseSettings
    {
        /// <summary>
        /// MySQL server host name or address.
        /// </summary>
        public string Server
        {
            get;
            internal set;
        }

        /// <summary>
        /// MySQL TCP port.
        /// </summary>
        public uint Port
        {
            get;
            internal set;
        }

        /// <summary>
        /// MySQL database name.
        /// </summary>
        public string Database
        {
            get;
            internal set;
        }

        /// <summary>
        /// MySQL user name.
        /// </summary>
        public string Username
        {
            get;
            internal set;
        }

        /// <summary>
        /// MySQL password.
        /// </summary>
        public string Password
        {
            get;
            internal set;
        }

        /// <summary>
        /// MySQL SSL mode.
        /// </summary>
        public string SslMode
        {
            get;
            internal set;
        }
    }

    /// <summary>
    /// Strongly typed application directory configuration.
    /// </summary>
    public sealed class PathSettings
    {
        /// <summary>
        /// Folder containing quiz BMP images.
        /// </summary>
        public string QuizImageFolder
        {
            get;
            internal set;
        }

        /// <summary>
        /// Folder for application logs.
        /// </summary>
        public string LogFolder
        {
            get;
            internal set;
        }

        /// <summary>
        /// Folder for exported files.
        /// </summary>
        public string ExportFolder
        {
            get;
            internal set;
        }

        /// <summary>
        /// Folder for generated report artifacts.
        /// </summary>
        public string ReportFolder
        {
            get;
            internal set;
        }
    }
}
