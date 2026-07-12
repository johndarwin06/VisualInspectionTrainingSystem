#region Namespaces

using System;
using System.Windows.Input;

#endregion

namespace VisualInspectionTrainingSystem.Commands
{
    /// <summary>
    /// A reusable command implementation that supports
    /// parameterless and parameterized commands.
    /// </summary>
    public class RelayCommand : ICommand
    {
        #region Fields

        private readonly Action _execute;
        private readonly Action<object> _executeParameter;

        private readonly Func<bool> _canExecute;
        private readonly Predicate<object> _canExecuteParameter;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a parameterless command.
        /// </summary>
        public RelayCommand(
            Action execute,
            Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        /// <summary>
        /// Creates a command with a parameter.
        /// </summary>
        public RelayCommand(
            Action<object> execute,
            Predicate<object> canExecute = null)
        {
            _executeParameter = execute;
            _canExecuteParameter = canExecute;
        }

        #endregion

        #region ICommand

        public bool CanExecute(object parameter)
        {
            if (_canExecute != null)
                return _canExecute();

            if (_canExecuteParameter != null)
                return _canExecuteParameter(parameter);

            return true;
        }

        public void Execute(object parameter)
        {
            if (_execute != null)
            {
                _execute();
                return;
            }

            _executeParameter?.Invoke(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add
            {
                CommandManager.RequerySuggested += value;
            }
            remove
            {
                CommandManager.RequerySuggested -= value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Refreshes all command states.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion
    }
}