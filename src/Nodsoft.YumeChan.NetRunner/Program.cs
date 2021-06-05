using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Nodsoft.YumeChan.Core;
using Lamar.Microsoft.DependencyInjection;
using Lamar;
using Serilog;
using Serilog.Events;



namespace Nodsoft.YumeChan.NetRunner
{
	public static class Program
	{
		public static async Task Main(string[] args)
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
				.Enrich.FromLogContext()
				.WriteTo.Console()
				.CreateLogger();


			IHost host = CreateHostBuilder(args).Build();

			YumeCore.Instance.Services = host.Services as Container;

			await YumeCore.Instance.StartBotAsync();
			await host.RunAsync();
		}
		public static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
				.UseLamar()
				.ConfigureLogging(builder =>
				{
					builder.AddSerilog(Log.Logger);
				})
				.ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
				.UseSerilog();
		}
	}
}
