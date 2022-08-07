using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class CoinJoinFeeRateStatStore : PeriodicRunner
{
	public CoinJoinFeeRateStatStore(IOptionsMonitor<WabiSabiConfig> config, IRPCClient rpc, IEnumerable<CoinJoinFeeRateStat> feeRateStats)
		: base(TimeSpan.FromMinutes(10))
	{
		Config = config;
		Rpc = rpc;
		CoinJoinFeeRateStats = new(feeRateStats.OrderBy(x => x.DateTimeOffset));
	}

	public CoinJoinFeeRateStatStore(IOptionsMonitor<WabiSabiConfig> config, IRPCClient rpc)
		: this(config, rpc, Enumerable.Empty<CoinJoinFeeRateStat>())
	{
	}

	private static TimeSpan[] TimeFrames { get; } = Constants.CoinJoinFeeRateMedianTimeFrames.Select(tf => TimeSpan.FromHours(tf)).ToArray();

	private static TimeSpan MaximumTimeToStore { get; } = TimeFrames.Max();

	private List<CoinJoinFeeRateStat> CoinJoinFeeRateStats { get; }

	private CoinJoinFeeRateMedian[] DefaultMedians { get; set; } = Array.Empty<CoinJoinFeeRateMedian>();

	private IOptionsMonitor<WabiSabiConfig> Config { get; }
	private IRPCClient Rpc { get; }

	public event EventHandler<CoinJoinFeeRateStat>? NewStat;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.CurrentValue.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancel).ConfigureAwait(false)).FeeRate;

		CoinJoinFeeRateStat feeRateStat = new(DateTimeOffset.UtcNow, Config.CurrentValue.ConfirmationTarget, feeRate);
		Add(feeRateStat);
		NewStat?.Invoke(this, feeRateStat);
	}

	private void Add(CoinJoinFeeRateStat feeRateStat)
	{
		CoinJoinFeeRateStats.Add(feeRateStat);

		DefaultMedians = TimeFrames.Select(t => new CoinJoinFeeRateMedian(t, GetMedian(t))).ToArray();

		// Prune old items.
		DateTimeOffset removeBefore = DateTimeOffset.UtcNow - MaximumTimeToStore;
		while (CoinJoinFeeRateStats.Any() && CoinJoinFeeRateStats[0].DateTimeOffset < removeBefore)
		{
			CoinJoinFeeRateStats.RemoveAt(0);
		}
	}

	private FeeRate GetMedian(TimeSpan timeFrame)
	{
		var from = DateTimeOffset.UtcNow - timeFrame;
		var feeRates = CoinJoinFeeRateStats
			.Where(x => x.DateTimeOffset >= from)
			.OrderByDescending(x => x.FeeRate.SatoshiPerByte)
			.ToArray();

		// If the median is even, then it's the average of the middle two numbers.
		FeeRate med = feeRates.Length % 2 == 0
			? new FeeRate((feeRates[feeRates.Length / 2].FeeRate.SatoshiPerByte + feeRates[(feeRates.Length / 2) - 1].FeeRate.SatoshiPerByte) / 2)
			: feeRates[feeRates.Length / 2].FeeRate;

		return med;
	}

	/// <summary>
	/// The medians are calculated periodically in every <see cref="PeriodicRunner.Period"/> time span.
	/// </summary>
	public CoinJoinFeeRateMedian[] GetDefaultMedians()
	{
		return DefaultMedians;
	}
}
