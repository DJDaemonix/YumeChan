using Microsoft.Extensions.Logging;
using YumeChan.Core.Config;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using static DSharpPlus.Entities.DiscordEmbedBuilder;

namespace YumeChan.Core
{
	public static class Utilities
	{
		public static EmbedFooter DefaultCoreFooter { get; } = new EmbedFooter()
		{
			Text = $"{YumeCore.Instance.CoreProperties.AppDisplayName} v{YumeCore.CoreVersion} - Powered by Nodsoft Systems"
		};

		public static bool ImplementsInterface(this Type type, Type interfaceType) => type.GetInterfaces().Where(t => t == interfaceType).Select(t => new { }).Any();

		public static ICoreProperties PopulateCoreProperties(this ICoreProperties properties)
		{
			properties.AppInternalName ??= "YumeChan";
			properties.AppDisplayName ??= "Yume-Chan";
			properties.BotToken ??= string.Empty;
			properties.CommandPrefix ??= "==";
			properties.DatabaseProperties.ConnectionString ??= "mongodb://localhost:27017";
			properties.DatabaseProperties.DatabaseName ??= "yc-default";

			return properties;
		}
	}
}