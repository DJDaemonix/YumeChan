using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nodsoft.YumeChan.Core;
using System.CommandLine;
using Nodsoft.YumeChan.Common;

namespace Nodsoft.YumeChan.ConsoleRunner
{
	public static class Program
	{
		public static async Task Main(string[] args)
		{
			CommandLineHandler handler = new();
			handler.Startup += RunAsync;
			handler.Register();

			await handler.RootCommand.InvokeAsync(args);
		}

		public static async Task RunAsync(CommandLineArgsReturn parsedArgs)
		{
			IServiceCollection services = await ConfigureServices(new ServiceCollection());
			await YumeCore.ConfigureServices(services);

			YumeCore.Instance.Services = services.BuildServiceProvider();
			await YumeCore.Instance.InitServicesAsync();

			if (!string.IsNullOrWhiteSpace(parsedArgs.BotToken))
			{
				await YumeCore.Instance.SetBotToken(parsedArgs.BotToken);
			}

			await YumeCore.Instance.StartBotAsync().ConfigureAwait(true);
			await Task.Delay(-1);
		}

		public static Task<IServiceCollection> ConfigureServices(IServiceCollection services)
		{
			services.AddLogging();
			services.AddSingleton(LoggerFactory.Create(builder =>
			{
				builder.ClearProviders()
#if DEBUG
						.SetMinimumLevel(LogLevel.Trace)
#endif
						.AddConsole()
						.AddFilter("Microsoft", LogLevel.Warning)
						.AddFilter("System", LogLevel.Warning)
						.AddDebug();
			}));

			services.AddSingleton(YumeCore.Instance);

			return Task.FromResult(services);
		}
	}
}
