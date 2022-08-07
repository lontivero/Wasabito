using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters.Bitcoin;

namespace WalletWasabi;

public class CoordinatorOptions
{
	public Uri Host { get; set; }
	public Uri Fallback { get; set; }
}

public class WalletOptions
{
	public string Directory { get; set; }
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money DustThreshold { get; set; }
}
