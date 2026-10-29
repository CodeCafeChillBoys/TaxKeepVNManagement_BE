using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVN.Infrastructure.Services
{
    public class DocumentOcrProducerService : IDocumentOcrProducerService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<DocumentOcrProducerService> _logger;
        private const string DefaultQueueName = "expense.ocr.ai.request.queue";

        public DocumentOcrProducerService(IConfiguration config, ILogger<DocumentOcrProducerService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private string GetQueueName()
        {
            return _config["RabbitMQ:ExpenseOcrRequestQueue"] ?? DefaultQueueName;
        }

        private ConnectionFactory CreateFactory()
        {
            return new ConnectionFactory
            {
                Uri = new Uri(_config["RabbitMQ:Url"] ?? "amqp://guest:guest@localhost:5672/")
            };
        }

        public void PublishBatchOcrTasks(IEnumerable<DocumentOcrExtractRequestMessage> messages)
        {
            var queueName = GetQueueName();
            var factory = CreateFactory();

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            foreach (var message in messages)
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                channel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body
                );

                _logger.LogInformation("Published OCR task to queue '{Queue}' for document taskId={TaskId}, periodId={PeriodId}",
                    queueName, message.TaskId, message.PeriodId);
            }
        }
    }
}
