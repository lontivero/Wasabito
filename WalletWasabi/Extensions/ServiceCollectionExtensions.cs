using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WalletWasabi.Services;

namespace WalletWasabi.Extensions;

public static class IServiceCollectionExtensions
{
	public static IServiceCollection AddBackgroundService<TService>(this IServiceCollection services)
		where TService : class, IHostedService =>
		services.AddSingleton<TService>().AddHostedService<BackgroundServiceStarter<TService>>();

	public static IServiceCollection AddBackgroundService<TService, TServiceImpl>(this IServiceCollection services)
		where TServiceImpl : class, IHostedService, TService where TService : class =>
		services.AddSingleton<TService, TServiceImpl>().AddHostedService<BackgroundServiceStarter<TServiceImpl>>();
}

public static class IServiceCollectionExtensionsForConfigurations
{
	private const string DefaultFileName = "config.json";
	public static IServiceCollection ConfigureWritable<TOptions>(this IServiceCollection services, IConfiguration configuration, string file = DefaultFileName) where TOptions : class, new()
	{
		var sectionKey = Option2Config(typeof(TOptions).Name);
		services.Configure<TOptions>(configuration.GetSection(sectionKey));
		services.AddTransient<IWritableOptions<TOptions>>(provider =>
		{
			var environment = provider.GetService<IHostEnvironment>();
			string jsonFilePath = environment?.ContentRootFileProvider.GetFileInfo(file).PhysicalPath
			                      ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);

			var options = provider.GetRequiredService<IOptionsMonitor<TOptions>>();			
			return new WritableOptions<TOptions>(options, configuration, sectionKey, jsonFilePath);
		});
		return services;
	}

	public static string Option2Config(string name) =>
		name switch
		{
			_ when name.EndsWith("Options") => name[..^"Options".Length],
			_ when name.EndsWith("Config") => name[..^"Config".Length],
			_ when name.EndsWith("Configuration") => name[..^"Configuration".Length],
			_ => name
		};
	
}
