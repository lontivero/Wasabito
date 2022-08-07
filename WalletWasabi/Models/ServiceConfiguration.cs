using System.Net;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models;

public class ServiceConfiguration
{
	public ServiceConfiguration(
		EndPoint? bitcoinCoreEndPoint,
		Money dustThreshold)
	{
		BitcoinCoreEndPoint = bitcoinCoreEndPoint;
		DustThreshold = dustThreshold;
	}

	public EndPoint? BitcoinCoreEndPoint { get; set; }
	public Money DustThreshold { get; set; }
}
