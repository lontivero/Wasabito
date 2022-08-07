using System.Net;

namespace WalletWasabi.BitcoinCore.Rpc;

public class RpcOptions
{
	public Uri Host { get; set; }
	public string AuthenticationString { get; set; }
}

public class BitcoinOptions
{
	public EndPoint Host { get; set; }
}

public class BitcoinIntegrationOptions : BitcoinOptions
{
	public bool Enabled { get; set; } = false;  
	public bool StartLocalBitcoinCoreOnStartup { get; set; } = false;
	public bool StopLocalBitcoinCoreOnShutdown { get; set; } = true;
	public string LocalBitcoinCoreDataDir { get; set; }
}