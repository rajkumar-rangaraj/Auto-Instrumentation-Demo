using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

internal class StartupHook
{
    internal static string OTelInsFolderLocation = GetOpenTelemetryInstrumentationPath();

    public static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += SharedHostPolicy.SharedAssemblyResolver.LoadAssemblyFromSharedLocation;
        var OtelInsFilePath = Path.Combine(OTelInsFolderLocation, "OpenTelemetryInstrumentation.dll");
        Assembly otelInsAssembly = Assembly.LoadFrom(OtelInsFilePath);
        
        Type type = otelInsAssembly.GetType("OpenTelemetryInstrumentation.TestConsoleExporter");
        var instance = Activator.CreateInstance(type);

        MethodInfo toInvoke = type.GetMethod("Enable");
        toInvoke.Invoke(instance, null);
    }

    private static string GetOpenTelemetryInstrumentationPath()
    {
        var startupAssemblyLocation = Assembly.GetExecutingAssembly().Location;
        var startupHookFolder = string.Concat(Path.DirectorySeparatorChar, "StartupHook", Path.DirectorySeparatorChar);
        var indexOfStartupHookFolder = startupAssemblyLocation.IndexOf(startupHookFolder, StringComparison.OrdinalIgnoreCase);
        var baseFolderLocation = startupAssemblyLocation.Substring(0, indexOfStartupHookFolder);
        var openTelemetryInstrumentationPath = Path.Combine(baseFolderLocation, "OpenTelemetryInstrumentation", "netcoreapp3.1");
        return openTelemetryInstrumentationPath;
    }
}

namespace SharedHostPolicy
{
    class SharedAssemblyResolver
    {
        public static Assembly LoadAssemblyFromSharedLocation(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            var sharedAssemblyPath = Path.Combine(StartupHook.OTelInsFolderLocation, $"{assemblyName.Name}.dll");
            if (File.Exists(sharedAssemblyPath))
            {
                return Assembly.LoadFrom(sharedAssemblyPath);
            }

            return null;
        }
    }
}
