using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NBitcoin;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Tor.StatusChecker;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent;

public static class Services
{
	private static IServiceProvider _provider;

	public static T? GetService<T>() => _provider.GetService<T>();
	public static T GetRequiredService<T>() => _provider.GetRequiredService<T>();
	public static string DataDir { get; private set; } = null!;

	public static TorSettings TorSettings => _provider.GetRequiredService<TorSettings>();
	public static BitcoinStore BitcoinStore => _provider.GetRequiredService<BitcoinStore>();
	public static HttpClientFactory HttpClientFactory  => _provider.GetRequiredService<HttpClientFactory>();
	public static TorOptions TorOptions => _provider.GetRequiredService<IOptions<TorOptions>>().Value;
	public static WalletOptions WalletOptions => _provider.GetRequiredService<IOptions<WalletOptions>>().Value;
	public static BitcoinIntegrationOptions BitcoinIntegrationOptions => _provider.GetRequiredService<IOptions<BitcoinIntegrationOptions>>().Value;
	public static UiConfig UiConfig => _provider.GetRequiredService<IOptions<UiConfig>>().Value;
	public static Network Network  => _provider.GetRequiredService<Network>();
	public static WasabiSynchronizer Synchronizer  => _provider.GetRequiredService<WasabiSynchronizer>();

	public static WalletManager WalletManager  => _provider.GetRequiredService<WalletManager>();

	public static TransactionBroadcaster TransactionBroadcaster  => _provider.GetRequiredService<TransactionBroadcaster>() ;

	public static TorStatusChecker TorStatusChecker => _provider.GetRequiredService<TorStatusChecker>();

	public static bool IsInitialized { get; private set; }

	/// <summary>
	/// Initializes global services used by fluent project.
	/// </summary>
	/// <param name="global">The global instance.</param>
	public static void Initialize(string dataDir, IServiceProvider serviceProvider)
	{
		_provider = serviceProvider;
		DataDir = dataDir;

		IsInitialized = true;
	}
}
