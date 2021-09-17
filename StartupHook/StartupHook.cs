using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Utils;

internal class StartupHook
{
    internal static string OTelInsFolderLocation = GetOpenTelemetryInstrumentationPath();
    private static Assembly s_diagnosticSourceAssembly = null;
    public const string DiagnosticSourceAssembly_Name = "System.Diagnostics.DiagnosticSource";
    public const string DiagnosticSourceAssembly_Version = "Version=6.0.0.0";
    public const string DiagnosticSourceAssembly_Culture = "Culture=neutral";
    public const string DiagnosticSourceAssembly_PublicKeyToken = "PublicKeyToken=cc7b13ffcd2ddd51";

    // Assembly System.Diagnostics.DiagnosticSource version 4.0.2.0 is the first official version of that assembly that contains Activity.
    // (Previous versions contained DiagnosticSource only.)
    // That version was shipped in the System.Diagnostics.DiagnosticSource NuGet version 4.4.0 on 2017-06-28.
    // See https://www.nuget.org/packages/System.Diagnostics.DiagnosticSource/4.4.0
    // It is highly unlikey that an application references an older version of DiagnosticSource.
    // However, if it does, we will not instrument it.
    public static readonly Version DiagnosticSourceAssembly_MinReqVersion = new Version(4, 0, 2, 0);

    public static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += SharedHostPolicy.SharedAssemblyResolver.LoadAssemblyFromSharedLocation;
        LoadDiagnosticSourceAssembly();
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

    private static Assembly LoadDiagnosticSourceAssembly()
    {
        // See if DiagnosticSource.dll is already loaded and known to this loader:

        Assembly diagnosticSourceAssembly = s_diagnosticSourceAssembly;
        if (diagnosticSourceAssembly != null)
        {
            return diagnosticSourceAssembly;
        }

        Console.WriteLine($"Looking for the \"{DiagnosticSourceAssembly_Name}\" assembly.");

        // Perhaps DiagnosticSource.dll is not yet known to this loader, but it has already been loaded by the application.
        // Let's look for it:

        IEnumerable<Assembly> loadedAssemblies = AssemblyLoadContext.Default.Assemblies;

        if (loadedAssemblies != null)
        {
            foreach (Assembly loadedAssembly in loadedAssemblies)
            {
                string loadedAssemblyName = loadedAssembly.FullName;
                if (loadedAssemblyName.StartsWith(DiagnosticSourceAssembly_Name, StringComparison.OrdinalIgnoreCase)
                        && loadedAssemblyName.Contains(DiagnosticSourceAssembly_PublicKeyToken))
                {
                    if (diagnosticSourceAssembly != null)
                    {
                        throw new InvalidOperationException($"The assembly \"{DiagnosticSourceAssembly_Name}\" is loaded at least twice."
                                                          + " This is an unsupported condition."
                                                          + $" First instance: [FullName={Format.QuoteOrSpellNull(diagnosticSourceAssembly.FullName)},"
                                                          + $" Location={Format.QuoteOrSpellNull(diagnosticSourceAssembly.Location)}];"
                                                          + $" Second instance: [FullName={Format.QuoteOrSpellNull(loadedAssembly.FullName)},"
                                                          + $" Location={Format.QuoteOrSpellNull(loadedAssembly.Location)}];");
                    }

                    diagnosticSourceAssembly = loadedAssembly;
                }
            }
        }

        // DiagnosticSource.dll may be loaded, but not into the default AssemblyLoadContext. That is not supported.
        // Let's verify:

        foreach (AssemblyLoadContext asmLdCtx in AssemblyLoadContext.All)
        {
            if (Object.ReferenceEquals(asmLdCtx, AssemblyLoadContext.Default))
            {
                continue;
            }

            foreach (Assembly loadedAssembly in asmLdCtx.Assemblies)
            {
                string loadedAssemblyName = loadedAssembly.FullName;
                if (loadedAssemblyName.StartsWith(DiagnosticSourceAssembly_Name, StringComparison.OrdinalIgnoreCase)
                        && loadedAssemblyName.Contains(DiagnosticSourceAssembly_PublicKeyToken))
                {
                    throw new InvalidOperationException($"The assembly \"{DiagnosticSourceAssembly_Name}\" is loaded into a non-default AssemblyLoadContext."
                                                      + " This is an unsupported condition."
                                                      + $" AssemblyLoadContext.Name={Format.QuoteOrSpellNull(asmLdCtx.Name)};"
                                                      + $" Loaded assembly: [FullName={Format.QuoteOrSpellNull(loadedAssembly.FullName)},"
                                                      + $" Location={Format.QuoteOrSpellNull(loadedAssembly.Location)}];");
                }
            }
        }

        // If we found DiagnosticSource.dll already loaded, we are done:

        if (diagnosticSourceAssembly != null)
        {
            AssemblyName asmName = diagnosticSourceAssembly.GetName();
            if (asmName.Version < DiagnosticSourceAssembly_MinReqVersion)
            {
                Console.WriteLine($"The \"{DiagnosticSourceAssembly_Name}\" assembly is already loaded by the application and the version is too old:"
                   + $" Minimum required version: \"{DiagnosticSourceAssembly_MinReqVersion}\";"
                   + $" Actually loaded assembly: {{Version=\"{asmName.Version}\", Location=\"{diagnosticSourceAssembly.Location}\"}}."
                   + " Replacing a readily loaded assembly is not supported. Activity-based auto-instrumentation cannot be performed.");

                diagnosticSourceAssembly = null;
            }
            else
            {
                Console.WriteLine($"The \"{DiagnosticSourceAssembly_Name}\" assembly is already loaded by the application and will be used:"
                       + $" FullName=\"{diagnosticSourceAssembly.FullName}\", Location=\"{diagnosticSourceAssembly.Location}\".");
            }

            s_diagnosticSourceAssembly = diagnosticSourceAssembly;
            return diagnosticSourceAssembly;
        }

        // Ok, so DiagnosticSource.dll is not already loaded.
        // We need to load it. We will request this by specifying the assembly without the version.
        // The runtime will search the normal asembly resolution paths. If it finds any version, we will use it.
        // This approach is "almost" certain to give us the DiagnosticSource version that would have been loaded
        // by the application if it tries to load the assembly later.
        // (Note there are some unlikely but possible edge cases to still have a version mismatch if the application
        // messes with assembly loading logic in the default load context.)
        // In case that DiagnosticSource is not found in the normal probing paths, we will need to hook up the
        // AssemblyResolve event before we request the load. The event handler will do additional work to fall back
        // to using assembly binaries packaged with this library.

        AppDomain.CurrentDomain.AssemblyResolve += SharedHostPolicy.SharedAssemblyResolver.AssemblyResolveEventHandler;

        string diagnosticSourceNameString_NoVersion = $"{DiagnosticSourceAssembly_Name}, {DiagnosticSourceAssembly_Version} , {DiagnosticSourceAssembly_Culture}, {DiagnosticSourceAssembly_PublicKeyToken}";
        AssemblyName diagnosticSourceAssemblyName_NoVersion = new AssemblyName(diagnosticSourceNameString_NoVersion);

        Console.WriteLine($"The \"{DiagnosticSourceAssembly_Name}\" assembly is not yet loaded by the application."
               + $" Performing explicit load by FullName=\"{diagnosticSourceAssemblyName_NoVersion.FullName}\"");

        diagnosticSourceAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(diagnosticSourceAssemblyName_NoVersion);

        if (diagnosticSourceAssembly == null)
        {
            Console.WriteLine($"Could not load the \"{DiagnosticSourceAssembly_Name}\" assembly even after advanced assembly resolution logic.");
        }
        else
        {
            AssemblyName asmName = diagnosticSourceAssembly.GetName();
            if (asmName.Version < DiagnosticSourceAssembly_MinReqVersion)
            {
                // Theoretically we could avoid loading old versions and require at least the min-req version. 
                // Note however, that this would ONLY work if DiagnosticSource.dll is NOT YET loaded when the DynamicLoader initializes.
                // If the assembly is ALREADY loaded, then it's too late. If an assembly binary with an old version is in the probing path,
                // than whether or not the assembly is ALREADY loaded is, essentially, a race condition. For some applications it may result
                // in different outcomes each time. We do not want to engage in such flaky behaviour also ALWAYS bail out.

                Console.WriteLine($"The \"{DiagnosticSourceAssembly_Name}\" assembly was loaded, but its version too old."
                        + " This happens when an old version of the assembly is located in the default assembly probing paths."
                        + " A newer version of the assembly is distributed with this library,"
                        + " however, the presence of the old assembly version implies that this application requires that partilar version."
                        + $" Upgrade your application to use a newer version of the \"{DiagnosticSourceAssembly_Name}\" assembly or delete"
                        + " the assembly binaries from any assembly probing paths in order to allow this library to inject a newer version."
                        + $" Minimum required version: \"{DiagnosticSourceAssembly_MinReqVersion}\";"
                        + $" Actually loaded assembly: {{Version=\"{asmName.Version}\", Location=\"{diagnosticSourceAssembly.Location}\"}}."
                        + " Activity-based auto-instrumentation cannot be performed.");

                diagnosticSourceAssembly = null;
            }
            else
            {
                Console.WriteLine($"The \"{DiagnosticSourceAssembly_Name}\" was loaded:"
                       + $" FullName=\"{diagnosticSourceAssembly.FullName}\", Location=\"{diagnosticSourceAssembly.Location}\".");
            }
        }

        s_diagnosticSourceAssembly = diagnosticSourceAssembly;
        return diagnosticSourceAssembly;
    }
}

namespace SharedHostPolicy
{
    class SharedAssemblyResolver
    {
        private static readonly ReadOnlyCollection<string> AssemblyList = new ReadOnlyCollection<string>(
                                                                    new string[]
                                                                    {
                                                                                "Microsoft.AI.WindowsServer", // AzureRoleEnvironmentContextReader creates a new AppDomain, this will help resolve the library. 
                                                                                "System.Buffers",
                                                                                "System.Diagnostics.DiagnosticSource",
                                                                                "System.Memory",
                                                                                "System.Runtime.CompilerServices.Unsafe",
                                                                    });

        private static List<int> processedAssemblyList = new List<int>(5); // AssemblyList count is 5.
        private static readonly string LightupAssemblyPath = GetCurrentAssemblyPath();

        public static Assembly LoadAssemblyFromSharedLocation(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            var sharedAssemblyPath = Path.Combine(StartupHook.OTelInsFolderLocation, $"{assemblyName.Name}.dll");
            if (File.Exists(sharedAssemblyPath))
            {
                return Assembly.LoadFrom(sharedAssemblyPath);
            }

            return null;
        }

        public static Assembly AssemblyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            // If the assembly that caused this callback is not one of the assemblies packaged with this library, we do nothing:

            if (args == null || args.Name == null)
            {
                return null;
            }

            var assemblyName = new AssemblyName(args.Name);
            var shortAssemblyName = assemblyName.Name;
            var index = AssemblyList.IndexOf(shortAssemblyName);
            string dllPath;

            if (index >= 0 && !processedAssemblyList.Contains(index))
            {
                // We need to mark the assembly as processed to avoid a recursive call caused by the load request.
                processedAssemblyList.Add(index);
                dllPath = Path.Combine(LightupAssemblyPath, shortAssemblyName + ".dll");
                // var assembly = Assembly.LoadFrom(dllPath);
            }
            else
            {
                return null;
            }

            // Ok, we need to do stuff.

            // If we already marked this assembly as processed, we are in a recursive call caused by the load request below.
            // That means load failed even after copy and there is nothing else we can do.

            /* if (packagedAssemblyInfo.IsProcessedFromPackage)
            {
                Console.WriteLine($"The assembly \"{args.Name}\" was not found using the normal assembly resolution method."
                        + $" A fallback assembly binary is included in file \"{packagedAssemblyInfo.AssemblyFilePath}\"."
                        + $" Copying that file into this application's base directory was attempted, but the assembly still cannot be resolved."
                        + $" Giving up.");

                return null;
            }*/

            Console.WriteLine($"The assembly \"{args.Name}\" was not found using the normal assembly resolution method."
                   + $" A fallback assembly binary is included in file \"{dllPath}\"."
                   + $" That file will be now copied into this application's base directory and the loading will be retried.");

            // Validate AppDomain parameter:

            Validate.NotNull(sender, nameof(sender));
            AppDomain senderAppDomain = sender as AppDomain;
            if (senderAppDomain == null)
            {
                throw new ArgumentException($"The specified {nameof(sender)} is expected to be of runtime type {nameof(AppDomain)},"
                                          + $" but the actual type is {sender.GetType().FullName}.");
            }

            CopyFileToBaseDirectory(dllPath, senderAppDomain);

            Console.WriteLine($"Assembly binary copied into this application's base directory. Requesting to load assembly \"{dllPath}\".");

            Assembly resolvedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
            return resolvedAssembly;
        }

        private static void CopyFileToBaseDirectory(string srcFilePath, AppDomain appDomain)
        {
            string baseDirectory = appDomain.BaseDirectory;
            string fileName = Path.GetFileName(srcFilePath);
            string dstFilePath = Path.Combine(baseDirectory, fileName);

            Console.WriteLine($"Copying file \"{srcFilePath}\" to \"{ dstFilePath}\".");
            try
            {
                File.Copy(srcFilePath, dstFilePath, overwrite: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to copy file. Assembly loading will likely fail again. Details: \"{ex.ToString()}\".");
            }
        }

        private static string GetCurrentAssemblyPath()
        {
            try
            {
                var currentAssembly = Assembly.GetExecutingAssembly();
                var currentAssemblyPath = new FileInfo(currentAssembly.Location);
                return currentAssemblyPath.DirectoryName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get LightupAssemblyPath {ex}");
            }

            return null;
        }
    }
}
