using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using OpenTelemetry.StartupHookDemo;
using OpenTelemetry.StartupHookDemo.Adaptor;

internal class StartupHook
{
    internal static string OTelInsFolderLocation = GetOpenTelemetryInstrumentationPath();

    public static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += SharedAssemblyResolver.LoadAssemblyFromSharedLocation;

        // Load System.Diagnostics.DiagnosticSource for the framework to pick the correct version from TPL.
        // This bring the System.Diagnostics.DiagnosticSource used by this application.
        var appDiagnosticSourceAssembly = Assembly.Load(new AssemblyName("System.Diagnostics.DiagnosticSource, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"));
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        // Add OpenTelemetry SDK.
        var OtelInsFilePath = Path.Combine(OTelInsFolderLocation, "OpenTelemetryInstrumentation.dll");
        Assembly otelInsAssembly = Assembly.LoadFile(OtelInsFilePath);
        
        Type type = otelInsAssembly.GetType("OpenTelemetryInstrumentation.TestConsoleExporter");
        var instance = Activator.CreateInstance(type);

        // Enable OpenTelemety Instrumentation.
        MethodInfo toInvoke = type.GetMethod("Enable");
        toInvoke.Invoke(instance, null);

        // Add DiagnosticSource adaptors when app has lower version of DiagnosticSource when compared with OpenTelemetry DiagnosticSource.
        if (SharedAssemblyResolver.oTelDiagnosticSourceAssembly?.GetName().Version > appDiagnosticSourceAssembly.GetName().Version)
        {
            var aspnetSubscriber = new DiagnosticSourceSubscriber(new AspnetCoreDiagnosticSourceListener(), null);
            aspnetSubscriber.Subscribe();

            var httpClientSubscriber = new DiagnosticSourceSubscriber(new HttpClientDiagnosticSourceListener(), null);
            httpClientSubscriber.Subscribe();
        }
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

namespace OpenTelemetry.StartupHookDemo
{
    class SharedAssemblyResolver
    {
        internal static Assembly oTelDiagnosticSourceAssembly = null;

        public static Assembly LoadAssemblyFromSharedLocation(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            var sharedAssemblyPath = Path.Combine(StartupHook.OTelInsFolderLocation, $"{assemblyName.Name}.dll");
            if (File.Exists(sharedAssemblyPath))
            {
                var assembly = Assembly.LoadFile(sharedAssemblyPath);
                if(assemblyName.Name == "System.Diagnostics.DiagnosticSource")
                {
                    oTelDiagnosticSourceAssembly = assembly;
                }
                return assembly;
            }

            return null;
        }
    }
}
