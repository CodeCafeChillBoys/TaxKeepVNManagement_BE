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
        private const string RequestQueueName = "tax.ai.request.queue";  // routeKey 

        public TaxAIProducerService(IConfiguration config)
        {
            _config = config;
        }

        public void PublishExtractionTask(TaxRuleExtractRequestMessage message)
        {
            // xác định xem url có đúng địa chỉ chỗ để thực hiện hay ko
            var factory = new ConnectionFactory()
            {
                Uri = new Uri(_config["RabbitMQ:Url"] ?? "amqp://guest:guest@localhost:5672/")
            };

            using var connection = factory.CreateConnection(); // Tạo liên kết đến server Rabbit
            using var channel = connection.CreateModel(); // Tạo queue

            channel.QueueDeclare(
                queue: RequestQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var json = JsonSerializer.Serialize(message); // Từ obj chuyển sang json
            var body = Encoding.UTF8.GetBytes(json); // từ jsson chuyển sang kí tự ngôn ngữ máy

            var properties = channel.CreateBasicProperties(); // Dòng này tạo ra một object chứa các thuộc tính của message trước khi gửi.
            properties.Persistent = true; // Đánh dấu message là persistent (muốn RabbitMQ lưu message xuống disk thay vì chỉ giữ trong RAM).

            channel.BasicPublish(
                exchange: "",
                routingKey: RequestQueueName,
                basicProperties: properties,
                body: body
            );
        }
    }
}
