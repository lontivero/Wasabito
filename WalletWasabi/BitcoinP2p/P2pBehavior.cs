using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinP2p;

public class P2pBehavior : NodeBehavior
{
	private const int MaxInvSize = 50000;

	public P2pBehavior(MempoolService mempoolService)
	{
		MempoolService = Guard.NotNull(nameof(mempoolService), mempoolService);
	}

	public MempoolService MempoolService { get; }

	protected override void AttachCore()
	{
		AttachedNode.MessageReceived += AttachedNode_MessageReceivedAsync;
	}

	protected override void DetachCore()
	{
		AttachedNode.MessageReceived -= AttachedNode_MessageReceivedAsync;
	}

	private async void AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
	{
		try
		{
			if (message.Message.Payload is GetDataPayload getDataPayload)
			{
				await ProcessGetDataAsync(node, getDataPayload).ConfigureAwait(false);
			}
			else if (message.Message.Payload is TxPayload txPayload)
			{
				ProcessTx(txPayload);
			}
			else if (message.Message.Payload is InvPayload invPayload)
			{
				await ProcessInventoryAsync(node, invPayload).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException ex)
		{
			Logger.LogDebug(ex);
		}
		catch (Exception ex)
		{
			Logger.LogInfo($"Ignoring {ex.GetType()}: {ex.Message}");
			Logger.LogDebug(ex);
		}
	}

	private async Task ProcessInventoryAsync(Node node, InvPayload invPayload)
	{
		var getDataPayload = new GetDataPayload();
		foreach (var inv in invPayload.Inventory)
		{
			if (ProcessInventoryVector(inv, node.RemoteSocketEndpoint))
			{
				getDataPayload.Inventory.Add(inv);
			}
		}
		if (getDataPayload.Inventory.Any() && node.IsConnected)
		{
			await node.SendMessageAsync(getDataPayload).ConfigureAwait(false);
		}
	}

	private bool ProcessInventoryVector(InventoryVector inv, EndPoint remoteSocketEndpoint)
	{
		if (inv.Type.HasFlag(InventoryType.MSG_TX))
		{
			if (MempoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry? entry)) // If we have the transaction then adjust confirmation.
			{
				if (entry.NodeRemoteSocketEndpoint == remoteSocketEndpoint.ToString())
				{
					return false; // Wtf, why are you trying to broadcast it back to us?
				}

				entry.ConfirmPropagationOnce();
			}

			// If we already processed it or we're in trusted node mode, then don't ask for it.
			if (MempoolService.IsProcessed(inv.Hash))
			{
				return false;
			}

			return true;
		}

		return false;
	}

	private async Task ProcessGetDataAsync(Node node, GetDataPayload payload)
	{
		if (payload.Inventory.Count > MaxInvSize)
		{
			Logger.LogDebug($"Received inventory too big. {nameof(MaxInvSize)}: {MaxInvSize}, Node: {node.RemoteSocketEndpoint}");
			return;
		}

		foreach (var inv in payload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
		{
			if (MempoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry? entry)) // If we have the transaction to be broadcasted then broadcast it now.
			{
				if (entry.NodeRemoteSocketEndpoint != node.RemoteSocketEndpoint.ToString())
				{
					continue; // Would be strange. It could be some kind of attack.
				}

				try
				{
					var txPayload = new TxPayload(entry.Transaction.Transaction);
					if (!node.IsConnected)
					{
						Logger.LogInfo($"Could not serve transaction. Node ({node.RemoteSocketEndpoint}) is not connected anymore: {entry.TransactionId}.");
					}
					else
					{
						await node.SendMessageAsync(txPayload).ConfigureAwait(false);
						entry.MakeBroadcasted();
						Logger.LogInfo($"Successfully served transaction to node ({node.RemoteSocketEndpoint}): {entry.TransactionId}.");
					}
				}
				catch (Exception ex)
				{
					Logger.LogInfo(ex);
				}
			}
		}
	}

	protected virtual void ProcessTx(TxPayload payload)
	{
		Transaction transaction = payload.Object;
		transaction.PrecomputeHash(false, true);
		MempoolService.Process(transaction);
	}
	
	public override object Clone() => new P2pBehavior(MempoolService);	
}
