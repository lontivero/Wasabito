using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class HideShowBehavior : DisposingBehavior<Window>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable.Merge(
				Observable.FromEventPattern((App)Application.Current!, nameof(App.ShowRequested)))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				if (AssociatedObject.WindowState == WindowState.Minimized)
				{
					AssociatedObject.WindowState = (WindowState)Enum.Parse(typeof(WindowState), Services.UiConfig.WindowState);
				}

				AssociatedObject.Show();
				AssociatedObject.BringIntoView();
			})
			.DisposeWith(disposables);

		// TODO: we need the close button click only, external close request should not be cancelled.
		Observable
			.FromEventPattern<CancelEventArgs>(AssociatedObject, nameof(AssociatedObject.Closing))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(args =>
			{
				if (Services.UiConfig.HideOnClose)
				{
					args.EventArgs.Cancel = true;
					AssociatedObject.Hide();
				}
			})
			.DisposeWith(disposables);
	}
}
