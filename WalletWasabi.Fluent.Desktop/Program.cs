using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.ReactiveUI;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Avalonia.OpenGL;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.CrashReport;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Rpc;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Rpc;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.TypeConverters;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.BlockstreamInfo;
using WalletWasabi.WebClients.Wasabi;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Concurrency;
using WalletWasabi.Fluent.Desktop.Extensions;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.Desktop;

public class Program
{
	private static Global? Global;

	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	public static int Main(string[] args)
	{
		bool runGuiInBackground = args.Any(arg => arg.Contains(StartupHelper.SilentArgument));

		//CreateHostBuilder(args).Build().Run();
		TypeDescriptor.AddAttributes(typeof(Money),new TypeConverterAttribute(typeof(MoneyConverter)));
		TypeDescriptor.AddAttributes(typeof(ExtPubKey),new TypeConverterAttribute(typeof(ExtPubKeyConverter)));
		TypeDescriptor.AddAttributes(typeof(Uri),new TypeConverterAttribute(typeof(UriConverter)));
		TypeDescriptor.AddAttributes(typeof(EndPoint),new TypeConverterAttribute(typeof(EndPointConverter)));

		var config = new ConfigurationBuilder()
			.AddEnvironmentVariables(prefix: "WWL_")
			.AddCommandLine(args)
			.Build();

		var dataDirectory = Path.Combine(
			EnvironmentHelpers.ExpandDirectory(
				config.GetValue<string>("datadir") ?? EnvironmentHelpers.GetDefaultDataDir()), "client");

		var hostBuilder = CreateHostBuilder(args);
		hostBuilder.ConfigureAppConfiguration(configurationBuilder =>
		{
			var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
			configurationBuilder
				.SetBasePath(dataDirectory)
				.AddJsonFile("config.json", true, true)
				.AddJsonFile($"config.{environment}.json", true, true)
				.AddEnvironmentVariables(prefix: "WWL_")
				.AddCommandLine(args)
				.Build();

		});
		hostBuilder.ConfigureLogging((ctx, logging) =>
		{
			logging.AddConfiguration(ctx.Configuration);
			logging.AddConsole();
			logging.AddDebug();
		});
		hostBuilder.ConfigureServices((ctx, services) =>
		{
			services.AddOptions();
			services.AddLogging(logging =>
				logging.AddFilter((s, level) => level >= Microsoft.Extensions.Logging.LogLevel.Warning));

			// Configurations
			var configurationRoot = ctx.Configuration;
			services.ConfigureWritable<GeneralOptions>(configurationRoot);
			services.ConfigureWritable<JsonRpcServerConfiguration>(configurationRoot);
			services.ConfigureWritable<CoordinatorOptions>(configurationRoot);
			services.ConfigureWritable<WalletOptions>(configurationRoot);
			services.ConfigureWritable<TorOptions>(configurationRoot);
			services.ConfigureWritable<BitcoinIntegrationOptions>(configurationRoot);
			services.ConfigureWritable<UiConfig>(configurationRoot);

			GeneralOptions generalOptions = new();
			ctx.Configuration.GetSection(key: Option2Config(nameof(GeneralOptions))).Bind(generalOptions);
			var network =  Network.GetNetwork(generalOptions.Network)
			              ?? throw new ArgumentException("Unsupported Network.");

			services.AddSingleton(_ => network);
			BitcoinIntegrationOptions bitcoinIntegrationOptions = new();
			ctx.Configuration.GetSection(key: Option2Config(nameof(BitcoinIntegrationOptions))).Bind(bitcoinIntegrationOptions);
			services.AddSingleton(p =>
			{
				var bitcoinOptions = p.GetRequiredService<IOptions<BitcoinIntegrationOptions>>();
				var walletOptions = p.GetRequiredService<IOptions<WalletOptions>>();
				return new ServiceConfiguration(bitcoinOptions.Value.Host,
					walletOptions.Value.DustThreshold);
			});

			services.AddSingleton(p =>
			{
				var walletOptions = p.GetRequiredService<IOptions<WalletOptions>>();
				return new WalletDirectories(network, Path.Combine(dataDirectory, walletOptions.Value.Directory));
			});
			services.AddSingleton<WalletManager>();

			services.AddSingleton(p =>
			{
				var torOptions = p.GetRequiredService<IOptions<TorOptions>>();
				return new TorSettings(torOptions.Value.DataDirectory ?? dataDirectory, torOptions.Value.TerminateOnExit, Environment.ProcessId);
			});
			services.AddSingleton(p => new AllTransactionStore(Path.Combine(dataDirectory, "BitcoinStore", network.ToString()), network));

			services.AddSingleton(p => new IndexStore(Path.Combine(dataDirectory, "IndexStore"), network, new(maxChainSize: 20_000)));

			services.AddSingleton<MempoolService>();
			services.AddSingleton<IRepository<uint256, Block>>(p => new FileSystemBlockRepository(Path.Combine(dataDirectory, "Blocks"), network));
			services.AddSingleton<BitcoinStore>();

			services.AddSingleton(p =>
			{
				var coordinatorOptions = p.GetRequiredService<IOptions<CoordinatorOptions>>();
				var torOptions = p.GetRequiredService<IOptions<TorOptions>>();
				return new HttpClientFactory(torOptions.Value.SocksEndpoint,
					backendUriGetter: () => coordinatorOptions.Value.Host);
			});

			services.AddSingleton<IWasabiHttpClientFactory>(p =>
				p.GetRequiredService<HttpClientFactory>());

			services.AddSingleton<WasabiSynchronizer>();
			services.AddSingleton<TransactionBroadcaster>();

			services.AddSingleton<IMemoryCache>(p =>
				new MemoryCache(new MemoryCacheOptions {
					SizeLimit = 1_000,
					ExpirationScanFrequency = TimeSpan.FromSeconds(30)
				}));

			services.AddBackgroundService<UpdateChecker>();
			services.AddBackgroundService<P2pNetwork>();

			services.AddSingleton<BlockstreamInfoClient>();
			services.AddBackgroundService<BlockstreamInfoFeeProvider>();
			services.AddSingleton<FeeProvider>();
			services.AddBackgroundService<IFeeProvider, FeeProvider>();

			services.AddSingleton<IWabiSabiApiRequestHandler>(p =>
			{
				PersonCircuit roundStateUpdaterCircuit = new();
				HttpClientFactory httpClientFactory = p.GetRequiredService<HttpClientFactory>();
				var roundStateUpdaterHttpClient = httpClientFactory.NewHttpClient(Mode.SingleCircuitPerLifetime, roundStateUpdaterCircuit);
				return new WabiSabiHttpApiClient(roundStateUpdaterHttpClient);
			});

			services.AddBackgroundService<RoundStateUpdater>( );
			services.AddBackgroundService<CoinJoinManager>();
			services.AddSingleton<CoinJoinProcessor>();

			if (/*Config.UseTor && */ network != Network.RegTest)
			{
				services.AddSingleton(p =>
				{
					var torSettings = p.GetRequiredService<TorSettings>();
					var torManager = new TorProcessManager(torSettings);
					torManager.StartAsync(attempts: 3, CancellationToken.None).GetAwaiter().GetResult();
					return torManager;
				});

				services.AddBackgroundService<TorMonitor>();
			}

			services.AddSingleton<IJsonRpcService, WasabiJsonRpcService>();
			services.AddBackgroundService<JsonRpcServer>();

			services.AddSingleton(p =>
			{
				var p2pNetwork = p.GetRequiredService<P2pNetwork>();
				return p2pNetwork.Nodes;
			});
			services.AddSingleton<CoreNode>(_=> null!);
			services.AddSingleton<P2pBlockProvider>();
			services.AddSingleton<IBlockProvider, SmartBlockProvider>(p =>
			{
				var provider = p.GetRequiredService<P2pBlockProvider>();
				var cache = p.GetRequiredService<IMemoryCache>();
				return new SmartBlockProvider(provider, cache);
			});
			services.AddSingleton<TorStatusChecker>(p =>
			{
				HttpClientFactory httpClientFactory = p.GetRequiredService<HttpClientFactory>();
				return new TorStatusChecker(TimeSpan.FromHours(6), httpClientFactory.NewHttpClient(Mode.DefaultCircuit),
					new XmlIssueListParser());
			});
			services.AddSingleton<CachedBlockProvider>();
		});
		var host = hostBuilder.Build();
		host.Start();

		Logger.LogDebug($"Wasabi was started with these argument(s): {(args.Any() ? string.Join(" ", args) : "none") }.");

		Global = new Global();
		Services.Initialize(dataDirectory, host.Services);

		RxApp.DefaultExceptionHandler = Observer.Create<Exception>(ex =>
		{
			if (Debugger.IsAttached)
			{
				Debugger.Break();
			}

			Logger.LogError(ex);

			RxApp.MainThreadScheduler.Schedule(() => throw ex);
		});

		Logger.LogSoftwareStarted("Wasabi GUI");
		AppBuilder
			.Configure(() => new App(async () => await Global.InitializeNoWalletAsync(), runGuiInBackground)).UseReactiveUI()
			.SetupAppBuilder()
			.AfterSetup(_ =>
				{
					var glInterface = AvaloniaLocator.CurrentMutable.GetService<IPlatformOpenGlInterface>();
					Logger.LogInfo(glInterface is { }
						? $"Renderer: {glInterface.PrimaryContext.GlInterface.Renderer}"
						: "Renderer: Avalonia Software");

					ThemeHelper.ApplyTheme(Services.UiConfig.DarkModeEnabled ? Theme.Dark : Theme.Light);
				})
				.StartWithClassicDesktopLifetime(args);

		// Start termination/disposal of the application.
		AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

		Logger.LogSoftwareStopped("Wasabi");

		return 0;
	}

	public static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.ConfigureServices((hostContext, services) =>
			{
				//services.AddHostedService<Worker>();
			});

	private static void TerminateApplication()
	{
		MainViewModel.Instance.ClearStacks();
		MainViewModel.Instance.StatusIcon.Dispose();
	}

	private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		Logger.LogDebug(e.Exception);
	}

	private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}

	[SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Required to bootstrap Avalonia's Visual Previewer")]
	private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() => new App()).UseReactiveUI().SetupAppBuilder();

	/// <summary>
	/// Sets up and initializes the crash reporting UI.
	/// </summary>
	/// <param name="serializableException"></param>
	/// <param name="logPath"></param>
	/// <returns></returns>
	private static AppBuilder BuildCrashReporterApp(SerializableException serializableException)
	{
		var result = AppBuilder
			.Configure(() => new CrashReportApp(serializableException))
			.UseReactiveUI();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			result
				.UseWin32()
				.UseSkia();
		}
		else
		{
			result.UsePlatformDetect();
		}

		return result
			.With(new Win32PlatformOptions { AllowEglInitialization = false, UseDeferredRendering = true })
			.With(new X11PlatformOptions { UseGpu = false, WmClass = "Wasabi Wallet Crash Reporting" })
			.With(new AvaloniaNativePlatformOptions { UseDeferredRendering = true, UseGpu = false })
			.With(new MacOSPlatformOptions { ShowInDock = true })
			.AfterSetup(_ => ThemeHelper.ApplyTheme(Theme.Dark));
	}

	private static string Option2Config(string name) =>
		name switch
		{
			_ when name.EndsWith("Options") => name[..^"Options".Length],
			_ when name.EndsWith("Config") => name[..^"Config".Length],
			_ when name.EndsWith("Configuration") => name[..^"Configuration".Length],
			_ => name
		};
}
