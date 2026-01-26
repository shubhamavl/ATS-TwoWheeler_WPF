using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System; // Added for IDisposable
using ATS_TwoWheeler_WPF.Core; // Added
using ATS_TwoWheeler_WPF.Services; // Added
using ATS_TwoWheeler_WPF.Services.Interfaces; // Added

namespace ATS_TwoWheeler_WPF.ViewModels.Base
{
    public abstract class BaseViewModel : INotifyPropertyChanged, IDisposable // IDisposable is already there, but ensuring 'using System;' is present
    {
        public virtual void Dispose() { }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
