using Examples.Console;
using System;

namespace DemoConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            using (var sample = new InstrumentationWithActivitySource())
            {
                sample.Start();

                System.Console.WriteLine("Traces are being created and exported " +
                    "to Console in the background. " +
                    "Press ENTER to stop.");
                System.Console.ReadLine();
            }
        }
    }
}
