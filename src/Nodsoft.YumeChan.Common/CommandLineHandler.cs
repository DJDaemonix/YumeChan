using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text;
using System.Threading.Tasks;

namespace Nodsoft.YumeChan.Common
{
	public class CommandLineHandler
	{
		public delegate Task StartupDelegate(CommandLineArgsReturn parsedArgs);
		public StartupDelegate Startup { get; set; }

		public RootCommand RootCommand { get; set; } = new RootCommand
		{
			new Option<string>("--token", "Discord Bot Token to use.\nSee link https://discord.com/developers/applications to obtain a Token from your Bot.")
		};

		public void Register()
		{
			RootCommand.Handler = CommandHandler.Create<string>(async (token) =>
			{
				CommandLineArgsReturn parsedArgs = new();
				parsedArgs.BotToken = token;

				await Startup.Invoke(parsedArgs);
			});
		}
	}

	public struct CommandLineArgsReturn
	{
		public string BotToken { get; set; }
	}
}
