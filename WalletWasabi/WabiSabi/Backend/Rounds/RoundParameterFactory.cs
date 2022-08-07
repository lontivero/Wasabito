using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Options;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class RoundParameterFactory
{
	public RoundParameterFactory(IOptionsMonitor<WabiSabiConfig> config, Network network)
	{
		Config = config;
		Network = network;
	}

	public IOptionsMonitor<WabiSabiConfig> Config { get; }
	public Network Network { get; }

	public virtual RoundParameters CreateRoundParameter(FeeRate feeRate, Money maxSuggestedAmount) =>
		RoundParameters.Create(
			Config.CurrentValue,
			Network,
			feeRate,
			maxSuggestedAmount);

	public virtual RoundParameters CreateBlameRoundParameter(FeeRate feeRate, Round blameOf) =>
		RoundParameters.Create(
			Config.CurrentValue,
			Network,
			feeRate,
			blameOf.Parameters.MaxSuggestedAmount);
}
