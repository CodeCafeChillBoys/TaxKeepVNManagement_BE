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
using Microsoft.Extensions.Caching.Memory;
using TaxKeepVN.Application.DTOs.OcrAI;
using TaxKeepVNManagementSystem.Hubs;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    public class OcrAIConsumerBackgroundService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly IHubContext<TaxAIHub> _hubContext;
        private readonly IMemoryCache _cache;
        private readonly ILogger<OcrAIConsumerBackgroundService> _logger;
        private const string ResponseQueueName = "ocr.ai.response.queue";

        public OcrAIConsumerBackgroundService(
            IConfiguration config,
            IHubContext<TaxAIHub> hubContext,
            IMemoryCache cache,
            ILogger<OcrAIConsumerBackgroundService> logger)
        {
            _config = config;
            _hubContext = hubContext;
            _cache = cache;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OcrAIConsumerBackgroundService is starting...");

            var rabbitUrl = _config["RabbitMQ:Url"] ?? "amqp://guest:guest@localhost:5672/";
            var factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitUrl),
                DispatchConsumersAsync = true
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
                            _logger.LogInformation("Received OCR response: {Json}", jsonString);

                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            var response = JsonSerializer.Deserialize<OcrExtractResponseMessage>(jsonString, options);

                            if (response != null)
                            {
                                // Lưu vào In-Memory Cache trong 30 phút để BE/FE có thể tra cứu theo taskId
                                _cache.Set($"ocr_task_{response.TaskId}", response, TimeSpan.FromMinutes(30));

                                var eventName = response.Success
                                    ? "OnOcrExtractionCompleted"
                                    : "OnOcrExtractionFailed";

                                // 1. Gửi cho User cụ thể theo UserId (nếu có)
                                if (response.UserId.HasValue)
                                {
                                    await _hubContext.Clients.User(response.UserId.Value.ToString())
                                        .SendAsync(eventName, response, stoppingToken);
                                }

                                // 2. Gửi cho Group đang theo dõi TaskId này
                                await _hubContext.Clients.Group($"task_{response.TaskId}")
                                    .SendAsync(eventName, response, stoppingToken);

                                // 3. Broadcast fallback cho tất cả Clients đang kết nối
                                await _hubContext.Clients.All
                                    .SendAsync(eventName, response, stoppingToken);

                                _logger.LogInformation(
                                    "Dispatched SignalR event {EventName} for taskId={TaskId}, success={Success}",
                                    eventName, response.TaskId, response.Success);
                            }

                            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing OCR response message");
                            channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                        }
                    };

                    channel.BasicConsume(queue: ResponseQueueName, autoAck: false, consumer: consumer);

                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RabbitMQ connection error in OcrAIConsumer, retrying in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }
    }
}
