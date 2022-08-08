using System.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Extensions.Options;
using NBitcoin.RPC;
using WalletWasabi.Backend.Controllers.WabiSabi;
using WalletWasabi.Backend.Middlewares;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Extensions;
using WalletWasabi.TypeConverters;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Models.Serialization;

[assembly: ApiController]


namespace WalletWasabi.Backend;


public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	// This method gets called by the runtime. Use this method to add services to the container.
	public void ConfigureServices(IServiceCollection services)
	{
		services.AddMemoryCache();
		services.AddLogging();

		services.AddMvc()
			.AddControllersAsServices()
			.AddNewtonsoftJson();

		services.AddControllers()
			.AddNewtonsoftJson(x => x.SerializerSettings.Converters = JsonSerializationOptions.Default.Settings.Converters);

		services.AddOptions();
		TypeDescriptor.AddAttributes(typeof(Money),new TypeConverterAttribute(typeof(MoneyConverter)));
		TypeDescriptor.AddAttributes(typeof(ExtPubKey),new TypeConverterAttribute(typeof(ExtPubKeyConverter)));
		TypeDescriptor.AddAttributes(typeof(Uri),new TypeConverterAttribute(typeof(UriConverter)));
		TypeDescriptor.AddAttributes(typeof(EndPoint),new TypeConverterAttribute(typeof(EndPointConverter)));

		services.AddLogging(logging => logging.AddFilter((s, level) => level >= Microsoft.Extensions.Logging.LogLevel.Warning));

		var network = Network.GetNetwork(Configuration["Network"])
		              ?? throw new ArgumentException("Unsupported Network.");

		// Configurations
		services.AddSingleton(_ => network);
		services.Configure<DoSOptions>(Configuration.GetSection(key: Option2Config(nameof(DoSOptions))));
		services.Configure<RpcOptions>(Configuration.GetSection(key: Option2Config(nameof(RpcOptions))));
		services.Configure<BitcoinOptions>(Configuration.GetSection(key: Option2Config(nameof(BitcoinOptions))));
		services.Configure<WabiSabiConfig>(Configuration.GetSection(Option2Config(nameof(WabiSabiConfig))));
		services.AddSingleton(provider =>
		{
			var cfg = provider.GetRequiredService<IOptions<RpcOptions>>();
			return new RPCClient(cfg.Value.AuthenticationString, cfg.Value.Host, network);
		});

		// Services
		services.AddSingleton<IRPCClient, CachedRpcClient>();
		services.AddSingleton<Prison>();

		services.AddSingleton<Warden>();
		services.AddSingleton<InMemoryCoinJoinIdStore>();
		services.AddSingleton<CoinJoinFeeRateStatStore>();
		services.AddSingleton<RoundParameterFactory>();

		services.AddBackgroundService<Arena>();
		services.AddBackgroundService<BlockNotifier>();

		services.AddSingleton<MempoolService>();
		services.AddScoped(typeof(TimeSpan), _ => TimeSpan.FromSeconds(2));

		services.AddSingleton<IdempotencyRequestCache>();
		services.AddSingleton<IndexBuilderService>();
		services.AddStartupTask<StartupTask>();

		services.AddResponseCompression();
	}

	[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "This method gets called by the runtime. Use this method to configure the HTTP request pipeline")]
	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		app.UseRouting();

		// So to correctly handle HEAD requests.
		// https://www.tpeczek.com/2017/10/exploring-head-method-behavior-in.html
		// https://github.com/tpeczek/Demo.AspNetCore.Mvc.CosmosDB/blob/master/Demo.AspNetCore.Mvc.CosmosDB/Middlewares/HeadMethodMiddleware.cs
		app.UseMiddleware<HeadMethodMiddleware>();

		app.UseResponseCompression();

		app.UseEndpoints(endpoints => endpoints.MapControllers());
	}

	private string Option2Config(string name) =>
		name switch
		{
			_ when name.EndsWith("Options") => name[..^"Options".Length],
			_ when name.EndsWith("Config") => name[..^"Config".Length],
			_ => name
		};
}
