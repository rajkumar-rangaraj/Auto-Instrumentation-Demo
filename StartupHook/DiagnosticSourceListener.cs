using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTelemetry.StartupHookDemo.Adaptor
{
    internal class DiagnosticSourceListener : IObserver<KeyValuePair<string, object>>
    {
        private readonly ListenerHandler handler;

        public DiagnosticSourceListener(ListenerHandler handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (!this.handler.SupportsNullActivity && Activity.Current == null)
            {
                return;
            }

            try
            {
                if (value.Key.EndsWith("Start", StringComparison.Ordinal))
                {
                    this.handler.OnStartActivity(Activity.Current, value);
                }
                else if (value.Key.EndsWith("Stop", StringComparison.Ordinal))
                {
                    this.handler.OnStopActivity(Activity.Current, value);
                }
                else if (value.Key.EndsWith("Exception", StringComparison.Ordinal))
                {
                    this.handler.OnException(Activity.Current, value);
                }
                else
                {
                    this.handler.OnCustom(value.Key, Activity.Current, value);
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
