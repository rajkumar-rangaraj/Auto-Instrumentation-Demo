using OpenTelemetry;
using OpenTelemetry.Trace;
using System.Threading;

namespace OpenTelemetryInstrumentation
{
    public class TestConsoleExporter
    {
        public void Enable()
        {
            var openTelemetry = Sdk.CreateTracerProviderBuilder()
                    .AddSource("Samples.SampleClient", "Samples.SampleServer")
                    .AddConsoleExporter()
                    .Build();

            System.Console.WriteLine("OpenTelemetry Added!!!");
        }
    }
}
