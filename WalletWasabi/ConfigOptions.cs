using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters.Bitcoin;

namespace WalletWasabi;

public class GeneralOptions
{
	public string Network { get; set; } = "Main";
}

public class CoordinatorOptions
{
	public Uri Host { get; set; } = new Uri("http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/");
	public Uri Fallback { get; set; }
}

public class WalletOptions
{
	public string Directory { get; set; } = "wallets";
	
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money DustThreshold { get; set; } = Money.Zero;
}
