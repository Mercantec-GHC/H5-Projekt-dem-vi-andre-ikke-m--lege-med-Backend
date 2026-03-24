using System.Text;
using System.Text.Json;
using MQTTnet;

namespace UptimeDaddy.API.Services
{
    public class MqttPublishService
    {
        private readonly IConfiguration _configuration;

        public MqttPublishService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task PublishWebsiteCreatedAsync(long userId, long websiteId, string url, int intervalTime)
        {
            var host = _configuration["Mqtt:Host"];
            var port = int.Parse(_configuration["Mqtt:Port"] ?? "1883");

            var factory = new MqttClientFactory();
            var client = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .Build();

            await client.ConnectAsync(options);

            var payloadObject = new
            {
                type = "website_created",
                userId = userId,
                websiteId = websiteId,
                path = url,
                interval_time = intervalTime,
                timestamp = DateTime.UtcNow
            };

            var payload = JsonSerializer.Serialize(payloadObject);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("uptime/websites/created")
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .Build();

            await client.PublishAsync(message);
            await client.DisconnectAsync();
        }

        public async Task PublishWebsiteDeletedAsync(long userId, long websiteId)
        {
            var host = _configuration["Mqtt:Host"];
            var port = int.Parse(_configuration["Mqtt:Port"] ?? "1883");

            var factory = new MqttClientFactory();
            var client = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .Build();

            await client.ConnectAsync(options);

            var payloadObject = new
            {
                type = "website_deleted",
                userId = userId,
                websiteId = websiteId,
                timestamp = DateTime.UtcNow
            };

            var payload = JsonSerializer.Serialize(payloadObject);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("uptime/websites/deleted")
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .Build();

            await client.PublishAsync(message);
            await client.DisconnectAsync();
        }
    }
}