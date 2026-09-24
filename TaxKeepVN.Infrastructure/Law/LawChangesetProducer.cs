using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using TaxKeepVN.Application.DTOs.Law.Contract;
using TaxKeepVN.Application.Law.Messaging;

namespace TaxKeepVN.Infrastructure.Law
{
    public class LawChangesetProducer : ILawChangesetProducer
    {
        private readonly IConfiguration _config;
        private readonly ILogger<LawChangesetProducer> _logger;
        private const string RequestQueueName = "law.changeset.request.queue";

        public LawChangesetProducer(IConfiguration config, ILogger<LawChangesetProducer> logger)
        {
            _config = config;
            _logger = logger;
        }

        public Task PublishExtractRequestAsync(LawChangesetExtractRequest request)
        {
            try
            {
                var factory = new ConnectionFactory
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

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(request, jsonOptions);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;

                channel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: RequestQueueName,
                    basicProperties: properties,
                    body: body
                );

                _logger.LogInformation("Successfully published LawChangesetExtractRequest to {Queue} for TaskId={TaskId}, ChangesetId={ChangesetId}",
                    RequestQueueName, request.TaskId, request.ChangesetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish LawChangesetExtractRequest to RabbitMQ for TaskId={TaskId}", request.TaskId);
                throw;
            }

            return Task.CompletedTask;
        }
    }
}
