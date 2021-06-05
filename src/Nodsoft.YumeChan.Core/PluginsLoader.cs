using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nodsoft.YumeChan.PluginBase;
using Microsoft.Extensions.Logging;
using System.Security;

namespace Nodsoft.YumeChan.Core
{
	internal class PluginsLoader
	{
		internal List<Assembly> PluginAssemblies { get; set; }
		internal List<FileInfo> PluginFiles { get; set; }
		internal List<Plugin> PluginManifests { get; set; }

		internal DirectoryInfo PluginsLoadDirectory { get; set; }
		internal string PluginsLoadDiscriminator { get; set; } = string.Empty;

		public PluginsLoader(string pluginsLoadDirectoryPath)
		{
			PluginsLoadDirectory = string.IsNullOrEmpty(pluginsLoadDirectoryPath)
				? SetDefaultPluginsDirectoryEnvironmentVariable()
				: Directory.Exists(pluginsLoadDirectoryPath)
					? new DirectoryInfo(pluginsLoadDirectoryPath)
					: Directory.CreateDirectory(pluginsLoadDirectoryPath);
		}

		private DirectoryInfo SetDefaultPluginsDirectoryEnvironmentVariable()
		{
			FileInfo file = new(Assembly.GetExecutingAssembly().Location);
			PluginsLoadDirectory = Directory.CreateDirectory(file.DirectoryName + Path.DirectorySeparatorChar + "Plugins" + Path.DirectorySeparatorChar);

			try
			{
				Environment.SetEnvironmentVariable("YumeChan.PluginsLocation", PluginsLoadDirectory.FullName);
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Environment.SetEnvironmentVariable("YumeChan.PluginsLocation", PluginsLoadDirectory.FullName, EnvironmentVariableTarget.User);
				}
			}
			catch (SecurityException e)
			{
				YumeCore.Instance.Logger.Log(LogLevel.Warning, e, "Failed to write Environment Variable \"YumeChan.PluginsLocation\".");
			}
			return PluginsLoadDirectory;
		}

		public void LoadPluginAssemblies()
		{
			PluginFiles = new List<FileInfo>(PluginsLoadDirectory.GetFiles($"*{PluginsLoadDiscriminator}*.dll"));
			PluginAssemblies ??= new List<Assembly>();

			foreach (FileInfo file in PluginFiles)
			{
				if (file is not null || file.Name != Path.GetFileName(typeof(Plugin).Assembly.Location))
				{
					PluginLoadContext loadContext = new(file);
					PluginAssemblies.Add(loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(loadContext.File.FullName))));
				}
			}
		}

		public List<Plugin> LoadPluginManifests()
		{
			List<Plugin> plugins = new();

			foreach (Assembly a in PluginAssemblies)
			{
				foreach (Type t in a.ExportedTypes)
				{
					if (t.BaseType.FullName == typeof(Plugin).FullName)
					{
						plugins.Add(InstantiateManifest(t));
						break;
					}
				}
			}

			return plugins;
		}

		internal static Plugin InstantiateManifest(Type typePlugin)
		{
			object instance = ActivatorUtilities.CreateInstance(YumeCore.Instance.Services, typePlugin);
			return instance as Plugin;
		}
	}
}
