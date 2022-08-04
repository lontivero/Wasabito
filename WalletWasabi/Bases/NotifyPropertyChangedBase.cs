using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WalletWasabi.Bases;

public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
{
	#region Events

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected bool RaiseAndSetIfChanged<T>(ref T field, T value, string propertyName)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}

		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	#endregion Events
}
