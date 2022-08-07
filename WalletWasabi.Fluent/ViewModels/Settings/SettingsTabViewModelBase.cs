using System.Reactive.Concurrency;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Settings;

public abstract class SettingsTabViewModelBase : RoutableViewModel
{
	public const int ThrottleTime = 500;

	protected SettingsTabViewModelBase()
	{
	}

	public static event EventHandler<RestartNeededEventArgs>? RestartNeeded;

	private static object ConfigLock { get; } = new();

	protected void Save()
	{
		if (Validations.Any)
		{
			return;
		}

		RxApp.MainThreadScheduler.Schedule(
			() =>
			{
				try
				{
					lock (ConfigLock)
					{
						EditConfigOnSave();
						OnConfigSaved();
					}
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
			});
	}

	protected abstract void EditConfigOnSave();

	private static void OnConfigSaved()
	{
		var isRestartNeeded = CheckIfRestartIsNeeded();

		RestartNeeded?.Invoke(
			typeof(SettingsTabViewModelBase),
			new RestartNeededEventArgs
			{
				IsRestartNeeded = isRestartNeeded
			});
	}

	public static bool CheckIfRestartIsNeeded()
	{
		return false; // isRestartNeeded;
	}
}
