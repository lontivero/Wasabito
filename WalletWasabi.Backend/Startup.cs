using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Controllers.WabiSabi;
using WalletWasabi.Backend.Middlewares;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WebClients;

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

		services.AddMvc(options => options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(BitcoinAddress))))
			.AddControllersAsServices();

		services.AddMvc()
			.AddNewtonsoftJson();

		services.AddControllers().AddNewtonsoftJson(x => x.SerializerSettings.Converters = JsonSerializationOptions.Default.Settings.Converters);

		services.AddLogging(logging => logging.AddFilter((s, level) => level >= Microsoft.Extensions.Logging.LogLevel.Warning));

		services.AddSingleton<IExchangeRateProvider>(new ExchangeRateProvider());
		services.AddSingleton<IdempotencyRequestCache>();
#pragma warning disable CA2000 // Dispose objects before losing scope, reason: https://github.com/dotnet/roslyn-analyzers/issues/3836
		services.AddSingleton(new Global(Configuration["datadir"]));
#pragma warning restore CA2000 // Dispose objects before losing scope
		services.AddSingleton(serviceProvider =>
		{
			var global = serviceProvider.GetRequiredService<Global>();
			var coordinator = global.HostedServices.Get<WabiSabiCoordinator>();
			return coordinator.Arena;
		});
		services.AddSingleton(serviceProvider =>
		{
			var global = serviceProvider.GetRequiredService<Global>();
			var coordinator = global.HostedServices.Get<WabiSabiCoordinator>();
			return coordinator.CoinJoinFeeRateStatStore;
		});
		services.AddStartupTask<InitConfigStartupTask>();

		services.AddResponseCompression();
	}

	[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "This method gets called by the runtime. Use this method to configure the HTTP request pipeline")]
	public void Configure(IApplicationBuilder app, IWebHostEnvironment env, Global global)
	{
		app.UseStaticFiles();

		app.UseRouting();

		// So to correctly handle HEAD requests.
		// https://www.tpeczek.com/2017/10/exploring-head-method-behavior-in.html
		// https://github.com/tpeczek/Demo.AspNetCore.Mvc.CosmosDB/blob/master/Demo.AspNetCore.Mvc.CosmosDB/Middlewares/HeadMethodMiddleware.cs
		app.UseMiddleware<HeadMethodMiddleware>();

		app.UseResponseCompression();

		app.UseEndpoints(endpoints => endpoints.MapControllers());

		var applicationLifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
		applicationLifetime.ApplicationStopped.Register(() => OnShutdown(global)); // Don't register async, that won't hold up the shutdown
	}

	private void OnShutdown(Global global)
	{
		global.Dispose();
		Logger.LogSoftwareStopped("Wasabi Backend");
	}
}
