using Newtonsoft.Json;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Bases;
using WalletWasabi.Fluent.Converters;

namespace WalletWasabi.Fluent;

public class UiConfig : NotifyPropertyChangedBase
{
	private bool _privacyMode = false;
	private bool _isCustomChangeAddress = false;
	private bool _autoCopy = true;
	private bool _autoPaste = false;
	private int _feeDisplayUnit = 0;
	private bool _darkModeEnabled = true;
	private string? _lastSelectedWallet = null;
	private string _windowState = "Normal";
	private bool _runOnSystemStartup;
	private bool _oobe;
	private bool _hideOnClose;
	private int _feeTarget;
	private bool _sendAmountConversionReversed;

	public UiConfig()
	{
		this.WhenAnyValue(
				x => x.AutoCopy,
				x => x.AutoPaste,
				x => x.IsCustomChangeAddress,
				x => x.DarkModeEnabled,
				x => x.FeeDisplayUnit,
				x => x.LastSelectedWallet,
				x => x.WindowState,
				x => x.Oobe,
				x => x.RunOnSystemStartup,
				x => x.PrivacyMode,
				x => x.HideOnClose,
				x => x.FeeTarget,
				(_, _, _, _, _, _, _, _, _, _, _, _) => Unit.Default)
			.Throttle(TimeSpan.FromMilliseconds(500))
			.Skip(1) // Won't save on UiConfig creation.
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => ToFile());

		this.WhenAnyValue(x => x.SendAmountConversionReversed)
			.Throttle(TimeSpan.FromMilliseconds(500))
			.Skip(1) // Won't save on UiConfig creation.
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => ToFile());
	}

	public bool Oobe
	{
		get => _oobe;
		set => RaiseAndSetIfChanged(ref _oobe, value, nameof(Oobe));
	}

	[JsonProperty(PropertyName = "WindowState")]
	[JsonConverter(typeof(WindowStateAfterStartJsonConverter))]
	public string WindowState
	{
		get => _windowState;
		internal set => RaiseAndSetIfChanged(ref _windowState, value, nameof(WindowState));
	}

	public int FeeTarget
	{
		get => _feeTarget;
		internal set => RaiseAndSetIfChanged(ref _feeTarget, value, nameof(FeeTarget));
	}

	public int FeeDisplayUnit
	{
		get => _feeDisplayUnit;
		set => RaiseAndSetIfChanged(ref _feeDisplayUnit, value, nameof(FeeDisplayUnit));
	}

	public bool AutoCopy
	{
		get => _autoCopy;
		set => RaiseAndSetIfChanged(ref _autoCopy, value, nameof(AutoCopy));
	}

	public bool AutoPaste
	{
		get => _autoPaste;
		set => RaiseAndSetIfChanged(ref _autoPaste, value, nameof(AutoPaste));
	}

	public bool IsCustomChangeAddress
	{
		get => _isCustomChangeAddress;
		set => RaiseAndSetIfChanged(ref _isCustomChangeAddress, value, nameof(IsCustomChangeAddress));
	}

	public bool PrivacyMode
	{
		get => _privacyMode;
		set => RaiseAndSetIfChanged(ref _privacyMode, value, nameof(PrivacyMode));
	}

	public bool DarkModeEnabled
	{
		get => _darkModeEnabled;
		set => RaiseAndSetIfChanged(ref _darkModeEnabled, value, nameof(DarkModeEnabled));
	}

	public string? LastSelectedWallet
	{
		get => _lastSelectedWallet;
		set => RaiseAndSetIfChanged(ref _lastSelectedWallet, value, nameof(LastSelectedWallet));
	}

	public bool RunOnSystemStartup
	{
		get => _runOnSystemStartup;
		set => RaiseAndSetIfChanged(ref _runOnSystemStartup, value, nameof(RunOnSystemStartup));
	}

	public bool HideOnClose
	{
		get => _hideOnClose;
		set => RaiseAndSetIfChanged(ref _hideOnClose, value, nameof(HideOnClose));
	}

	public bool SendAmountConversionReversed
	{
		get => _sendAmountConversionReversed;
		internal set => RaiseAndSetIfChanged(ref _sendAmountConversionReversed, value, nameof(SendAmountConversionReversed));
	}

	private void ToFile()
	{
		var writableUiConfig = Services.GetRequiredService<IWritableOptions<UiConfig>>();
		writableUiConfig.Update(uiConfig =>
		{
			uiConfig._autoCopy = _autoCopy;
			uiConfig._oobe = _oobe;
			uiConfig._autoPaste = _autoPaste;
			uiConfig._feeTarget = _feeTarget;
			uiConfig._privacyMode = _privacyMode;
			uiConfig._darkModeEnabled = _darkModeEnabled;
			uiConfig._feeDisplayUnit = _feeDisplayUnit;
			uiConfig._hideOnClose = _hideOnClose;
			uiConfig._lastSelectedWallet = _lastSelectedWallet;
			uiConfig._isCustomChangeAddress = _isCustomChangeAddress;
			uiConfig._runOnSystemStartup = _runOnSystemStartup;
			uiConfig._sendAmountConversionReversed = _sendAmountConversionReversed;
		});
	}
}
