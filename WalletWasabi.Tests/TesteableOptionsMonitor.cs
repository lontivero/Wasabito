using Microsoft.Extensions.Options;

namespace WalletWasabi.Tests;

public class TesteableOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
{
	private Action<TOptions, string> _listener;
	private static readonly Disposable DisposableObject = new();

	public TesteableOptionsMonitor(TOptions currentValue)
	{
		CurrentValue = currentValue;
	}

	public TOptions CurrentValue { get; private set; }

	public TOptions Get(string name)
	{
		return CurrentValue;
	}

	public void Set(TOptions value)
	{
		CurrentValue = value;
		_listener.Invoke(value, null);
	}

	public IDisposable OnChange(Action<TOptions, string> listener)
	{
		_listener = listener;
		return DisposableObject;
	}

	class Disposable : IDisposable
	{
		public void Dispose() { }
	}
}
