using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenTelemetry.StartupHookDemo.Adaptor
{
    public class HttpClientDiagnosticSourceListener : IObserver<KeyValuePair<string, object>>
    {
        internal readonly MethodInfo startActivityMethodInfo = null;
        internal readonly MethodInfo onStartActivityMethodInfo = null;
        internal readonly MethodInfo onStopActivityMethodInfo = null;

        internal readonly PropertyInfo activityCurrentPropertyInfo = null;

        internal readonly object httpHandlerDiagnosticListenerInstance;
        internal readonly object activitySourceInstance = null;

        public HttpClientDiagnosticSourceListener()
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
            Assembly assembly = SharedAssemblyResolver.oTelDiagnosticSourceAssembly;

            // Create instance of ActivitySource.
            Type activitySourceType = assembly.GetType("System.Diagnostics.ActivitySource");
            activitySourceInstance = Activator.CreateInstance(activitySourceType, flags, null, new object[] { "OpenTelemetry.Instrumentation.Http", "1.0.0.0" }, null);

            // Get Activity.Current.
            Type activityType = assembly.GetType("System.Diagnostics.Activity");
            activityCurrentPropertyInfo = activityType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);

            // ActivitySource.StartActivity
            // This is needed for to get the type for IEnumerable<ActivityLink> to use in signature.
            FieldInfo linksFieldInfo = activityType.GetField("s_emptyLinks", BindingFlags.NonPublic | BindingFlags.Static);
            Type[] startActivitySignature = new[] { typeof(string), assembly.GetType("System.Diagnostics.ActivityKind"), typeof(string), typeof(IEnumerable<KeyValuePair<string, object>>), linksFieldInfo.FieldType, typeof(DateTimeOffset) };
            startActivityMethodInfo = activitySourceType.GetMethod("StartActivity", startActivitySignature);

            // Create an instance of HttpHandlerDiagnosticListener with HttpClientInstrumentationOptions
            Type httpClientInstrumentationOptionstype = Type.GetType("OpenTelemetry.Instrumentation.Http.HttpClientInstrumentationOptions, OpenTelemetry.Instrumentation.Http");
            object httpClientInstrumentationOptionsInstance = Activator.CreateInstance(httpClientInstrumentationOptionstype, flags, null, null, null);

            Type HttpHandlerDiagnosticListenertype = Type.GetType("OpenTelemetry.Instrumentation.Http.Implementation.HttpHandlerDiagnosticListener, OpenTelemetry.Instrumentation.Http");
            httpHandlerDiagnosticListenerInstance = Activator.CreateInstance(HttpHandlerDiagnosticListenertype, flags, null, new object[] { httpClientInstrumentationOptionsInstance }, null);

            // Get OnStartActivity and OnStopActivity method from HttpHandlerDiagnosticListener.
            var signatureOnStartActivity = new[] {activityType, typeof(object) };
            onStartActivityMethodInfo = HttpHandlerDiagnosticListenertype.GetMethod("OnStartActivity", signatureOnStartActivity);
            onStopActivityMethodInfo = HttpHandlerDiagnosticListenertype.GetMethod("OnStopActivity", signatureOnStartActivity);
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

                // Namespace: OpenTelemetry.Instrumentation.Http.Implementation
                // HttpHandlerDiagnosticListener.OnStartActivity(Activity activity, object payload)
                onStartActivityMethodInfo.Invoke(httpHandlerDiagnosticListenerInstance, new object[] {activity, value.Value});
            }
            else if (value.Key.EndsWith("Stop", StringComparison.Ordinal))
            {
                try
                {
                    // Namespace: System.Diagnostics
                    // Activity.Current
                    dynamic currentActivity = activityCurrentPropertyInfo.GetValue(null, null);
                    if (currentActivity != null)
                    {
                        // TODO: Add Tags, Baggage - If exists.

                        // Namespace: OpenTelemetry.Instrumentation.Http.Implementation
                        // HttpHandlerDiagnosticListener.OnStopActivity(Activity activity, object payload)
                        onStopActivityMethodInfo.Invoke(httpHandlerDiagnosticListenerInstance, new object[] { currentActivity, value.Value });

                        // stop the current activity in Otel diagnostic source as we created it. 
                        currentActivity.Stop();
                    }
                }
                catch (Exception)
                {
                }

            }
        }
    }
}
