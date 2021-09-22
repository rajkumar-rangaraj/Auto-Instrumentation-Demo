using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenTelemetry.StartupHookDemo.Adaptor
{
    internal class HttpClientDiagnosticSourceListener : ListenerHandler
    {
        private const string DiagnosticSourceName = "HttpHandlerDiagnosticListener";

        internal readonly MethodInfo onStartActivityMethodInfo = null;
        internal readonly MethodInfo onStopActivityMethodInfo = null;

        internal readonly object httpHandlerDiagnosticListenerInstance;
        internal readonly object activitySourceInstance = null;

        public HttpClientDiagnosticSourceListener() : base(DiagnosticSourceName)
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
            activitySourceInstance = Activator.CreateInstance(activitySourceType, flags, null, new object[] { "OpenTelemetry.Instrumentation.Http", "1.0.0.0" }, null);

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

        public override void OnCustom(string name, Activity activity, KeyValuePair<string, object> value)
        {

        }

        public void OnError(Exception error)
        {

        }

        public override void OnException(Activity activity, KeyValuePair<string, object> value)
        {

        }

        public override void OnStartActivity(Activity activity, KeyValuePair<string, object> value)
        {
            // There is a check in OpenTelemetry which is acting as a barrier to combine start/stop.
            // Ref: https://github.com/open-telemetry/opentelemetry-dotnet/blob/232cc1dda5be6c0f987d46b7a25020a6f86a147d/src/OpenTelemetry.Instrumentation.Http/Implementation/HttpHandlerDiagnosticListener.cs#L92
            // Even OpenTelemetry does not know why that check was needed.

            // Namespace: System.Diagnostics
            // ActivitySource.StartActivity(string name,
            //                              ActivityKind kind,
            //                              string parentId,
            //                              IEnumerable<KeyValuePair<string, object?>>? tags = null,
            //                              IEnumerable<ActivityLink>? links = null,
            //                              DateTimeOffset startTime = default);
            var oTelActivity = startActivityMethodInfo.Invoke(
                activitySourceInstance,
                new object[] { activity.OperationName,
                    2,
                    null,
                    null,
                    null,
                    new DateTimeOffset(activity.StartTimeUtc)
                });

            // Namespace: OpenTelemetry.Instrumentation.Http.Implementation
            // HttpHandlerDiagnosticListener.OnStartActivity(Activity activity, object payload)
            onStartActivityMethodInfo.Invoke(httpHandlerDiagnosticListenerInstance, new object[] { oTelActivity, value.Value });
        }

        public override void OnStopActivity(Activity activity, KeyValuePair<string, object> value)
        {
            try
            {
                // Namespace: System.Diagnostics
                // Activity.Current
                dynamic currentOtelActivity = activityCurrentPropertyInfo.GetValue(null, null);
                if (currentOtelActivity != null)
                {
                    // TODO: Add Tags, Baggage - If exists.

                    // Namespace: OpenTelemetry.Instrumentation.Http.Implementation
                    // HttpHandlerDiagnosticListener.OnStopActivity(Activity activity, object payload)
                    onStopActivityMethodInfo.Invoke(httpHandlerDiagnosticListenerInstance, new object[] { currentOtelActivity, value.Value });

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
