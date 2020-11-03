using System;
using System.CommandLine;
using System.CommandLine.DragonFruit;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nodsoft.YumeChan.Common;
using Nodsoft.YumeChan.Core;

using static Nodsoft.YumeChan.NetRunner.Properties.AppProperties;


namespace Nodsoft.YumeChan.NetRunner
{
	public static class Program
	{
		private static string[] sharedArgs;

		public static async Task Main(string[] args)
		{
			sharedArgs = args;

			CommandLineHandler handler = new();
			handler.Startup += RunAsync;
			handler.Register();

			await handler.RootCommand.InvokeAsync(args);
		}

		public static async Task RunAsync(CommandLineArgsReturn parsedArgs)
		{
			IHost host = CreateHostBuilder(sharedArgs).Build();

			YumeCore.Instance.Services = host.Services;
			await YumeCore.Instance.InitServicesAsync();

			if (!string.IsNullOrWhiteSpace(parsedArgs.BotToken))
			{
				await YumeCore.Instance.SetBotToken(parsedArgs.BotToken);
			}

			await YumeCore.Instance.StartBotAsync();
			await host.RunAsync();
		}

		public static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host	.CreateDefaultBuilder(args)
						.ConfigureLogging(builder =>
						{
							builder.ClearProviders()
									.AddConsole()
									.AddFilter("Microsoft", LogLevel.Warning)
									.AddFilter("System", LogLevel.Warning)
									.AddDebug();
						})
						.ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
		}
	}
}
