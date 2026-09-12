using System;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVN.Infrastructure.Services
{
    public class TaxAIProducerService : ITaxAIProducerService
    {
        private readonly IConfiguration _config;
        private const string RequestQueueName = "tax.ai.request.queue";

        public TaxAIProducerService(IConfiguration config)
        {
            _config = config;
        }

        public void PublishExtractionTask(TaxRuleExtractRequestMessage message)
        {
            var factory = new ConnectionFactory()
            {
                Uri = new Uri(_config["RabbitMQ:Url"] ?? "amqp://guest:guest@localhost:5672/")
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: RequestQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            channel.BasicPublish(
                exchange: "",
                routingKey: RequestQueueName,
                basicProperties: properties,
                body: body
            );
        }
    }
}
