#region Namespaces

using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Windows;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Provides centralized MySQL database access for the application.
    /// </summary>
    public class MySqlService : IDisposable
    {
        #region Fields

        private readonly string _connectionString;
        private MySqlConnection _connection;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the database service.
        /// </summary>
        public MySqlService()
        {
            _connectionString = ConfigurationService.GetMySqlConnectionString();

            _connection = new MySqlConnection(_connectionString);
        }

        #endregion

        #region Connection

        /// <summary>
        /// Opens the database connection.
        /// </summary>
        public void OpenConnection()
        {
            if (_connection == null)
            {
                _connection = new MySqlConnection(_connectionString);
            }

            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
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
                MessageBox.Show(
                    ex.Message,
                    "MySQL Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
            finally
            {
                CloseConnection();
            }
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
