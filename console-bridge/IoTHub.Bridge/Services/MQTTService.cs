using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Subscribing;
using MQTTnet.Client.Unsubscribing;
using System.Text;

namespace IoTHub.Bridge.Services
{
    public interface IMQTTService
    {
        Task Connect();
        Task Disconnect();
        bool IsConnected();
        Task Subscribe(Action<string> onMessageRecievedAction);
        Task Unsubscribe();
        Task Publish(string message);
    }

    public class MqttServiceOptions
    {
        public string Address { get; set; }
        public int Port { get; set; }
        public string DefaultTopic { get; set; }
    }

    public class MQTTService : IMQTTService
    {
        private readonly IMqttClientOptions mqttClientOptions;
        private readonly MqttClientSubscribeOptions mqttSubscribeOptions;
        private readonly MqttClientUnsubscribeOptions mqttUnsubscribeOptions;
        private readonly IMqttClient mqttClient;
        private readonly ILogger logger;
        private readonly string defaultTopic;

        public MQTTService(IConfiguration config, ILogger<MQTTService> logger, MqttFactory mqttFactory)
        {
            this.logger = logger;

            this.defaultTopic = config.GetValue<string>("services:mqtt:defaulttopic");

            this.mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(config.GetValue<string>("services:mqtt:address"), config.GetValue<int>("services:mqtt:port", 1883))
                //add additional options such as auth etc
                .Build();

            this.mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(this.defaultTopic)
                .Build();

            this.mqttUnsubscribeOptions = mqttFactory.CreateUnsubscribeOptionsBuilder()
                .WithTopicFilter(this.defaultTopic)
                .Build();

            this.mqttClient = mqttFactory.CreateMqttClient();

        }

        public async Task Connect()
        {
            await this.mqttClient.ConnectAsync(this.mqttClientOptions, CancellationToken.None);
        }

        public async Task Disconnect()
        {
            await this.mqttClient.DisconnectAsync(CancellationToken.None);
        }

        public bool IsConnected()
        {
            return this.mqttClient.IsConnected;
        }

        public async Task Publish(string payload)
        {
            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(this.defaultTopic)
                .WithPayload(payload)
                .Build();

            await this.mqttClient.PublishAsync(applicationMessage);
            this.logger.LogDebug($"message published: {payload}");
        }

        public async Task Subscribe(Action<string> onMessageRecievedAction)
        {
            await this.mqttClient.SubscribeAsync(this.mqttSubscribeOptions, CancellationToken.None);
            this.logger.LogDebug($"subscribed to topic {this.defaultTopic}");

            this.mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                //hard-coded for JSON message
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                this.logger.LogDebug($"message received: {payload}");

                onMessageRecievedAction.Invoke(payload);
            });
        }

        public async Task Unsubscribe()
        {
            await this.mqttClient.UnsubscribeAsync(this.mqttUnsubscribeOptions);
            this.logger.LogDebug($"unsubscribed from topic {this.defaultTopic}");
        }
    }
}
