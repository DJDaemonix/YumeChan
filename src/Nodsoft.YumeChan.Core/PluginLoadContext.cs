using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace Nodsoft.YumeChan.Core
{
    class PluginLoadContext : AssemblyLoadContext
    {
        public FileInfo File { get; init; }

        private readonly AssemblyDependencyResolver resolver;

        public PluginLoadContext(FileInfo file)
        {
            File = file;
            resolver = new AssemblyDependencyResolver(file.FullName);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
            
            if (assemblyPath is not null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            
            if (libraryPath is not null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
