using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TaxKeepVN.Application.DTOs.OcrAI;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using TaxKeepVNManagementSystem.Hubs;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    public class CertificateOcrConsumerBackgroundService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly IHubContext<TaxAIHub> _hubContext;
        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CertificateOcrConsumerBackgroundService> _logger;
        private const string ResponseQueueName = "certificate.ocr.ai.response.queue";

        public CertificateOcrConsumerBackgroundService(
            IConfiguration config,
            IHubContext<TaxAIHub> hubContext,
            IMemoryCache cache,
            IServiceScopeFactory scopeFactory,
            ILogger<CertificateOcrConsumerBackgroundService> logger)
        {
            _config = config;
            _hubContext = hubContext;
            _cache = cache;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CertificateOcrConsumerBackgroundService is starting...");

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
                            _logger.LogInformation("Received Certificate OCR response: {Json}", jsonString);

                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            var response = JsonSerializer.Deserialize<CertificateOcrResponseMessage>(jsonString, options);

                            if (response != null)
                            {
                                // 1. Lưu kết quả vào Cache trong 30 phút để FE polling nếu không dùng SignalR
                                _cache.Set($"cert_ocr_task_{response.TaskId}", response, TimeSpan.FromMinutes(30));

                                // 2. Tự động đối chiếu chéo (Cross-Verification) với tài khoản User và Hồ sơ chuyên gia
                                if (response.UserId.HasValue || response.CertificateId.HasValue || response.ApplicationId.HasValue)
                                {
                                    await PerformAutoVerificationAsync(response);
                                }

                                var eventName = response.Success
                                    ? "OnCertificateOcrCompleted"
                                    : "OnCertificateOcrFailed";

                                // 3. Bắn SignalR thông báo theo UserId
                                if (response.UserId.HasValue)
                                {
                                    await _hubContext.Clients.User(response.UserId.Value.ToString())
                                        .SendAsync(eventName, response, stoppingToken);
                                }

                                // 4. Bắn SignalR theo nhóm TaskId
                                await _hubContext.Clients.Group($"task_{response.TaskId}")
                                    .SendAsync(eventName, response, stoppingToken);
                            }

                            channel.BasicAck(ea.DeliveryTag, multiple: false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Lỗi khi xử lý message từ Certificate OCR response queue");
                            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                        }
                    };

                    channel.BasicConsume(
                        queue: ResponseQueueName,
                        autoAck: false,
                        consumer: consumer
                    );

                    _logger.LogInformation("CertificateOcrConsumerBackgroundService started listening on: {Queue}", ResponseQueueName);

                    while (!stoppingToken.IsCancellationRequested && connection.IsOpen)
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Mất kết nối tới RabbitMQ ({Error}). Đang thử kết nối lại sau 5s...", ex.Message);
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task PerformAutoVerificationAsync(CertificateOcrResponseMessage response)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var certRepo = unitOfWork.Repository<ExpertApplicationCertificate>();

                // 1. Tìm đúng bản ghi chứng chỉ cần cập nhật
                ExpertApplicationCertificate? targetCert = null;
                if (response.CertificateId.HasValue)
                {
                    targetCert = await certRepo.GetByIdAsync(response.CertificateId.Value);
                }
                // nếu targetCert ko có trong DB và  ApplicationId có trong xuất hiện trong DB 
                if (targetCert == null && response.ApplicationId.HasValue)
                {
                    var certs = await certRepo.FindAsync(c => c.ApplicationId == response.ApplicationId.Value);
                    targetCert = certs.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
                }

                if (targetCert == null)
                {
                    _logger.LogWarning("Không tìm thấy ExpertApplicationCertificate cho TaskId: {TaskId}", response.TaskId);
                    return;
                }

                // 2. Nếu AI báo thất bại hoặc ảnh không phải chứng chỉ (hóa đơn, selfie, CCCD, mờ...)
                if (!response.Success || response.Data == null)
                {
                    targetCert.VerificationStatus = CertificateVerificationStatus.Rejected;
                    targetCert.VerificationSource = "AI_OCR_GEMINI_RABBITMQ";
                    targetCert.VerificationNote = $"AI từ chối: {response.Message}";
                    targetCert.VerifiedAt = DateTimeOffset.UtcNow;
                    certRepo.Update(targetCert);
                    await unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Đã cập nhật từ chối chứng chỉ {CertId} do AI báo lỗi: {Msg}",
                        targetCert.Id, response.Message);
                    return;
                }

                // 3. Nếu AI bóc tách thành công -> Thực hiện đối soát chéo với thông tin User
                User? user = null;
                if (response.UserId.HasValue)
                {
                    var userRepo = unitOfWork.Repository<User>();
                    user = await userRepo.GetByIdAsync(response.UserId.Value);
                }

                var data = response.Data;
                var isNameMatch = user != null &&
                                  !string.IsNullOrWhiteSpace(data.FullName) &&
                                  StringComparisonHelper.IsNameMatching(user.FullName, data.FullName);

                var isCitizenMatch = true;
                if (user != null && !string.IsNullOrWhiteSpace(data.CitizenId) && !string.IsNullOrWhiteSpace(user.CitizenId))
                {
                    isCitizenMatch = string.Equals(user.CitizenId.Trim(), data.CitizenId.Trim(), StringComparison.OrdinalIgnoreCase);
                }

                _logger.LogInformation(
                    "Kết quả đối soát chứng chỉ {CertId} (TaskId: {TaskId}): NameMatch={NameMatch}, CitizenMatch={CitizenMatch}, DetectedType={Type}, IsValidType={IsValidType}",
                    targetCert.Id, response.TaskId, isNameMatch, isCitizenMatch, data.DetectedType, data.IsValidType
                );

                if (isNameMatch && isCitizenMatch && data.IsValidType)
                {
                    targetCert.VerificationStatus = CertificateVerificationStatus.Verified;
                    targetCert.VerificationSource = "AI_OCR_GEMINI_RABBITMQ";
                    targetCert.VerificationNote = $"AI tự động xác thực thành công. Khớp Họ tên ('{data.FullName}')" +
                                                  (!string.IsNullOrWhiteSpace(data.CitizenId) ? $" & Số CCCD ('{data.CitizenId}')" : "") +
                                                  $". Loại: {data.DetectedType}. Độ tin cậy: {data.OverallConfidence:P0}.";
                }
                else if (!isNameMatch)
                {
                    targetCert.VerificationStatus = CertificateVerificationStatus.Rejected;
                    targetCert.VerificationSource = "AI_OCR_GEMINI_RABBITMQ";
                    targetCert.VerificationNote = $"AI từ chối: Họ tên trên chứng chỉ ('{data.FullName}') không trùng khớp với họ tên tài khoản ('{user?.FullName}').";
                }
                else if (!isCitizenMatch)
                {
                    targetCert.VerificationStatus = CertificateVerificationStatus.Rejected;
                    targetCert.VerificationSource = "AI_OCR_GEMINI_RABBITMQ";
                    targetCert.VerificationNote = $"AI từ chối: Số CCCD trên chứng chỉ ('{data.CitizenId}') không khớp với CCCD đăng ký tài khoản ('{user?.CitizenId}').";
                }
                else if (!data.IsValidType)
                {
                    targetCert.VerificationStatus = CertificateVerificationStatus.Rejected;
                    targetCert.VerificationSource = "AI_OCR_GEMINI_RABBITMQ";
                    targetCert.VerificationNote = $"AI từ chối: Loại chứng chỉ phát hiện ('{data.DetectedType}') không nằm trong danh mục chứng chỉ được chấp nhận của hệ thống.";
                }

                targetCert.VerifiedAt = DateTimeOffset.UtcNow;
                certRepo.Update(targetCert);
                await unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đối soát tự động chứng chỉ trong Background Consumer");
            }
        }
    }
}
