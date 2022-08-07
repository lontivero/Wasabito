using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;

namespace WalletWasabi.WebClients.BlockstreamInfo;

public class BlockstreamInfoFeeProvider : PeriodicRunner, IFeeProvider
{
	public BlockstreamInfoFeeProvider(BlockstreamInfoClient blockstreamInfoClient, TimeSpan? period = null) : base(period ?? TimeSpan.FromMinutes(3))
	{
		BlockstreamInfoClient = blockstreamInfoClient;
		IsPaused = true;
	}

	public event EventHandler<AllFeeEstimate>? AllFeeEstimateArrived;

	public BlockstreamInfoClient BlockstreamInfoClient { get; set; }
	public AllFeeEstimate? AllFeeEstimate { get; private set; }
	public bool InError { get; private set; } = false;
	public bool IsPaused { get; set; } = false;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		if (IsPaused)
		{
			return;
		}
		try
		{
			var allFeeEstimate = await BlockstreamInfoClient.GetFeeEstimatesAsync(cancel).ConfigureAwait(false);
			AllFeeEstimate = allFeeEstimate;

			if (allFeeEstimate.Estimations.Any())
			{
				AllFeeEstimateArrived?.Invoke(this, allFeeEstimate);
			}

			InError = false;
		}
		catch
		{
			InError = true;
			throw;
		}
	}
}
