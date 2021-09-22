using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace OpenTelemetry.StartupHookDemo.Adaptor
{
    internal class DiagnosticSourceSubscriber : IDisposable, IObserver<DiagnosticListener>
    {
        private readonly List<IDisposable> listenerSubscriptions;
        private readonly Func<string, ListenerHandler> handlerFactory;
        private readonly Func<DiagnosticListener, bool> diagnosticSourceFilter;
        private readonly Func<string, object, object, bool> isEnabledFilter;
        private long disposed;
        private IDisposable allSourcesSubscription;

        public DiagnosticSourceSubscriber(
            ListenerHandler handler,
            Func<string, object, object, bool> isEnabledFilter)
            : this(_ => handler, value => handler.SourceName == value.Name, isEnabledFilter)
        {
        }

        public DiagnosticSourceSubscriber(
            Func<string, ListenerHandler> handlerFactory,
            Func<DiagnosticListener, bool> diagnosticSourceFilter,
            Func<string, object, object, bool> isEnabledFilter)
        {
            this.listenerSubscriptions = new List<IDisposable>();
            this.handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
            this.diagnosticSourceFilter = diagnosticSourceFilter;
            this.isEnabledFilter = isEnabledFilter;
        }

        public void Subscribe()
        {
            if (this.allSourcesSubscription == null)
            {
                this.allSourcesSubscription = DiagnosticListener.AllListeners.Subscribe(this);
            }
        }

        public void OnNext(DiagnosticListener value)
        {
            if ((Interlocked.Read(ref this.disposed) == 0) &&
                this.diagnosticSourceFilter(value))
            {
                var handler = this.handlerFactory(value.Name);
                var listener = new DiagnosticSourceListener(handler);
                var subscription = this.isEnabledFilter == null ?
                    value.Subscribe(listener) :
                    value.Subscribe(listener, this.isEnabledFilter);

                lock (this.listenerSubscriptions)
                {
                    this.listenerSubscriptions.Add(subscription);
                }
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 1)
            {
                return;
            }

            lock (this.listenerSubscriptions)
            {
                foreach (var listenerSubscription in this.listenerSubscriptions)
                {
                    listenerSubscription?.Dispose();
                }

                this.listenerSubscriptions.Clear();
            }

            this.allSourcesSubscription?.Dispose();
            this.allSourcesSubscription = null;
        }
    }
}
