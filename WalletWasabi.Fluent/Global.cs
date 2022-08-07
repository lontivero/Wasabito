using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Models;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent;

public class Global
{
	public async Task InitializeNoWalletAsync()
	{
		var cancel = CancellationToken.None;
		var bstoreInitTask = Services.BitcoinStore.InitializeAsync(cancel);

		try
		{
			await bstoreInitTask.ConfigureAwait(false);

			// Make sure that the height of the wallets will not be better than the current height of the filters.
			Services.WalletManager.SetMaxBestHeight(Services.BitcoinStore.IndexStore.SmartHeaderChain.TipHeight);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// If our internal data structures in the Bitcoin Store gets corrupted, then it's better to rescan all the wallets.
			Services.WalletManager.SetMaxBestHeight(SmartHeader.GetStartingHeader(Services.Network).Height);
			throw;
		}

		var requestInterval = Services.Network == Network.RegTest ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
		int maxFiltSyncCount = Services.Network == Network.Main ? 1000 : 10000; // On testnet, filters are empty, so it's faster to query them together

		Services.Synchronizer.Start(requestInterval, maxFiltSyncCount);

		var p2pNetwork = Services.GetService<P2pNetwork>();
		Services.TransactionBroadcaster.Initialize(p2pNetwork.Nodes, null);

		Services.WalletManager.RegisterServices(Services.BitcoinStore, Services.Synchronizer,
			Services.GetRequiredService<ServiceConfiguration>(),
			Services.GetRequiredService<HybridFeeProvider>(),
			Services.GetRequiredService<CachedBlockProvider>());
	}
}
