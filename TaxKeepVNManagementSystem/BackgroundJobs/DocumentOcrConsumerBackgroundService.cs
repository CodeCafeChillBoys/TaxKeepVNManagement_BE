using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using TaxKeepVNManagementSystem.Hubs;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    public class DocumentOcrConsumerBackgroundService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<TaxAIHub> _hubContext;
        private readonly ILogger<DocumentOcrConsumerBackgroundService> _logger;
        private const string DefaultResponseQueue = "expense.ocr.ai.response.queue";

        public DocumentOcrConsumerBackgroundService(
            IConfiguration config,
            IServiceProvider serviceProvider,
            IHubContext<TaxAIHub> hubContext,
            ILogger<DocumentOcrConsumerBackgroundService> logger)
        {
            _config = config;
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DocumentOcrConsumerBackgroundService is starting...");

            var rabbitUrl = _config["RabbitMQ:Url"] ?? "amqp://guest:guest@localhost:5672/";
            var responseQueue = _config["RabbitMQ:ExpenseOcrResponseQueue"] ?? DefaultResponseQueue;

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
                        queue: responseQueue,
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
                            _logger.LogInformation("Received document OCR extraction response: {Json}", jsonString);

                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            var response = JsonSerializer.Deserialize<DocumentOcrResponseMessage>(jsonString, options);

                            if (response?.Data != null)
                            {
                                await ProcessOcrResultAsync(response.Data, stoppingToken);

                                // Gửi SignalR thông báo real-time tới Client (chỉ có 2 trường hợp: Thành công hoặc Thất bại)
                                var eventName = string.Equals(response.Data.Status, "FAILED", StringComparison.OrdinalIgnoreCase)
                                    ? "OnDocumentOcrFailed"
                                    : "OnDocumentOcrCompleted";

                                if (response.Data.UserId.HasValue)
                                {
                                    await _hubContext.Clients.User(response.Data.UserId.Value.ToString())
                                        .SendAsync(eventName, response, stoppingToken);
                                }

                                await _hubContext.Clients.Group($"task_{response.Data.Id}")
                                    .SendAsync(eventName, response, stoppingToken);

                                await _hubContext.Clients.All
                                    .SendAsync(eventName, response, stoppingToken);

                                _logger.LogInformation("Dispatched SignalR event {EventName} for documentId={DocId}",
                                    eventName, response.Data.Id);
                            }

                            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing document OCR message from {Queue}", responseQueue);
                            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                        }
                    };

                    channel.BasicConsume(
                        queue: responseQueue,
                        autoAck: false,
                        consumer: consumer
                    );

                    _logger.LogInformation("DocumentOcrConsumerBackgroundService listening on {Queue}", responseQueue);

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
                    _logger.LogWarning("RabbitMQ connection lost in DocumentOcrConsumer: {Message}. Retrying in 5s...", ex.Message);
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

            _logger.LogInformation("DocumentOcrConsumerBackgroundService has stopped.");
        }

        private async Task ProcessOcrResultAsync(DocumentOcrData data, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var docRepo = unitOfWork.Repository<Document>();
            var document = await docRepo.GetByIdAsync(data.Id);

            if (document == null)
            {
                _logger.LogWarning("Document {DocId} not found in database.", data.Id);
                return;
            }

            if (string.Equals(document.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Document {DocId} is already CONFIRMED. Skipping OCR extraction update.", data.Id);
                return;
            }

            // Đảm bảo doc_type_code hợp lệ trong bảng document_types nếu có
            if (!string.IsNullOrWhiteSpace(data.DocTypeCode))
            {
                var normalizedCode = data.DocTypeCode.Trim().ToUpperInvariant();
                var docTypeRepo = unitOfWork.Repository<TaxDocumentType>();
                var existingDocType = (await docTypeRepo.FindAsync(t => t.Code == normalizedCode)).FirstOrDefault();
                if (existingDocType != null)
                {
                    document.DocTypeCode = existingDocType.Code;
                }
                else
                {
                    _logger.LogWarning("AI returned unknown DocTypeCode '{Code}', not in system catalog. Leaving as null for user to select.", normalizedCode);
                    document.DocTypeCode = null;
                }
            }

            // Lưu toàn bộ dữ liệu bóc tách từ AI vào bảng documents
            document.InvoiceSeries = data.InvoiceSeries;
            document.InvoiceNumber = data.InvoiceNumber;
            if (!string.IsNullOrWhiteSpace(data.InvoiceDate) && DateOnly.TryParse(data.InvoiceDate, out var invDate))
            {
                document.InvoiceDate = invDate;
            }
            if (data.ExtractedYear.HasValue)
            {
                document.ExtractedYear = (short)data.ExtractedYear.Value;
            }
            document.SellerName = data.SellerName;
            document.SellerTaxCode = data.SellerTaxCode;
            document.SellerAddress = data.SellerAddress;
            document.SellerPhone = data.SellerPhone;
            document.BuyerName = data.BuyerName;
            document.BuyerTaxCode = data.BuyerTaxCode;
            document.BuyerIdCard = data.BuyerIdCard;
            document.BuyerAddress = data.BuyerAddress;
            document.PaymentMethod = data.PaymentMethod;
            document.TotalAmount = data.TotalAmount;
            document.TotalAmountInWords = data.TotalAmountInWords;
            document.LookupUrl = data.LookupUrl;
            document.LookupCode = data.LookupCode;

            if (data.ValidationStatus != null)
            {
                document.IsYearValid = data.ValidationStatus.IsYearValid;
                document.IsIdentityValid = data.ValidationStatus.IsIdentityValid;
            }

            document.Status = string.Equals(data.Status, "FAILED", StringComparison.OrdinalIgnoreCase)
                ? "FAILED"
                : "EXTRACTED";

            docRepo.Update(document);

            // Lưu danh sách chi tiết các dòng viện phí / hàng hóa vào bảng document_items
            if (data.Items != null && data.Items.Any())
            {
                var itemRepo = unitOfWork.Repository<DocumentItem>();
                var existingItems = await itemRepo.FindAsync(i => i.DocumentId == document.Id);
                foreach (var oldItem in existingItems)
                {
                    itemRepo.Remove(oldItem);
                }

                foreach (var itemDto in data.Items)
                {
                    var item = new DocumentItem
                    {
                        DocumentId = document.Id,
                        ItemOrder = itemDto.ItemOrder,
                        ItemName = itemDto.ItemName,
                        Unit = itemDto.Unit,
                        Quantity = itemDto.Quantity,
                        UnitPrice = itemDto.UnitPrice,
                        TotalPrice = itemDto.TotalPrice
                    };
                    await itemRepo.AddAsync(item);
                }
            }

            await unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Document {DocId} full data saved to DB with status EXTRACTED. Dispatched for client review.", document.Id);
        }
    }
}
