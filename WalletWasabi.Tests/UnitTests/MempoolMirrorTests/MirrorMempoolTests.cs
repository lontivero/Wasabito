using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.MempoolMirrorTests;

public class MirrorMempoolTests
{
	[Fact]
	public async Task CanCopyMempoolFromRpcAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync(nameof(CanCopyMempoolFromRpcAsync));
		using var mempoolMirror = new MempoolMirror(coreNode.RpcClient, coreNode.P2pNode, period: TimeSpan.FromSeconds(2));
		await mempoolMirror.StartAsync(CancellationToken.None);

		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			var spendAmount = new Money(0.0004m, MoneyUnit.BTC);
			var spendAmount2 = new Money(0.0004m, MoneyUnit.BTC);
			await rpc.GenerateAsync(101);

			var txid = await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), spendAmount);
			var txid2 = await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), spendAmount2);

			while ((await rpc.GetRawMempoolAsync()).Length != 2)
			{
				await Task.Delay(50);
			}

			await mempoolMirror.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
			var localMempoolHashes = mempoolMirror.GetMempoolHashes();

			Assert.Equal(2, localMempoolHashes.Count);
			Assert.Contains(txid, localMempoolHashes);
			Assert.Contains(txid2, localMempoolHashes);
		}
		finally
		{
			await mempoolMirror.StopAsync(CancellationToken.None);
			await coreNode.TryStopAsync();
		}
	}

	[Fact]
	public async Task CanHandleArrivingTxAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync(nameof(CanHandleArrivingTxAsync));
		using var mempoolMirror = new MempoolMirror(coreNode.RpcClient, coreNode.P2pNode, period: TimeSpan.FromSeconds(7));
		await mempoolMirror.StartAsync(CancellationToken.None);

		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			await rpc.GenerateAsync(101);

			var txid = await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), new Money(0.0004m, MoneyUnit.BTC));

			while (!(await rpc.GetRawMempoolAsync()).Any())
			{
				await Task.Delay(50);
			}

			await mempoolMirror.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var localMempoolHashes = mempoolMirror.GetMempoolHashes();

			Assert.Single(localMempoolHashes);
			Assert.Contains(txid, localMempoolHashes);
		}
		finally
		{
			await mempoolMirror.StopAsync(CancellationToken.None);
			await coreNode.TryStopAsync();
		}
	}

	[Fact]
	public async Task CanHandleTheSameTxSentManyTimesAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync(nameof(CanHandleTheSameTxSentManyTimesAsync));
		using var mempoolMirror = new MempoolMirror(coreNode.RpcClient, coreNode.P2pNode, period: TimeSpan.FromSeconds(2));
		await mempoolMirror.StartAsync(CancellationToken.None);

		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;

			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			using var k1 = new Key();
			var blockIds = await rpc.GenerateToAddressAsync(1, k1.PubKey.WitHash.GetAddress(network));
			var block = await rpc.GetBlockAsync(blockIds[0]);
			var coinBaseTx = block.Transactions[0];

			var tx = Transaction.Create(network);
			tx.Inputs.Add(coinBaseTx, 0);
			tx.Outputs.Add(Money.Coins(49.9999m), BitcoinFactory.CreateBitcoinAddress(network));
			tx.Sign(k1.GetBitcoinSecret(network), coinBaseTx.Outputs.AsCoins().First());
			var valid = tx.Check();

			await rpc.GenerateAsync(101);

			for (int i = 0; i < 5; i++)
			{
				await rpc.SendRawTransactionAsync(tx);
			}

			while (!(await rpc.GetRawMempoolAsync()).Any())
			{
				await Task.Delay(50);
			}

			await mempoolMirror.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolHashes = mempoolMirror.GetMempoolHashes();

			Assert.Single(localMempoolHashes);
			Assert.Contains(tx.GetHash(), localMempoolHashes);
		}
		finally
		{
			await mempoolMirror.StopAsync(CancellationToken.None);
			await coreNode.TryStopAsync();
		}
	}

	[Fact]
	public async Task CanHandleManyTxsAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync(nameof(CanHandleManyTxsAsync));
		using var mempoolMirror = new MempoolMirror(coreNode.RpcClient, coreNode.P2pNode, period: TimeSpan.FromSeconds(2));
		await mempoolMirror.StartAsync(CancellationToken.None);

		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;

			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			using var k1 = new Key();
			var blockIds = await rpc.GenerateToAddressAsync(1, k1.PubKey.WitHash.GetAddress(network));
			var block = await rpc.GetBlockAsync(blockIds[0]);
			var coinBaseTx = block.Transactions[0];

			await rpc.GenerateAsync(101);

			for (int i = 0; i < 5; i++)
			{
				await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), new Money(0.0004m, MoneyUnit.BTC));
			}

			while ((await rpc.GetRawMempoolAsync()).Length != 5)
			{
				await Task.Delay(50);
			}

			await mempoolMirror.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolHashes = mempoolMirror.GetMempoolHashes();

			Assert.Equal(5, localMempoolHashes.Count);
		}
		finally
		{
			await mempoolMirror.StopAsync(CancellationToken.None);
			await coreNode.TryStopAsync();
		}
	}

	[Fact]
	public async Task CanHandleConfirmationAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync(nameof(CanHandleConfirmationAsync));
		using var mempoolMirror = new MempoolMirror(coreNode.RpcClient, coreNode.P2pNode, period: TimeSpan.FromSeconds(2));
		await mempoolMirror.StartAsync(CancellationToken.None);

		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;

			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			var spendAmount = new Money(0.0004m, MoneyUnit.BTC);

			await rpc.GenerateAsync(101);

			var rpcMempoolBeforeSend = await rpc.GetRawMempoolAsync();
			var localMempoolBeforeSend = mempoolMirror.GetMempoolHashes();
			Assert.Equal(rpcMempoolBeforeSend.Length, localMempoolBeforeSend.Count);

			await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), spendAmount);
			while (!(await rpc.GetRawMempoolAsync()).Any())
			{
				await Task.Delay(50);
			}

			await mempoolMirror.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolAfterSend = mempoolMirror.GetMempoolHashes();
			Assert.Equal(1, localMempoolAfterSend.Count);
			Assert.Single(localMempoolAfterSend);

			await rpc.GenerateAsync(1);
			while ((await rpc.GetRawMempoolAsync()).Any())
			{
				await Task.Delay(50);
			}
			await mempoolMirror.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolAfterBlockMined = mempoolMirror.GetMempoolHashes();
			Assert.Empty(localMempoolAfterBlockMined);
		}
		finally
		{
			await mempoolMirror.StopAsync(CancellationToken.None);
			await coreNode.TryStopAsync();
		}
	}
}
