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
    /// Provides secure workstation-local application configuration.
    /// </summary>
    internal static class ConfigurationService
    {
        #region Constants

        private const string SettingsFileKey = "DatabaseSettingsFile";
        private const string DefaultSettingsFile = "DatabaseSettings.local.config";
        private const string ExampleSettingsFile = "DatabaseSettings.example.config";

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates the MySQL connection string from the local database settings file.
        /// </summary>
        /// <returns>A MySQL connection string built from local configuration.</returns>
        public static string GetMySqlConnectionString()
        {
            string settingsPath = FindDatabaseSettingsFile();
            DatabaseSettings settings = ReadDatabaseSettings(settingsPath);

            return BuildConnectionString(settings);
        }

        #endregion

        #region File Discovery

        private static string FindDatabaseSettingsFile()
        {
            string configuredFile = ConfigurationManager.AppSettings[SettingsFileKey];

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
                "Database configuration is missing. Create " +
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
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configuredFile));

            AddCandidatePath(
                paths,
                Path.Combine(Environment.CurrentDirectory, configuredFile));

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
                AddCandidatePath(paths, Path.Combine(directory.FullName, fileName));

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

        private static DatabaseSettings ReadDatabaseSettings(string path)
        {
            try
            {
                XDocument document = XDocument.Load(path);

                XElement root = document.Root;

                if (root == null ||
                    !string.Equals(
                        root.Name.LocalName,
                        "databaseSettings",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw CreateInvalidConfigurationException(
                        "Root element must be <databaseSettings>.");
                }

                XElement mysql = root.Elements()
                    .FirstOrDefault(element => string.Equals(
                        element.Name.LocalName,
                        "mysql",
                        StringComparison.OrdinalIgnoreCase));

                if (mysql == null)
                {
                    throw CreateInvalidConfigurationException(
                        "Missing <mysql> settings element.");
                }

                DatabaseSettings settings = new DatabaseSettings
                {
                    Server = GetRequiredAttribute(mysql, "server"),
                    Database = GetRequiredAttribute(mysql, "database"),
                    Username = GetRequiredAttribute(mysql, "username"),
                    Password = GetRequiredAttribute(mysql, "password", true),
                    SslMode = GetRequiredAttribute(mysql, "sslMode"),
                    Port = GetRequiredPort(mysql)
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
                    "Database configuration is invalid. Check " +
                    DefaultSettingsFile +
                    " against " +
                    ExampleSettingsFile +
                    ". " +
                    ex.Message,
                    ex);
            }
        }

        private static string GetRequiredAttribute(
            XElement element,
            string attributeName)
        {
            return GetRequiredAttribute(
                element,
                attributeName,
                false);
        }

        private static string GetRequiredAttribute(
            XElement element,
            string attributeName,
            bool allowEmpty)
        {
            XAttribute attribute = element.Attribute(attributeName);

            if (attribute == null ||
                (!allowEmpty && string.IsNullOrWhiteSpace(attribute.Value)))
            {
                throw CreateInvalidConfigurationException(
                    "Missing required '" + attributeName + "' value.");
            }

            return attribute.Value.Trim();
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

        private static void ValidateSettings(DatabaseSettings settings)
        {
            try
            {
                BuildConnectionString(settings);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException(
                    "Database configuration contains an invalid MySQL setting. " +
                    ex.Message,
                    ex);
            }
        }

        private static ConfigurationErrorsException CreateInvalidConfigurationException(
            string detail)
        {
            return new ConfigurationErrorsException(
                "Database configuration is invalid. " +
                detail +
                " Use " +
                ExampleSettingsFile +
                " as the template.");
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

        #region Nested Types

        private sealed class DatabaseSettings
        {
            public string Server { get; set; }

            public uint Port { get; set; }

            public string Database { get; set; }

            public string Username { get; set; }

            public string Password { get; set; }

            public string SslMode { get; set; }
        }

        #endregion
    }
}
