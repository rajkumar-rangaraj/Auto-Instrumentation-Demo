using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace OpenTelemetry.StartupHookDemo.Adaptor
{
    /// <summary>
    /// ListenerHandler base class.
    /// </summary>
    internal abstract class ListenerHandler
    {
        internal readonly static MethodInfo startActivityMethodInfo = null;
        internal readonly static MethodInfo activityStopMethodInfo = null;

        internal readonly static PropertyInfo activityCurrentPropertyInfo = null;

        internal readonly static Type activitySourceType = null;
        internal readonly static Type activityType = null;

        static ListenerHandler()
        {
            Assembly diagnosticSourceAssembly = SharedAssemblyResolver.oTelDiagnosticSourceAssembly;

            // Create a instance of ActivitySource.
            activitySourceType = diagnosticSourceAssembly.GetType("System.Diagnostics.ActivitySource");

            // Get Activity.Current.
            activityType = diagnosticSourceAssembly.GetType("System.Diagnostics.Activity");
            activityStopMethodInfo = activityType.GetMethod("Stop");
            activityCurrentPropertyInfo = activityType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);

            // ActivitySource.StartActivity
            // This is needed for to get the type for IEnumerable<ActivityLink> to use in signature.
            FieldInfo linksFieldInfo = activityType.GetField("s_emptyLinks", BindingFlags.NonPublic | BindingFlags.Static);
            Type[] startActivitySignature = new[] { typeof(string), diagnosticSourceAssembly.GetType("System.Diagnostics.ActivityKind"), typeof(string), typeof(IEnumerable<KeyValuePair<string, object>>), linksFieldInfo.FieldType, typeof(DateTimeOffset) };
            startActivityMethodInfo = activitySourceType.GetMethod("StartActivity", startActivitySignature);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ListenerHandler"/> class.
        /// </summary>
        /// <param name="sourceName">The name of the <see cref="ListenerHandler"/>.</param>
        public ListenerHandler(string sourceName)
        {
            this.SourceName = sourceName;
        }

        /// <summary>
        /// Gets the name of the <see cref="ListenerHandler"/>.
        /// </summary>
        public string SourceName { get; }

        /// <summary>
        /// Gets a value indicating whether the <see cref="ListenerHandler"/> supports NULL <see cref="Activity"/>.
        /// </summary>
        public virtual bool SupportsNullActivity { get; }

        /// <summary>
        /// Method called for an event with the suffix 'Start'.
        /// </summary>
        /// <param name="activity">The <see cref="Activity"/> to be started.</param>
        /// <param name="payload">An object that represent the value being passed as a payload for the event.</param>
        public virtual void OnStartActivity(Activity activity, KeyValuePair<string, object> value)
        {
        }

        /// <summary>
        /// Method called for an event with the suffix 'Stop'.
        /// </summary>
        /// <param name="activity">The <see cref="Activity"/> to be stopped.</param>
        /// <param name="payload">An object that represent the value being passed as a payload for the event.</param>
        public virtual void OnStopActivity(Activity activity, KeyValuePair<string, object> value)
        {
        }

        /// <summary>
        /// Method called for an event with the suffix 'Exception'.
        /// </summary>
        /// <param name="activity">The <see cref="Activity"/>.</param>
        /// <param name="payload">An object that represent the value being passed as a payload for the event.</param>
        public virtual void OnException(Activity activity, KeyValuePair<string, object> value)
        {
        }

        /// <summary>
        /// Method called for an event which does not have 'Start', 'Stop' or 'Exception' as suffix.
        /// </summary>
        /// <param name="name">Custom name.</param>
        /// <param name="activity">The <see cref="Activity"/> to be processed.</param>
        /// <param name="payload">An object that represent the value being passed as a payload for the event.</param>
        public virtual void OnCustom(string name, Activity activity, KeyValuePair<string, object> value)
        {
        }
    }
}
