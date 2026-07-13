#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Configuration;
using System.Data;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Provides centralized MySQL database access for the application.
    /// </summary>
    public class MySqlService : IDisposable
    {
        #region Fields

        private readonly DatabaseSettings _settings;
        private readonly string _connectionString;
        private MySqlConnection _connection;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the database service.
        /// </summary>
        public MySqlService()
        {
            _settings = ConfigurationService.GetApplicationSettings().Database;

            _connectionString = ConfigurationService.GetMySqlConnectionString();

            _connection = new MySqlConnection(_connectionString);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Last non-sensitive connection error message.
        /// </summary>
        public string LastConnectionError
        {
            get;
            private set;
        }

        #endregion

        #region Connection

        /// <summary>
        /// Opens the database connection.
        /// </summary>
        public void OpenConnection()
        {
            OpenConnection(CancellationToken.None);
        }

        /// <summary>
        /// Opens the database connection with cancellation support.
        /// </summary>
        public void OpenConnection(CancellationToken cancellationToken)
        {
            if (IsConnectionOpen())
            {
                return;
            }

            Exception lastException = null;
            int attempts = GetAttemptCount();

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ResetConnection();

                try
                {
                    _connection.Open();
                    LastConnectionError = string.Empty;

                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (!ShouldRetryConnectionFailure(ex) ||
                        attempt >= attempts)
                    {
                        LastConnectionError = BuildConnectionErrorMessage(
                            attempt,
                            attempts,
                            ex);

                        throw CreateConnectionException(
                            attempt,
                            attempts,
                            ex);
                    }

                    DelayBeforeRetry(cancellationToken);
                }
            }

            throw CreateConnectionException(
                attempts,
                attempts,
                lastException);
        }

        /// <summary>
        /// Opens the database connection asynchronously with cancellation support.
        /// </summary>
        public async Task OpenConnectionAsync(CancellationToken cancellationToken)
        {
            if (IsConnectionOpen())
            {
                return;
            }

            Exception lastException = null;
            int attempts = GetAttemptCount();

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ResetConnection();

                try
                {
                    await _connection.OpenAsync(cancellationToken);

                    LastConnectionError = string.Empty;

                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (IsCancellation(ex))
                    {
                        LastConnectionError =
                            "Database connection timed out or was cancelled.";

                        throw;
                    }

                    if (!ShouldRetryConnectionFailure(ex) ||
                        attempt >= attempts)
                    {
                        LastConnectionError = BuildConnectionErrorMessage(
                            attempt,
                            attempts,
                            ex);

                        throw CreateConnectionException(
                            attempt,
                            attempts,
                            ex);
                    }

                    await Task.Delay(
                        _settings.RetryDelayMilliseconds,
                        cancellationToken);
                }
            }

            throw CreateConnectionException(
                attempts,
                attempts,
                lastException);
        }

        /// <summary>
        /// Closes the database connection.
        /// </summary>
        public void CloseConnection()
        {
            if (_connection != null &&
                _connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }
        }

        /// <summary>
        /// Returns the current MySQL connection.
        /// </summary>
        public MySqlConnection GetConnection()
        {
            return _connection;
        }

        #endregion

        #region Execute Methods

        /// <summary>
        /// Executes INSERT, UPDATE, DELETE.
        /// </summary>
        public int ExecuteNonQuery(
            string sql,
            params MySqlParameter[] parameters)
        {
            OpenConnection();

            using (MySqlCommand command = new MySqlCommand(sql, _connection))
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }

                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes COUNT(), MAX(), MIN(), etc.
        /// </summary>
        public object ExecuteScalar(
            string sql,
            params MySqlParameter[] parameters)
        {
            OpenConnection();

            using (MySqlCommand command = new MySqlCommand(sql, _connection))
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }

                return command.ExecuteScalar();
            }
        }

        /// <summary>
        /// Returns a DataTable.
        /// </summary>
        public DataTable ExecuteDataTable(
            string sql,
            params MySqlParameter[] parameters)
        {
            OpenConnection();

            using (MySqlCommand command = new MySqlCommand(sql, _connection))
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }

                using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
                {
                    DataTable table = new DataTable();

                    adapter.Fill(table);

                    return table;
                }
            }
        }

        /// <summary>
        /// Executes a DataReader.
        /// Caller must close the reader.
        /// </summary>
        public MySqlDataReader ExecuteReader(
            string sql,
            params MySqlParameter[] parameters)
        {
            OpenConnection();

            MySqlCommand command = new MySqlCommand(sql, _connection);

            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            return command.ExecuteReader();
        }

        #endregion

        #region Transaction

        /// <summary>
        /// Starts a transaction.
        /// </summary>
        public MySqlTransaction BeginTransaction()
        {
            OpenConnection();

            return _connection.BeginTransaction();
        }

        #endregion

        #region Connection Test

        /// <summary>
        /// Tests the database connection.
        /// </summary>
        public bool TestConnection()
        {
            try
            {
                OpenConnection();

                return _connection.State == ConnectionState.Open;
            }
            catch (Exception ex)
            {
                LastConnectionError = CreateSafeConnectionMessage(ex);

                return false;
            }
            finally
            {
                CloseConnection();
            }
        }

        /// <summary>
        /// Tests the database connection asynchronously.
        /// </summary>
        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                await OpenConnectionAsync(cancellationToken);

                return _connection.State == ConnectionState.Open;
            }
            catch (Exception ex)
            {
                LastConnectionError = CreateSafeConnectionMessage(ex);

                return false;
            }
            finally
            {
                CloseConnection();
            }
        }

        #endregion

        #region Retry Helpers

        private int GetAttemptCount()
        {
            return Math.Max(
                1,
                _settings.RetryCount + 1);
        }

        private bool IsConnectionOpen()
        {
            return _connection != null &&
                   _connection.State == ConnectionState.Open;
        }

        private void ResetConnection()
        {
            if (_connection != null)
            {
                _connection.Dispose();
            }

            _connection = new MySqlConnection(_connectionString);
        }

        private void DelayBeforeRetry(CancellationToken cancellationToken)
        {
            if (_settings.RetryDelayMilliseconds <= 0)
            {
                return;
            }

            cancellationToken.WaitHandle.WaitOne(
                _settings.RetryDelayMilliseconds);

            cancellationToken.ThrowIfCancellationRequested();
        }

        private bool ShouldRetryConnectionFailure(Exception ex)
        {
            if (IsCancellation(ex) ||
                IsAuthenticationFailure(ex))
            {
                return false;
            }

            if (ex is TimeoutException ||
                ex is SocketException)
            {
                return true;
            }

            MySqlException mysqlException = ex as MySqlException;

            if (mysqlException != null)
            {
                return IsTransientMySqlError(mysqlException);
            }

            if (ex.InnerException != null)
            {
                return ShouldRetryConnectionFailure(ex.InnerException);
            }

            return false;
        }

        private static bool IsTransientMySqlError(MySqlException ex)
        {
            switch (ex.Number)
            {
                case 0:
                case 1042:
                case 1043:
                case 2002:
                case 2003:
                case 2013:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAuthenticationFailure(Exception ex)
        {
            MySqlException mysqlException = ex as MySqlException;

            if (mysqlException != null &&
                (mysqlException.Number == 1044 ||
                 mysqlException.Number == 1045))
            {
                return true;
            }

            string message = ex.Message;

            if (!string.IsNullOrWhiteSpace(message) &&
                (message.IndexOf(
                     "Access denied for user",
                     StringComparison.OrdinalIgnoreCase) >= 0 ||
                 message.IndexOf(
                     "Authentication to host",
                     StringComparison.OrdinalIgnoreCase) >= 0 ||
                 message.IndexOf(
                     "using password",
                     StringComparison.OrdinalIgnoreCase) >= 0 ||
                 message.IndexOf(
                     "RSA public key",
                     StringComparison.OrdinalIgnoreCase) >= 0 ||
                 message.IndexOf(
                     "public key retrieval",
                     StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            if (ex.InnerException != null)
            {
                return IsAuthenticationFailure(ex.InnerException);
            }

            return false;
        }

        private static bool IsCancellation(Exception ex)
        {
            return ex is OperationCanceledException ||
                   ex is TaskCanceledException;
        }

        private string BuildConnectionErrorMessage(
            int attempt,
            int attempts,
            Exception ex)
        {
            if (IsAuthenticationFailure(ex))
            {
                return "Database authentication failed. Check the local database username and password.";
            }

            if (IsCancellation(ex))
            {
                return "Database connection timed out or was cancelled.";
            }

            return "Database connection failed after " +
                   attempt +
                   " of " +
                   attempts +
                   " attempt(s). Check that MySQL is running and reachable.";
        }

        private InvalidOperationException CreateConnectionException(
            int attempt,
            int attempts,
            Exception ex)
        {
            return new InvalidOperationException(
                BuildConnectionErrorMessage(
                    attempt,
                    attempts,
                    ex),
                ex);
        }

        private string CreateSafeConnectionMessage(Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(LastConnectionError))
            {
                return LastConnectionError;
            }

            if (ex is ConfigurationErrorsException)
            {
                return ex.Message;
            }

            if (IsAuthenticationFailure(ex))
            {
                return "Database authentication failed. Check the local database username and password.";
            }

            if (IsCancellation(ex))
            {
                return "Database connection timed out or was cancelled.";
            }

            return "Database connection failed. Check that MySQL is running and reachable.";
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases the database connection.
        /// </summary>
        public void Dispose()
        {
            CloseConnection();

            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
        }

        #endregion
    }
}
