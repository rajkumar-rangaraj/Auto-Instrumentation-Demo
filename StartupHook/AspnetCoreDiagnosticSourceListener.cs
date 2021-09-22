using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace OpenTelemetry.StartupHookDemo.Adaptor
{
    internal class AspnetCoreDiagnosticSourceListener : ListenerHandler
    {
        private const string DiagnosticSourceName = "Microsoft.AspNetCore";

        internal readonly MethodInfo onStartActivityMethodInfo = null;
        internal readonly MethodInfo onStopActivityMethodInfo = null;
        
        internal readonly object httpInListenerInstance;
        internal readonly object activitySourceInstance = null;

        public AspnetCoreDiagnosticSourceListener() : base(DiagnosticSourceName)
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
            // We could avoid creating many activity source instance and create one for auto-instrumentation. Add it to the source of class library.
            activitySourceInstance = Activator.CreateInstance(activitySourceType, flags, null, new object[] { "OpenTelemetry.Instrumentation.AspNetCore", "1.0.0.0" }, null);

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
                        new DateTimeOffset(activity.StartTimeUtc)});

            // Namespace: OpenTelemetry.Instrumentation.AspNetCore.Implementation
            // HttpInListener.OnStartActivity(Activity activity, object payload)
            onStartActivityMethodInfo.Invoke(httpInListenerInstance, new object[] { oTelActivity, value.Value });
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
                    // TODO: Call OnCustom if value.key == "Microsoft.AspNetCore.Mvc.BeforeAction"

                    // Namespace: OpenTelemetry.Instrumentation.AspNetCore.Implementation
                    // HttpInListener.OnStopActivity(Activity activity, object payload)
                    onStopActivityMethodInfo.Invoke(httpInListenerInstance, new object[] { currentOtelActivity, value.Value });

                    // stop the current activity in Otel diagnostic source as we created it. 
                    activityStopMethodInfo.Invoke(currentOtelActivity, null);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
