using System.Threading.Tasks;
using Lamar;
using Lamar.Microsoft.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nodsoft.YumeChan.Core;
using Serilog;
using Serilog.Events;

namespace Nodsoft.YumeChan.ConsoleRunner
{
	public static class Program
	{
		public static async Task Main(string[] _)
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
				.Enrich.FromLogContext()
				.WriteTo.Console()
				.CreateLogger();

			IHost host = CreateHostBuilder(new ServiceRegistry()).Build();
			YumeCore.Instance.Services = host.Services as Container;

			await YumeCore.Instance.StartBotAsync().ConfigureAwait(false);
			await host.RunAsync();
		}

		public static IHostBuilder CreateHostBuilder(ServiceRegistry serviceRegistry = null)
		{
			return new HostBuilder()
				.UseLamar(serviceRegistry ?? new())
				.ConfigureAppConfiguration(builder => {	})
				.ConfigureContainer<ServiceRegistry>((context, services) =>
				{
					YumeCore.Instance.ConfigureServices(services);
				})
				.ConfigureLogging(builder => 
				{
					builder.AddSerilog(Log.Logger);
				})
				.UseSerilog();
		}
	}
}
