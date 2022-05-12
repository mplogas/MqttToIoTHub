using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace IoTHub.Bridge.Services
{
    public interface IIoTHubService
    {
        Task Connect();
        Task Disconnect();
        bool IsConnected();
        Task Send(string payload);
    }

    class IoTHubServiceOptions
    {
        public string ConnectionString { get; set; }
    }

    public class IoTHubService : IIoTHubService
    {
        private readonly ILogger<IoTHubService> logger;
        private readonly ModuleClient client;
        private ConnectionStatus connectionStatus;

        public IoTHubService(IConfiguration config, ILogger<IoTHubService> logger)
        {
            this.logger = logger;
            this.client = ModuleClient.CreateFromConnectionString(config.GetValue<string>("services:iothub:connectionstring"));
            this.client.SetConnectionStatusChangesHandler(ConnectionStatusChangeHandler);
        }

        private void ConnectionStatusChangeHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            switch (status)
            {
                case ConnectionStatus.Connected:
                    this.logger.LogInformation("IoT Hub client connected.");
                    break;
                case ConnectionStatus.Disconnected:
                    this.logger.LogInformation("IoT Hub client disconnected.");
                    break;
                case ConnectionStatus.Disabled:
                    this.logger.LogWarning($"IoT Hub client disabled! Reason {reason}");
                    break;
                default:
                    this.logger.LogDebug($"IoT Hub Client status change: {status}");
                    break;
            }

            connectionStatus = status;
        }

        public async Task Connect()
        {
            await this.client.OpenAsync();
        }

        public async Task Disconnect()
        {
            await this.client.CloseAsync();
        }

        public bool IsConnected()
        {
            return connectionStatus == ConnectionStatus.Connected;
        }

        public async Task Send(string payload)
        {
            if (IsConnected())
            {
                using var message = new Message(Encoding.UTF8.GetBytes(payload))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentEncoding = Encoding.UTF8.ToString(),
                    ContentType = "application/json"
                };

                await this.client.SendEventAsync(message);

                this.logger.LogDebug("Message to IoT Hub sent");
            }
            else
            {
                this.logger.LogWarning("Failed to send message to IoT Hub");
            }
        }
    }
}
