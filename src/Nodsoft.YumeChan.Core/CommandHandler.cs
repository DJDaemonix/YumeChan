using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using Lamar;
using Microsoft.Extensions.Logging;
using Nodsoft.YumeChan.Core.Config;
using Nodsoft.YumeChan.Core.Services.Formatters;
using Nodsoft.YumeChan.PluginBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;



namespace Nodsoft.YumeChan.Core
{
	public class CommandHandler
	{
		public CommandsNextExtension Commands { get; internal set; }
		public CommandsNextConfiguration CommandsConfiguration { get; internal set; }

		public List<Plugin> Plugins { get; internal set; }

		internal ICoreProperties Config { get; set; }

		private readonly DiscordClient client;
		private readonly IServiceProvider services;
		private readonly ServiceRegistry registry;
		private readonly ILogger logger;
		private readonly PluginsLoader externalModulesLoader;


		public CommandHandler(DiscordClient client, ILogger<CommandHandler> logger, IServiceProvider services, ServiceRegistry registry)
		{
			this.client = client;
			this.services = services;
			this.registry = registry;
			this.logger = logger;

			externalModulesLoader = new(string.Empty);
		}


		public async Task InstallCommandsAsync()
		{
			CommandsConfiguration = new()
			{
				Services = services,
				StringPrefixes = new[] { Config.CommandPrefix }
			};

			Commands = client.UseCommandsNext(CommandsConfiguration);
			Commands.CommandErrored += OnCommandErroredAsync;
			Commands.CommandExecuted += OnCommandExecuted;

//			Commands.CommandExecuted += OnCommandExecutedAsync; // Hook execution event
//			client.MessageReceived += HandleCommandAsync; // Hook command handler

			await RegisterCommandsAsync();

			Commands.SetHelpFormatter<HelpCommandFormatter>();
		}

		public async Task UninstallCommandsAsync()
		{
//			Commands.CommandExecuted -= OnCommandExecutedAsync;
//			client.MessageReceived -= HandleCommandAsync;

			await ReleaseCommandsAsync();
		}


/*		public void RegisterTypeReaders()
		{
			
		}
*/

		public async Task RegisterCommandsAsync()
		{
			Plugins = new() { new Modules.InternalPlugin() }; // Add YumeCore internal commands
			externalModulesLoader.LoadPluginAssemblies();

			IEnumerable<Plugin> pluginManifests = externalModulesLoader.LoadPluginManifests();

			IEnumerable<Plugin> collection()
			{
				foreach (Plugin plugin in pluginManifests)
				{
					if (!Plugins.Exists(p => p?.PluginAssemblyName == plugin.PluginAssemblyName))
					{
						yield return plugin;
					}
				}
			}

			Plugins.AddRange(collection());

			foreach (Plugin plugin in Plugins)
			{
				plugin.ConfigureServices(registry);
				await plugin.LoadPlugin();
				Commands.RegisterCommands(plugin.GetType().Assembly);
				logger.LogInformation("Loaded Plugin '{Plugin}'.", plugin.PluginAssemblyName);
			}

			Commands.RegisterCommands(Assembly.GetEntryAssembly()); // Add possible Commands from Entry Assembly (contextual)
		}


		public async Task ReleaseCommandsAsync()
		{
			Commands.UnregisterCommands(Commands.RegisteredCommands.Values.ToArray());

			foreach (Plugin plugin in new List<Plugin>(Plugins.Where(p => p is not Modules.InternalPlugin)))
			{
				await plugin.UnloadPlugin();
				Plugins.Remove(plugin);

				logger.LogInformation("Removed Plugin '{Plugin}'.", plugin.PluginAssemblyName);
			}
		}

		internal async Task OnCommandErroredAsync(CommandsNextExtension _, CommandErrorEventArgs e)
		{
			if (e.Exception is ChecksFailedException cf)
			{
				foreach (CheckBaseAttribute check in cf.FailedChecks)
				{
					string message = check switch
					{
						RequireOwnerAttribute => $"Sorry. You must be a Bot Owner to run this command.",
						RequireDirectMessageAttribute => "Sorry, not here. Please send me a Direct Message with that command.",
						RequireGuildAttribute => "Sorry, not here. Please send this command in a server.",
						RequireNsfwAttribute => "Sorry. As much as I'd love to, I've gotta keep the hot stuff to the right channels.",
						CooldownAttribute cd => $"Sorry. This command is on Cooldown. You can use it {cd.MaxUses} time(s) every {cd.Reset.TotalSeconds} seconds.",
						RequireUserPermissionsAttribute p => $"Sorry. You need to have permission(s) ``{p.Permissions}`` to run this.",
						_ => null
					};

					if (message is not null)
					{
						await e.Context.RespondAsync(message);
						return;
					}
				}
			}




#if DEBUG
			string response = $"An error occurred : \n```{e.Exception}```";
#else
			string response = $"Something went wrong while executing your command : \n\n{e.Exception.Message}";
#endif

			await e.Context.RespondAsync(response);
			logger.LogError("An error occured executing '{0}' from user '{1}' : \n{2}", e.Command.QualifiedName, e.Context.User.Id, e.Exception);
		}




		/*	private async Task HandleCommandAsync(SocketMessage arg)
				{
					if (arg is SocketUserMessage message)
					{
						int argPosition = 0;

						if (message.HasStringPrefix(Config.CommandPrefix, ref argPosition) || message.HasMentionPrefix(client.CurrentUser, ref argPosition))
						{
							SocketCommandContext context = new(client, message);

							await Commands.ExecuteAsync(context, argPosition, services).ConfigureAwait(false);
						}
					}
				}
		*/

		public Task OnCommandExecuted(CommandsNextExtension sender, CommandExecutionEventArgs e)
		{
			logger.LogDebug("Command '{0}' received from User '{1}'.", e.Command.QualifiedName, e.Context.User.Id);
			return Task.CompletedTask;
		}



		/*		public async Task LogAsync(LogMessage logMessage)
				{
					if (logMessage.Exception is CommandException cmdException)
					{
						// Inform the user that something unexpected has happened
		#if DEBUG
						await cmdException.Context.Channel.SendMessageAsync(cmdException.ToString());
		#else
						await cmdException.Context.Channel.SendMessageAsync("Something went wrong.");
		#endif

						// Log the incident
						await logger.Log(new LogMessage(LogSeverity.Error, "Commands", $"{cmdException.Context.User} failed to execute '{cmdException.Command.Name}' in channel {cmdException.Context.Channel}.", cmdException));
					}
				}
		*/
	}
}
