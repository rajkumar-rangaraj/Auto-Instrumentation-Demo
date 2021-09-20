using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;

namespace DemoConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            //using var listener = new ActivityListener
            //{
            //    ShouldListenTo = _ => true,
            //    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            //    ActivityStarted = activity => Console.WriteLine($"{activity.ParentId}:{activity.Id} - Start"),
            //    ActivityStopped = activity => Console.WriteLine($"{activity.ParentId}:{activity.Id} - Stop")
            //};

            //ActivitySource.AddActivityListener(listener);

            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
                client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

                var stringTask1 = client.GetStringAsync("https://api.github.com/orgs/dotnet/repos");
                var msg1 = stringTask1.Result;

                var stringTask2 = client.GetStringAsync("https://www.bing.com");
                var msg2 = stringTask2.Result;

                var stringTask3 = client.GetStringAsync("http://httpstat.us/500");
                var msg3 = stringTask3.Result;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            Console.ReadLine();
        }
    }
}
