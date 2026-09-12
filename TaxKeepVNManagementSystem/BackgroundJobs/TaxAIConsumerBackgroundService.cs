using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVNManagementSystem.Hubs;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    public class TaxAIConsumerBackgroundService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly IHubContext<TaxAIHub> _hubContext;
        private readonly ILogger<TaxAIConsumerBackgroundService> _logger;
        private const string ResponseQueueName = "tax.ai.response.queue";

        public TaxAIConsumerBackgroundService(
            IConfiguration config,
            IHubContext<TaxAIHub> hubContext,
            ILogger<TaxAIConsumerBackgroundService> logger)
        {
            _config = config;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TaxAIConsumerBackgroundService is starting...");

            var rabbitUrl = _config["RabbitMQ:Url"] ?? "amqp://guest:guest@localhost:5672/";
            var factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitUrl),
                DispatchConsumersAsync = true // Cho phap xu ly async trong event Received
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var connection = factory.CreateConnection();
                    using var channel = connection.CreateModel();

                    channel.QueueDeclare(
                        queue: ResponseQueueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null
                    );

                    channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.Received += async (model, ea) =>
                    {
                        try
                        {
                            var body = ea.Body.ToArray();
                            var jsonString = Encoding.UTF8.GetString(body);
                            _logger.LogInformation("Received tax extraction response: {Json}", jsonString);

                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            var response = JsonSerializer.Deserialize<TaxRuleExtractResponseMessage>(jsonString, options);

                            if (response != null)
                            {
                                var eventName = response.Status == "SUCCESS"
                                    ? "OnTaxExtractionCompleted"
                                    : "OnTaxExtractionFailed";

                                // 1. Gui cho Admin cu the theo AdminId (neu co)
                                if (response.AdminId.HasValue)
                                {
                                    await _hubContext.Clients.User(response.AdminId.Value.ToString())
                                        .SendAsync(eventName, response, stoppingToken);
                                }

                                // 2. Gui cho Group dang theo doi TaskId nay
                                await _hubContext.Clients.Group($"task_{response.TaskId}")
                                    .SendAsync(eventName, response, stoppingToken);

                                // 3. Broadcast cho tat ca Clients dang ket noi (fallback)
                                await _hubContext.Clients.All
                                    .SendAsync(eventName, response, stoppingToken);

                                _logger.LogInformation(
                                    "Dispatched SignalR event {EventName} for taskId={TaskId}, status={Status}",
                                    eventName, response.TaskId, response.Status);
                            }

                            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing message from {Queue}", ResponseQueueName);
                            // ACK de khong gay unhandled loop hoac co the NACK requeue neu can
                            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                        }
                    };

                    channel.BasicConsume(
                        queue: ResponseQueueName,
                        autoAck: false,
                        consumer: consumer
                    );

                    _logger.LogInformation("TaxAIConsumerBackgroundService connected and listening on {Queue}", ResponseQueueName);

                    // Giu ket noi ton tai cho toi khi co tin hieu dung
                    while (!stoppingToken.IsCancellationRequested && connection.IsOpen)
                    {
                        await Task.Delay(2000, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("RabbitMQ connection lost or not available: {Message}. Retrying in 5 seconds...", ex.Message);
                    try
                    {
                        await Task.Delay(5000, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.LogInformation("TaxAIConsumerBackgroundService has stopped.");
        }
    }
}
