using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace OpenTelemetry.StartupHookDemo.Adaptor
{
    public class AspnetCoreDiagnosticSourceListener : IObserver<KeyValuePair<string, object>>
    {
        internal readonly MethodInfo startActivityMethodInfo = null;
        internal readonly MethodInfo onStartActivityMethodInfo = null;
        internal readonly MethodInfo onStopActivityMethodInfo = null;

        internal readonly PropertyInfo activityCurrentPropertyInfo = null;
        
        internal readonly object httpInListenerInstance;
        internal readonly object activitySourceInstance = null;

        public AspnetCoreDiagnosticSourceListener()
        {
            Assembly diagnosticSourceAssembly = SharedAssemblyResolver.oTelDiagnosticSourceAssembly;
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

            // Create a instance of ActivitySource.
            Type activitySourceType = diagnosticSourceAssembly.GetType("System.Diagnostics.ActivitySource");
            activitySourceInstance = Activator.CreateInstance(activitySourceType, flags, null, new object[] { "OpenTelemetry.Instrumentation.Http", "1.0.0.0" }, null);

            // Get Activity.Current.
            Type activityType = diagnosticSourceAssembly.GetType("System.Diagnostics.Activity");
            activityCurrentPropertyInfo = activityType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);

            // ActivitySource.StartActivity
            // This is needed for to get the type for IEnumerable<ActivityLink> to use in signature.
            FieldInfo linksFieldInfo = activityType.GetField("s_emptyLinks", BindingFlags.NonPublic | BindingFlags.Static);
            Type[] startActivitySignature = new[] { typeof(string), diagnosticSourceAssembly.GetType("System.Diagnostics.ActivityKind"), typeof(string), typeof(IEnumerable<KeyValuePair<string, object>>), linksFieldInfo.FieldType, typeof(DateTimeOffset) };
            startActivityMethodInfo = activitySourceType.GetMethod("StartActivity", startActivitySignature);

            // Create an instance of HttpHandlerDiagnosticListener with AspNetCoreInstrumentationOptions
            Type aspNetCoreInstrumentationOptionsType = Type.GetType("OpenTelemetry.Instrumentation.AspNetCore.AspNetCoreInstrumentationOptions, OpenTelemetry.Instrumentation.AspNetCore");
            object aspNetCoreInstrumentationOptionsInstance = Activator.CreateInstance(aspNetCoreInstrumentationOptionsType, flags, null, null, null);

            Type httpInListenerType = Type.GetType("OpenTelemetry.Instrumentation.AspNetCore.Implementation.HttpInListener, OpenTelemetry.Instrumentation.AspNetCore");           
            // This release has 2 arguments, but in the current code we have one argument
            httpInListenerInstance = Activator.CreateInstance(httpInListenerType, flags, null, new object[] { "Microsoft.AspNetCore", aspNetCoreInstrumentationOptionsInstance }, null);

            // Get OnStartActivity and OnStopActivity method from HttpHandlerDiagnosticListener.
            var signatureOnStartActivity = new[] {activityType, typeof(object) };
            onStartActivityMethodInfo = httpInListenerType.GetMethod("OnStartActivity", signatureOnStartActivity);
            onStopActivityMethodInfo = httpInListenerType.GetMethod("OnStopActivity", signatureOnStartActivity);
        }
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key.EndsWith("Start", StringComparison.Ordinal))
            {
                var currentActivity = Activity.Current;

                // Namespace: System.Diagnostics
                // ActivitySource.StartActivity(string name,
                //                              ActivityKind kind,
                //                              string parentId,
                //                              IEnumerable<KeyValuePair<string, object?>>? tags = null,
                //                              IEnumerable<ActivityLink>? links = null,
                //                              DateTimeOffset startTime = default);
                var activity = startActivityMethodInfo.Invoke(
                    activitySourceInstance, 
                    new object[] { currentActivity.OperationName, 
                    2,
                    null,
                    null, 
                    null,
                    new DateTimeOffset(currentActivity.StartTimeUtc)
                    });

                // Namespace: OpenTelemetry.Instrumentation.AspNetCore.Implementation
                // HttpInListener.OnStartActivity(Activity activity, object payload)
                onStartActivityMethodInfo.Invoke(httpInListenerInstance, new object[] {activity, value.Value});
            }
            else if (value.Key.EndsWith("Stop", StringComparison.Ordinal))
            {
                try
                {
                    // Namespace: System.Diagnostics
                    // Activity.Current
                    dynamic currentOtelActivity = activityCurrentPropertyInfo.GetValue(null, null);
                    if (currentOtelActivity != null)
                    {
                        // TODO: Add Tags, Baggage - If exists.

                        // Namespace: OpenTelemetry.Instrumentation.AspNetCore.Implementation
                        // HttpInListener.OnStopActivity(Activity activity, object payload)
                        onStopActivityMethodInfo.Invoke(httpInListenerInstance, new object[] { currentOtelActivity, value.Value });

                        // stop the current activity in Otel diagnostic source as we created it. 
                        currentOtelActivity.Stop();
                    }
                }
                catch (Exception)
                {
                }

            }
        }
    }
}
