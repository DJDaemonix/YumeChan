using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Nodsoft.YumeChan.PluginBase;
using Nodsoft.YumeChan.Core.TypeReaders;
using Nodsoft.YumeChan.Core.Config;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;

namespace Nodsoft.YumeChan.Core
{
	public enum YumeCoreState
	{
		Offline = 0, Online = 1, Starting = 2, Stopping = 3, Reloading = 4
	}

	public sealed class YumeCore
	{
		// Properties

		public static YumeCore Instance { get; } = new Lazy<YumeCore>(() => new()).Value;

		public YumeCoreState CoreState { get; private set; }

		public static Version CoreVersion { get; } = typeof(YumeCore).Assembly.GetName().Version;

		public DiscordSocketClient Client { get; set; }
		public CommandService Commands { get; set; }
		public IServiceProvider Services { get; set; }

		internal PluginsLoader ExternalModulesLoader { get; set; }
		public List<Plugin> Plugins { get; set; }

		public ILogger Logger { get; set; }

		internal ConfigurationProvider<ICoreProperties> ConfigProvider { get; private set; }
		public ICoreProperties CoreProperties { get; private set; }

		private string BotToken { get; set; }

		// Constructors
		static YumeCore() { /* Static ctor for Singleton implementation */ }


		// Destructor
		~YumeCore()
		{
			StopBotAsync().Wait();
		}


		// Methods

		public static Task<IServiceCollection> ConfigureServices() => ConfigureServices(new ServiceCollection());
		public static Task<IServiceCollection> ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<DiscordSocketClient>()
					.AddSingleton<CommandService>()
					.AddTransient(typeof(PluginBase.Tools.IConfigProvider<>), typeof(ConfigurationProvider<>))
					.AddLogging();

			return Task.FromResult(services);
		}

		public Task InitServicesAsync()
		{
			if (Services is null)
			{
				throw new InvalidOperationException("Service Provider has not been defined.", new ArgumentNullException("IServiceProvider Services"));
			}

			Client ??= Services.GetRequiredService<DiscordSocketClient>();
			Commands ??= Services.GetRequiredService<CommandService>();
			Logger ??= Services.GetRequiredService<ILoggerFactory>().CreateLogger<YumeCore>();
			ConfigProvider ??= Services.GetRequiredService<PluginBase.Tools.IConfigProvider<ICoreProperties>>() as ConfigurationProvider<ICoreProperties>;
			CoreProperties = ConfigProvider.InitConfig("coreconfig.json", true).PopulateCoreProperties();

			CoreProperties.Path_Core ??= Directory.GetCurrentDirectory();
			CoreProperties.Path_Plugins ??= CoreProperties.Path_Core + Path.DirectorySeparatorChar + "Plugins";
			CoreProperties.Path_Config ??= CoreProperties.Path_Core + Path.DirectorySeparatorChar + "Config";

			return Task.CompletedTask;
		}

		public async Task StartBotAsync()
		{
			CoreState = YumeCoreState.Starting;

			// Event Subscriptions
			Client.Log   += Logger.Log;
			Commands.Log += Logger.Log;

			Client.MessageReceived += HandleCommandAsync;


			await RegisterTypeReaders();
			await RegisterCommandsAsync().ConfigureAwait(false);

			await Client.LoginAsync(TokenType.Bot, BotToken ?? await GetBotTokenAsync());
			await Client.StartAsync();

			CoreState = YumeCoreState.Online;
		}

		public async Task StopBotAsync()
		{
			CoreState = YumeCoreState.Stopping;

			Client.MessageReceived -= HandleCommandAsync;

			await ReleaseCommandsAsync().ConfigureAwait(false);

			await Client.LogoutAsync();
			await Client.StopAsync();

			Client.Log   -= Logger.Log;
			Commands.Log -= Logger.Log;

			CoreState = YumeCoreState.Offline;
		}

		public async Task RestartBotAsync()
		{
			// Stop Bot
			await StopBotAsync().ConfigureAwait(true);

			// Start Bot
			await StartBotAsync().ConfigureAwait(false);
		}

		public Task RegisterTypeReaders()
		{	
			Commands.AddTypeReader(typeof(IEmote), new EmoteTypeReader());

			return Task.CompletedTask;
		}

		public async Task RegisterCommandsAsync()
		{
			ExternalModulesLoader ??= new PluginsLoader(string.Empty);
			Plugins ??= new List<Plugin> { new Modules.InternalPlugin() };				// Add YumeCore internal commands

			await ExternalModulesLoader.LoadPluginAssemblies();

			List<Plugin> pluginsFromLoader = await ExternalModulesLoader.LoadPluginManifests();
			pluginsFromLoader.RemoveAll(plugin => plugin is null);

			Plugins.AddRange(from Plugin plugin
							 in pluginsFromLoader
							 where !Plugins.Exists(_plugin => _plugin.PluginAssemblyName == plugin.PluginAssemblyName)
							 select plugin);

			foreach (Plugin plugin in new List<Plugin>(Plugins))
			{
				await plugin.LoadPlugin();
				await Commands.AddModulesAsync(plugin.GetType().Assembly, Services);

				if (plugin is IMessageTap tap)
				{
					Client.MessageReceived	+= tap.OnMessageReceived;
					Client.MessageUpdated	+= tap.OnMessageUpdated;
					Client.MessageDeleted	+= tap.OnMessageDeleted;
				}
			}

			await Commands.AddModulesAsync(Assembly.GetEntryAssembly(), Services);      // Add possible Commands from Entry Assembly (contextual)
		}

		public async Task ReleaseCommandsAsync()
		{
			foreach (ModuleInfo module in new List<ModuleInfo>(Commands.Modules))
			{
				if (module is not Modules.ICoreModule)
				{
					await Commands.RemoveModuleAsync(module).ConfigureAwait(false);
				}
			}


			foreach (Plugin plugin in new List<Plugin>(Plugins))
			{
				if (plugin is not Modules.InternalPlugin)
				{
					if (plugin is IMessageTap tap)
					{
						Client.MessageReceived -= tap.OnMessageReceived;
						Client.MessageUpdated -= tap.OnMessageUpdated;
						Client.MessageDeleted -= tap.OnMessageDeleted;
					}

					await plugin.UnloadPlugin();
					Plugins.Remove(plugin);
				}
			}
		}

		public async Task ReloadCommandsAsync()
		{
			CoreState = YumeCoreState.Reloading;

			await ReleaseCommandsAsync().ConfigureAwait(true);

			await RegisterCommandsAsync().ConfigureAwait(false);

			CoreState = YumeCoreState.Online;
		}

		private async Task HandleCommandAsync(SocketMessage arg)
		{
			if (arg is SocketUserMessage message && !message.Author.IsBot)
			{
				int argPosition = 0;

				if (message.HasStringPrefix(CoreProperties.CommandPrefix, ref argPosition) || message.HasMentionPrefix(Client.CurrentUser, ref argPosition))
				{
					await Logger.Log(new LogMessage(LogSeverity.Verbose, "Commands", $"Command \"{message.Content}\" received from User {message.Author.Mention}."));

					SocketCommandContext context = new SocketCommandContext(Client, message);
					IResult result = await Commands.ExecuteAsync(context, argPosition, Services).ConfigureAwait(false);

					if (!result.IsSuccess)
					{
						await context.Channel.SendMessageAsync($"{context.User.Mention} {result.ErrorReason}").ConfigureAwait(false);

						LogMessage logMessage = new(LogSeverity.Verbose, new StackTrace().GetFrame(1).GetMethod().Name,
							$"{context.User.Mention} : {message} \n{result}");

						await Logger.Log(logMessage).ConfigureAwait(false);
					}
				}
			}
		}

		private async Task<string> GetBotTokenAsync()
		{
			string token = CoreProperties.BotToken;

			if (string.IsNullOrWhiteSpace(token))
			{
				string envVarName = $"{CoreProperties.AppInternalName}.Token";

				if (await TryBotTokenFromEnvironment(envVarName, out token, out EnvironmentVariableTarget target))
				{
					Logger.LogInformation($"Bot Token was read from {target} Environment Variable \"{envVarName}\", instead of \"coreproperties.json\" Config File.");
				}
				else
				{
					ApplicationException e = new ApplicationException("No Bot Token supplied.");
					Logger.LogCritical(e, $"No Bot Token was found in \"coreproperties.json\" Config File, and Environment Variables \"{envVarName}\" from relevant targets are empty. " +
											$"\nPlease set a Bot Token before launching the Bot.");
					throw e;
				}
			}

			return token;
		}

		private static Task<bool> TryBotTokenFromEnvironment(string envVarName, out string token, out EnvironmentVariableTarget foundFromTarget)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				foreach (EnvironmentVariableTarget target in typeof(EnvironmentVariableTarget).GetEnumValues())
				{
					token = Environment.GetEnvironmentVariable(envVarName, target);

					if (token is not null)
					{
						foundFromTarget = target;
						return Task.FromResult(true);
					}
				}

				token = null;
				foundFromTarget = default;
				return Task.FromResult(false);
			}
			else
			{
				token = Environment.GetEnvironmentVariable(envVarName);

				foundFromTarget = EnvironmentVariableTarget.Process;
				return Task.FromResult(token is not null);
			}
		}

		public Task SetBotToken(string token)
		{
			if (Instance.CoreState is 0)
			{
				BotToken = token;
				string msg = $"Token Set Externally";
#if DEBUG
				msg += $" : {token}";
#endif

				Logger.LogInformation(msg);
				return Task.CompletedTask;
			}
#if DEBUG
			else
			{
				throw new ApplicationException($"Bot is currently running (state : {CoreState}), cannot change Token.");
			}
#endif
		}
	}
}