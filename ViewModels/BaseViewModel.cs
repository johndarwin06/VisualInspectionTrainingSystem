#region Namespaces

using System.ComponentModel;
using System.Runtime.CompilerServices;

#endregion

namespace VisualInspectionTrainingSystem.ViewModels
{
    /// <summary>
    /// Base class for every ViewModel.
    /// Provides PropertyChanged notification.
    /// </summary>
    public class BaseViewModel : INotifyPropertyChanged
    {

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Protected Methods

        protected virtual void OnPropertyChanged(
            [CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(
            ref T storage,
            T value,
            [CallerMemberName] string propertyName = "")
        {
            if (Equals(storage, value))
                return false;

            storage = value;

            OnPropertyChanged(propertyName);

            return true;
        }

        #endregion

    }
}