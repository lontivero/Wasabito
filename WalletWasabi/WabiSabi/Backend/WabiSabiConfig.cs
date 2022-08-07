using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Backend;

public class WabiSabiConfig
{
	public uint ConfirmationTarget { get; set; } = 108;

	public Money MinRegistrableAmount { get; set; } = Money.Coins(0.00005m);
	public Money MaxRegistrableAmount { get; set; } = Money.Coins(43_000m);

	public bool AllowNotedInputRegistration { get; set; } = true;

	public TimeSpan StandardInputRegistrationTimeout { get; set; } = TimeSpan.FromHours(1);
	public TimeSpan BlameInputRegistrationTimeout { get; set; } = TimeSpan.FromMinutes(3);
	public TimeSpan ConnectionConfirmationTimeout { get; set; } = TimeSpan.FromMinutes(1);
	public TimeSpan OutputRegistrationTimeout { get; set; } = TimeSpan.FromMinutes(1);
	public TimeSpan TransactionSigningTimeout { get; set; } = TimeSpan.FromMinutes(1);

	public TimeSpan FailFastOutputRegistrationTimeout { get; set; } = TimeSpan.FromMinutes(3);
	public TimeSpan FailFastTransactionSigningTimeout { get; set; } = TimeSpan.FromMinutes(1);

	public TimeSpan RoundExpiryTimeout { get; set; } = TimeSpan.FromMinutes(5);
	public int MaxInputCountByRound { get; set; } = 100;

	public double MinInputCountByRoundMultiplier { get; set; } = 0.5;

	public int MinInputCountByRound => Math.Max(1, (int) (MaxInputCountByRound * MinInputCountByRoundMultiplier));

	public ExtPubKey CoordinatorExtPubKey { get; private set; } = Constants.WabiSabiFallBackCoordinatorExtPubKey;

	public int CoordinatorExtPubKeyCurrentDepth { get; private set; } = 1;

	public Money MaxSuggestedAmountBase { get; set; } = Money.Coins(0.1m);

	public int RoundParallelization { get; set; } = 1;

	public bool WW200CompatibleLoadBalancing { get; set; } = false;

	public double WW200CompatibleLoadBalancingInputSplit { get; set; } = 0.75;

	public Script GetNextCleanCoordinatorScript() => DeriveCoordinatorScript(CoordinatorExtPubKeyCurrentDepth);

	public Script DeriveCoordinatorScript(int index) => CoordinatorExtPubKey.Derive(0, false).Derive(index, false).PubKey.WitHash.ScriptPubKey;

	public void MakeNextCoordinatorScriptDirty()
	{
		CoordinatorExtPubKeyCurrentDepth++;
	}
}
