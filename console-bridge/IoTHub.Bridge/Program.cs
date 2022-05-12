using IoTHub.Bridge.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Newtonsoft.Json;
using Serilog;

namespace IoTHub.Bridge
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting up.");
            using var host = CreateHostBuilder(args).Build();

            await DoStuff(host.Services);
            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            var basePath = AppContext.BaseDirectory;
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", false)
                .AddUserSecrets<Program>()
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddConfiguration(configuration);
                })
                .ConfigureServices(services =>
                {
                    services.AddLogging(c => c.AddSerilog().AddConsole());
                    services.AddSingleton<MqttFactory>();
                    services.AddSingleton<IIoTHubService, IoTHubService>();
                    services.AddScoped<IMQTTService, MQTTService>();
                })
                .UseConsoleLifetime();
        }

        private static async Task DoStuff(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;

            var mqttService = provider.GetRequiredService<IMQTTService>();
            var iotHubService = provider.GetRequiredService<IIoTHubService>();

            await mqttService.Connect();
            await iotHubService.Connect();

            await mqttService.Subscribe(async payload =>
            {
                //Do fancy stuff with your message here, like buffering & calculations for avg/time windows or reduction
                // Console.WriteLine(payload);

                //inject the message into iothub
                await iotHubService.Send(payload);
            });


            // send 50 test messages
            for (var i = 0; i < 50; i++)
            {
                var payload = JsonConvert.SerializeObject(new
                {
                    Status = "OK",
                    Id = i
                });

                await mqttService.Publish(payload);
                await Task.Delay(200);
            }

        }
    }
}