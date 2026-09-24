using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TaxKeepVN.Application.DTOs.Law.Contract;
using TaxKeepVN.Application.Law.Common;
using TaxKeepVN.Application.Law.Normalizer;
using TaxKeepVN.Application.Law.Notification;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities.Law;
using TaxKeepVN.Infrastructure.Contexts;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    public class LawChangesetConsumerBackgroundService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LawChangesetConsumerBackgroundService> _logger;
        private const string ResponseQueueName = "law.changeset.response.queue";

        public LawChangesetConsumerBackgroundService(
            IConfiguration config,
            IServiceProvider serviceProvider,
            ILogger<LawChangesetConsumerBackgroundService> logger)
        {
            _config = config;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LawChangesetConsumerBackgroundService is starting...");

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
                            _logger.LogInformation("Received message from {Queue}: {Json}", ResponseQueueName, jsonString);

                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            var response = JsonSerializer.Deserialize<LawChangesetExtractResponse>(jsonString, options);

                            if (response != null)
                            {
                                await ProcessResponseAsync(response, jsonString);
                            }

                            channel.BasicAck(ea.DeliveryTag, false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing law changeset response message.");
                            channel.BasicNack(ea.DeliveryTag, false, requeue: false);
                        }
                    };

                    channel.BasicConsume(
                        queue: ResponseQueueName,
                        autoAck: false,
                        consumer: consumer
                    );

                    _logger.LogInformation("Listening on RabbitMQ queue: {Queue}", ResponseQueueName);

                    while (!stoppingToken.IsCancellationRequested && channel.IsOpen)
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RabbitMQ connection error in LawChangesetConsumerBackgroundService. Reconnecting in 5s...");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task ProcessResponseAsync(LawChangesetExtractResponse response, string rawJson)
        {
            if (response.SchemaVersion != 1)
            {
                _logger.LogWarning("Unsupported schemaVersion {Version} in LawChangesetExtractResponse. Expected 1.", response.SchemaVersion);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TaxKeepDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<ILawNotificationService>();

            var changeset = await db.LawChangesets
                .Include(c => c.Document)
                .Include(c => c.Ops)
                .Include(c => c.Relations)
                .FirstOrDefaultAsync(c => c.Id == response.ChangesetId);

            if (changeset == null || changeset.AiTaskId != response.TaskId || changeset.Status != LawConstants.ChangesetStatus.EXTRACTING)
            {
                _logger.LogInformation("Changeset {ChangesetId} not found, taskId mismatch or not in EXTRACTING state. Ignoring message.", response.ChangesetId);
                return;
            }

            if (string.Equals(response.Status, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                changeset.Status = LawConstants.ChangesetStatus.FAILED;
                changeset.AiErrorCode = response.ErrorCode;
                changeset.AiErrorMessage = response.ErrorMessage;
                changeset.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync();

                await notificationService.NotifyChangesetFailedAsync(
                    changeset.CreatedBy?.ToString() ?? string.Empty,
                    changeset.AiTaskId ?? Guid.Empty,
                    new
                    {
                        changesetId = changeset.Id,
                        taskId = changeset.AiTaskId,
                        documentId = changeset.DocumentId,
                        status = LawConstants.ChangesetStatus.FAILED,
                        errorCode = response.ErrorCode,
                        errorMessage = response.ErrorMessage
                    });

                return;
            }

            // SUCCESS branch
            var result = response.Result;
            if (result == null)
            {
                _logger.LogWarning("Changeset {ChangesetId} marked SUCCESS but result is null.", changeset.Id);
                return;
            }

            // 1. Fill document info
            if (changeset.Document != null && result.Document != null)
            {
                var doc = changeset.Document;
                if (string.IsNullOrWhiteSpace(doc.DocumentType) && !string.IsNullOrWhiteSpace(result.Document.DocumentType))
                    doc.DocumentType = result.Document.DocumentType;
                if (string.IsNullOrWhiteSpace(doc.Title) && !string.IsNullOrWhiteSpace(result.Document.Title))
                    doc.Title = result.Document.Title;
                if (string.IsNullOrWhiteSpace(doc.Issuer) && !string.IsNullOrWhiteSpace(result.Document.Issuer))
                    doc.Issuer = result.Document.Issuer;
                if (!doc.IssuedDate.HasValue && result.Document.IssuedDate.HasValue)
                    doc.IssuedDate = result.Document.IssuedDate;
                if (!doc.EffectiveDate.HasValue && result.Document.EffectiveDate.HasValue)
                    doc.EffectiveDate = result.Document.EffectiveDate;

                // Handle DocumentNumber if not set
                if (string.IsNullOrWhiteSpace(doc.DocumentNumber) && !string.IsNullOrWhiteSpace(result.Document.DocumentNumber))
                {
                    string rawNum = result.Document.DocumentNumber;
                    string norm = LegalDocumentNumber.Normalize(rawNum);

                    var placeholder = await db.LegalDocuments.FirstOrDefaultAsync(d => d.NumberNormalized == norm && d.IsPlaceholder);
                    if (placeholder != null)
                    {
                        // Adopt placeholder
                        placeholder.Title = doc.Title ?? placeholder.Title;
                        placeholder.Issuer = doc.Issuer ?? placeholder.Issuer;
                        placeholder.IssuedDate = doc.IssuedDate ?? placeholder.IssuedDate;
                        placeholder.EffectiveDate = doc.EffectiveDate ?? placeholder.EffectiveDate;
                        placeholder.FileUrl = doc.FileUrl;
                        placeholder.OriginalFilename = doc.OriginalFilename;
                        placeholder.SourceUrl = doc.SourceUrl;
                        placeholder.IsPlaceholder = false;

                        changeset.DocumentId = placeholder.Id;
                        db.LegalDocuments.Remove(doc);
                        changeset.Document = placeholder;
                    }
                    else
                    {
                        bool realExists = await db.LegalDocuments.AnyAsync(d => d.NumberNormalized == norm && !d.IsPlaceholder && d.Id != doc.Id);
                        if (realExists)
                        {
                            doc.DocumentNumber = rawNum;
                            doc.NumberNormalized = null; // Triggers DUPLICATE_DOCUMENT warning
                            var wList = result.Warnings ?? new List<string>();
                            if (!wList.Contains("DUPLICATE_DOCUMENT")) wList.Add("DUPLICATE_DOCUMENT");
                            result.Warnings = wList;
                        }
                        else
                        {
                            doc.DocumentNumber = rawNum;
                            doc.NumberNormalized = norm;
                            if (string.IsNullOrWhiteSpace(doc.DocumentType))
                                doc.DocumentType = LegalDocumentNumber.InferType(rawNum);
                        }
                    }
                }
            }

            // 2. Save metadata
            changeset.PagesRead = result.Coverage?.PagesRead;
            changeset.TotalPages = result.Coverage?.TotalPages;
            changeset.AiModel = response.Model;
            changeset.AiRawResponse = rawJson;
            changeset.AiWarnings = result.Warnings != null ? JsonSerializer.Serialize(result.Warnings) : null;

            // 3. Add operations (Origin = AI)
            int seq = 1;
            foreach (var op in result.Operations)
            {
                changeset.Ops.Add(new LawChangeOp
                {
                    Id = Guid.NewGuid(),
                    ChangesetId = changeset.Id,
                    Seq = seq++,
                    OpKey = op.OpKey,
                    OpType = op.Op.ToUpperInvariant(),
                    RuleCode = op.RuleCode.ToUpperInvariant(),
                    NewCode = op.NewCode,
                    ProposedDefinition = op.ProposedDefinition?.GetRawText(),
                    After = op.After?.GetRawText(),
                    ApplyFrom = op.ApplyFrom,
                    ApplyTo = op.ApplyTo,
                    ApplyBasis = op.ApplyBasis == null ? null : JsonSerializer.Serialize(op.ApplyBasis),
                    Article = op.Citation?.Article,
                    Clause = op.Citation?.Clause,
                    Point = op.Citation?.Point,
                    Page = op.Citation?.Page,
                    EvidenceText = op.Evidence,
                    Confidence = op.Confidence.HasValue ? (decimal)op.Confidence.Value : null,
                    Rationale = op.Rationale,
                    Origin = LawConstants.OpOrigin.AI,
                    Decision = LawConstants.Decision.PENDING
                });
            }

            // Add relations (Origin = AI)
            foreach (var rel in result.Relations)
            {
                changeset.Relations.Add(new LawChangesetRelation
                {
                    Id = Guid.NewGuid(),
                    ChangesetId = changeset.Id,
                    RelationKey = rel.RelationKey,
                    RelationType = rel.Type.ToUpperInvariant(),
                    TargetDocumentNumber = rel.TargetDocumentNumber,
                    TargetArticle = rel.TargetScope?.TryGetProperty("article", out var a) == true ? a.GetString() : null,
                    TargetClause = rel.TargetScope?.TryGetProperty("clause", out var cl) == true ? cl.GetString() : null,
                    TargetPoint = rel.TargetScope?.TryGetProperty("point", out var p) == true ? p.GetString() : null,
                    SourceArticle = rel.Citation?.Article,
                    SourceClause = rel.Citation?.Clause,
                    SourcePoint = rel.Citation?.Point,
                    SourcePage = rel.Citation?.Page,
                    EffectiveDate = rel.EffectiveDate,
                    EvidenceText = rel.Evidence,
                    Note = rel.Note,
                    Origin = LawConstants.OpOrigin.AI,
                    Decision = LawConstants.Decision.PENDING
                });
            }

            // 4. Run LawOpNormalizer
            var activeVersions = await db.LawRuleVersions
                .AsNoTracking()
                .Include(v => v.Document)
                .Where(v => v.CreatedInRevision <= changeset.BaseRevisionNo && (!v.SupersededInRevision.HasValue || v.SupersededInRevision.Value > changeset.BaseRevisionNo))
                .ToListAsync();

            var catalog = await db.LawRuleDefinitions.AsNoTracking().ToListAsync();

            var normalizer = new LawOpNormalizer();
            normalizer.Normalize(changeset, activeVersions, catalog, changeset.Document);

            // 5. Complete
            changeset.Status = LawConstants.ChangesetStatus.READY;
            changeset.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            // 6. SignalR OnLawChangesetReady
            await notificationService.NotifyChangesetReadyAsync(
                changeset.CreatedBy?.ToString() ?? string.Empty,
                changeset.AiTaskId ?? Guid.Empty,
                new
                {
                    changesetId = changeset.Id,
                    taskId = changeset.AiTaskId,
                    documentId = changeset.DocumentId,
                    status = LawConstants.ChangesetStatus.READY,
                    opsCount = changeset.Ops.Count,
                    relationsCount = changeset.Relations.Count,
                    warnings = result.Warnings ?? new List<string>()
                });

            _logger.LogInformation("Successfully processed LawChangesetExtractResponse for changeset {ChangesetId}", changeset.Id);
        }
    }
}
