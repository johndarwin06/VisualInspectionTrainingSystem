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
    /// Resolves application configuration values that can vary by workstation.
    /// </summary>
    internal static class ConfigurationService
    {
        #region Constants

        private const string DefaultConnectionName = "MySqlConnection";
        private const string DefaultConnectionEnvironmentVariable = "VITS_MYSQL_CONNECTION";
        private const string DefaultUserEnvironmentVariable = "VITS_MYSQL_USER";
        private const string DefaultPasswordEnvironmentVariable = "VITS_MYSQL_PASSWORD";
        private const string DefaultLocalConfigFile = "App.local.config";

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the MySQL connection string from environment, local config, or App.config.
        /// </summary>
        /// <returns>The resolved MySQL connection string.</returns>
        public static string GetMySqlConnectionString()
        {
            string connectionName = GetAppSetting(
                "MySqlConnectionName",
                DefaultConnectionName);

            string connectionSource;
            string connectionString = GetEnvironmentConnectionString(
                out connectionSource);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = GetLocalConnectionString(
                    connectionName,
                    out connectionSource);
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = GetConfiguredConnectionString(
                    connectionName,
                    out connectionSource);
            }

            connectionString = ApplyEnvironmentOverrides(connectionString);

            ValidateConnectionString(
                connectionString,
                connectionName,
                connectionSource);

            return connectionString;
        }

        #endregion

        #region Connection String Sources

        private static string GetEnvironmentConnectionString(
            out string connectionSource)
        {
            string variableName = GetAppSetting(
                "MySqlConnectionEnvironmentVariable",
                DefaultConnectionEnvironmentVariable);

            string connectionString = GetEnvironmentVariable(variableName);

            connectionSource = string.IsNullOrWhiteSpace(connectionString)
                ? null
                : "environment variable '" + variableName + "'";

            return connectionString;
        }

        private static string GetLocalConnectionString(
            string connectionName,
            out string connectionSource)
        {
            string localConfigFile = GetAppSetting(
                "MySqlLocalConfigFile",
                DefaultLocalConfigFile);

            connectionSource = null;

            foreach (string path in GetCandidateLocalConfigPaths(localConfigFile))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                connectionSource = "local configuration file '" + path + "'";

                return ReadLocalConnectionString(path, connectionName);
            }

            return null;
        }

        private static string GetConfiguredConnectionString(
            string connectionName,
            out string connectionSource)
        {
            ConnectionStringSettings settings =
                ConfigurationManager.ConnectionStrings[connectionName];

            if (settings == null ||
                string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new ConfigurationErrorsException(
                    "Connection string '" + connectionName +
                    "' was not found in application configuration. " +
                    "Create App.local.config from App.local.config.example, " +
                    "set VITS_MYSQL_CONNECTION, or set VITS_MYSQL_PASSWORD.");
            }

            connectionSource = "application configuration";

            return settings.ConnectionString;
        }

        #endregion

        #region Environment Overrides

        private static string ApplyEnvironmentOverrides(string connectionString)
        {
            string user = GetEnvironmentVariable(GetAppSetting(
                "MySqlUserEnvironmentVariable",
                DefaultUserEnvironmentVariable));

            string password = GetEnvironmentVariable(GetAppSetting(
                "MySqlPasswordEnvironmentVariable",
                DefaultPasswordEnvironmentVariable));

            if (string.IsNullOrWhiteSpace(user) &&
                string.IsNullOrWhiteSpace(password))
            {
                return connectionString;
            }

            MySqlConnectionStringBuilder builder =
                new MySqlConnectionStringBuilder(connectionString);

            if (!string.IsNullOrWhiteSpace(user))
            {
                builder["User ID"] = user;
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                builder["Password"] = password;
            }

            return builder.ConnectionString;
        }

        private static string GetEnvironmentVariable(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return null;
            }

            string value = Environment.GetEnvironmentVariable(variableName);

            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        #endregion

        #region Local Config

        private static IEnumerable<string> GetCandidateLocalConfigPaths(
            string localConfigFile)
        {
            List<string> paths = new List<string>();

            if (string.IsNullOrWhiteSpace(localConfigFile))
            {
                localConfigFile = DefaultLocalConfigFile;
            }

            if (Path.IsPathRooted(localConfigFile))
            {
                AddCandidatePath(paths, localConfigFile);

                return paths;
            }

            AddCandidatePath(
                paths,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, localConfigFile));

            AddCandidatePath(
                paths,
                Path.Combine(Environment.CurrentDirectory, localConfigFile));

            AddParentDirectoryCandidates(
                paths,
                AppDomain.CurrentDomain.BaseDirectory,
                localConfigFile);

            AddParentDirectoryCandidates(
                paths,
                Environment.CurrentDirectory,
                localConfigFile);

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
                 directory != null && depth < 6;
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
                existing => string.Equals(
                    existing,
                    fullPath,
                    StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                paths.Add(fullPath);
            }
        }

        private static string ReadLocalConnectionString(
            string path,
            string connectionName)
        {
            try
            {
                XDocument document = XDocument.Load(path);

                XElement root = document.Root;

                if (root == null)
                {
                    throw new ConfigurationErrorsException(
                        "Local configuration file is empty: " + path);
                }

                XElement connectionStrings = root.Element("connectionStrings");

                if (connectionStrings == null)
                {
                    throw new ConfigurationErrorsException(
                        "Local configuration file is missing <connectionStrings>: " +
                        path);
                }

                XElement connection = connectionStrings
                    .Elements("add")
                    .FirstOrDefault(element =>
                        string.Equals(
                            (string)element.Attribute("name"),
                            connectionName,
                            StringComparison.OrdinalIgnoreCase));

                if (connection == null)
                {
                    throw new ConfigurationErrorsException(
                        "Local configuration file does not define connection string '" +
                        connectionName + "': " + path);
                }

                string value = (string)connection.Attribute("connectionString");

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ConfigurationErrorsException(
                        "Local connection string '" + connectionName +
                        "' is empty: " + path);
                }

                return value.Trim();
            }
            catch (ConfigurationErrorsException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException(
                    "Unable to read local configuration file: " + path,
                    ex);
            }
        }

        #endregion

        #region Validation

        private static void ValidateConnectionString(
            string connectionString,
            string connectionName,
            string connectionSource)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ConfigurationErrorsException(
                    "Connection string '" + connectionName + "' is empty.");
            }

            try
            {
                MySqlConnectionStringBuilder builder =
                    new MySqlConnectionStringBuilder(connectionString);

                if (string.IsNullOrWhiteSpace(builder.Password))
                {
                    throw new ConfigurationErrorsException(
                        "MySQL password was not found in " +
                        (connectionSource ?? "configuration") +
                        ". Create App.local.config from App.local.config.example, " +
                        "set VITS_MYSQL_CONNECTION, or set VITS_MYSQL_PASSWORD.");
                }
            }
            catch (ConfigurationErrorsException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException(
                    "Connection string '" + connectionName + "' is invalid.",
                    ex);
            }
        }

        #endregion

        #region Helpers

        private static string GetAppSetting(
            string key,
            string fallbackValue)
        {
            string value = ConfigurationManager.AppSettings[key];

            return string.IsNullOrWhiteSpace(value)
                ? fallbackValue
                : value.Trim();
        }

        #endregion
    }
}
