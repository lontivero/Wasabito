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
	public static IServiceCollection AddBackgroundService<TService>(this IServiceCollection services) where TService : class, IHostedService =>
		services.AddSingleton<TService>().AddHostedService<BackgroundServiceStarter<TService>>();

	public static IServiceCollection AddBackgroundService<TService, TServiceImpl>(this IServiceCollection services) where TServiceImpl : class, IHostedService, TService where TService : class =>
		services.AddSingleton<TService, TServiceImpl>().AddHostedService<BackgroundServiceStarter<TServiceImpl>>();
	
	public static IServiceCollection ConfigureWritable<T>(this IServiceCollection services, IConfigurationSection section, string file = "appsettings.json") where T : class, new()
	{
		services.Configure<T>(section);
		services.AddTransient<IWritableOptions<T>>(provider =>
		{
			var configuration = (IConfigurationRoot)provider.GetRequiredService<IConfiguration>();
			var options = provider.GetRequiredService<IOptionsMonitor<T>>();
			return new WritableOptions<T>(options, configuration, section.Key, file);
		});
		return services;
	}
}
