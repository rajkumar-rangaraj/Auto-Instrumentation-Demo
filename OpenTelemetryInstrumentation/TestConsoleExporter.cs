using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Reflection;
using System.Threading;

namespace OpenTelemetryInstrumentation
{
    public class TestConsoleExporter
    {
        public void Enable()
        {
            var openTelemetry = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("OTelDemoWebApp"))
                    .AddSource("Samples.SampleClient", "Samples.SampleServer")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter()
                    .AddZipkinExporter(o =>
                    {
                        o.Endpoint = new Uri("http://localhost:9411/api/v2/spans");
                    })
                    .Build();

            System.Console.WriteLine("OpenTelemetry Added!!!");
        }
    }
}
