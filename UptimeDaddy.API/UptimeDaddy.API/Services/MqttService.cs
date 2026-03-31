using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using System.Text;
using System.Text.Json;
using UptimeDaddy.API.Data;
using UptimeDaddy.API.DTOs;
using UptimeDaddy.API.Models;

namespace UptimeDaddy.API.Services
{
    public class MqttService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private IMqttClient? _client;

        public MqttService(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var host = _configuration["Mqtt:Host"];
            var port = int.Parse(_configuration["Mqtt:Port"] ?? "1883");

            if (string.IsNullOrWhiteSpace(host))
            {
                Console.WriteLine("MQTT host is not configured.");
                return;
            }

            var factory = new MqttClientFactory();
            _client = factory.CreateMqttClient();

            _client.ApplicationMessageReceivedAsync += async e =>
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                Console.WriteLine($"MQTT message received: {payload}");

                try
                {
                    var message = JsonSerializer.Deserialize<MqttMeasurementMessageDto>(payload);

                    if (message == null || message.Pages.Count == 0)
                    {
                        Console.WriteLine("No pages found in MQTT payload.");
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    foreach (var page in message.Pages)
                    {
                        var websiteExists = await context.Websites
                            .AnyAsync(w => w.Id == page.Id, stoppingToken);

                        if (!websiteExists)
                        {
                            Console.WriteLine($"Website {page.Id} not found. Skipping measurement.");
                            continue;
                        }

                        var measurement = new Measurement
                        {
                            WebsiteId = page.Id,
                            StatusCode = int.TryParse(page.Response.Status, out var statusCode) ? statusCode : 0,
                            DnsLookupMs = page.Response.DnsLookup,
                            ConnectMs = page.Response.ConnectToPage,
                            TlsHandshakeMs = page.Response.TlsHandShake,
                            TimeToFirstByteMs = page.Response.TimeToFirstByte,
                            TotalTimeMs = page.Response.TotalTime,
                            CreatedAt = DateTime.UtcNow
                        };

                        context.Measurements.Add(measurement);
                    }

                    await context.SaveChangesAsync(stoppingToken);
                    Console.WriteLine("Measurements saved to database.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while processing MQTT message: {ex}");
                }
            };

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .Build();

            try
            {
                await _client.ConnectAsync(options, stoppingToken);
                await _client.SubscribeAsync("uptime/measurements", cancellationToken: stoppingToken);

                Console.WriteLine("MQTT connected and subscribed to uptime/measurements");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MQTT failed to connect: {ex.Message}");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}