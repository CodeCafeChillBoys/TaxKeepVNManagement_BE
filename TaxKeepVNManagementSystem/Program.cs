using System;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Application.Validators;
using TaxKeepVN.Domain.IRepositories;
using TaxKeepVN.Infrastructure.Contexts;
using TaxKeepVN.Infrastructure.Repositories;
using TaxKeepVN.Infrastructure.Services;
using TaxKeepVN.Infrastructure.Storage;
using TaxKeepVNManagementSystem.BackgroundJobs;
using TaxKeepVNManagementSystem.Hubs;
using TaxKeepVNManagementSystem.Middlewares;
using QuestPDF.Infrastructure;
using TaxKeepVN.Infrastructure.Services.Pdf;

var builder = WebApplication.CreateBuilder(args);

// ── In-Memory Cache (Tối ưu tốc độ mili-giây cho Dependent Document Rules) ──
builder.Services.AddMemoryCache();

// ── Controllers, Content Negotiation & FluentValidation ─────────────────────
builder.Services.AddControllers(options =>
{
    // Fallback to JSON when client sends accept text/plain or wildcard (prevents 406 Not Acceptable in Swagger/Clients)
    options.ReturnHttpNotAcceptable = false;
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
})
.AddXmlSerializerFormatters() // Support application/xml
.ConfigureApiBehaviorOptions(options =>
{
    // Disable automatic 400 from ModelState; let FluentValidation + Controller handle it
    options.SuppressModelStateInvalidFilter = true;
});

// Register FluentValidation validators from Application layer
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<IncomeSourceCreateValidator>();

// ── CORS (Hỗ trợ kết nối từ Android Emulator 10.0.2.2, Flutter, React Native, Web) ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ── Swagger & Security Definition ───────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TaxKeepVN API", Version = "v1" });

    // Chỉ hiển thị chú thích cho các endpoint có khai báo [EndpointSummary] (như TaxSettlement)
    c.OperationFilter<EndpointSummaryOperationFilter>();

    // Cho phép nhập JWT token trong Swagger UI
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập JWT token theo format: Bearer {token}"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            System.Array.Empty<string>()
        }
    });
});

// ── Database (PostgreSQL) ───────────────────────────────────────────────────
var connString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured in appsettings.json.");



builder.Services.AddDbContext<TaxKeepDbContext>(options =>
    options.UseNpgsql(connString));

// ── Repository & UnitOfWork ─────────────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// ── Application & Infrastructure Services ───────────────────────────────────
builder.Services.AddHttpClient<IFileStorageService, SupabaseStorageService>();
builder.Services.AddScoped<IOcrService, OcrService>();
builder.Services.AddScoped<IDependentDocumentService, DependentDocumentService>();
builder.Services.AddScoped<IDependentReminderService, DependentReminderService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IIncomeSourceService, IncomeSourceService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITokenRevocationService, TokenRevocationService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IDependentService, DependentService>();
builder.Services.AddScoped<IIncomeService, IncomeService>();
builder.Services.AddScoped<ITaxAIProducerService, TaxAIProducerService>();
builder.Services.AddScoped<IDocumentOcrProducerService, DocumentOcrProducerService>();
builder.Services.AddScoped<IDependentRuleService, DependentRuleService>();
// Services từ feature/CalculateTaxFinalization
builder.Services.AddScoped<ISystemConfigService, SystemConfigService>();
builder.Services.AddScoped<ITaxSettlementService, TaxSettlementService>();
// Services kết xuất tờ khai PDF & đóng gói ZIP hồ sơ quyết toán (WBS 3.6.T7, 3.6.T8, 3.6.T9)
builder.Services.AddScoped<IDownloadTokenService, DownloadTokenService>();
builder.Services.AddScoped<ITaxSettlementPdfService, TaxSettlementPdfService>();
builder.Services.AddScoped<ITaxSettlementPackageService, TaxSettlementPackageService>();
// Services từ feature/implementation-upload-document-ai-extraction
builder.Services.AddScoped<ITaxPeriodService, TaxPeriodService>();
builder.Services.AddScoped<ITaxDocumentTypeService, TaxDocumentTypeService>();
builder.Services.AddScoped<ITaxAiConfigService, TaxAiConfigService>();
builder.Services.AddScoped<IUrlRuleService, UrlRuleService>();
builder.Services.AddScoped<IOcrAIProducerService, OcrAIProducerService>();
builder.Services.AddScoped<ICertificateOcrProducerService, CertificateOcrProducerService>();
builder.Services.AddScoped<ITaxChatService, TaxChatService>();
builder.Services.AddScoped<IAiKeyService, AiKeyService>();
builder.Services.AddScoped<IPromptTemplateService, PromptTemplateService>();

builder.Services.AddScoped<ISpecializationService, SpecializationService>();
builder.Services.AddScoped<IExpertApplicationService, ExpertApplicationService>();
builder.Services.AddScoped<IConsultationFeeConfigService, ConsultationFeeConfigService>();
builder.Services.AddScoped<IExpertCertificateConfigService, ExpertCertificateConfigService>();
builder.Services.AddScoped<IExpertSearchConfigService, ExpertSearchConfigService>();
builder.Services.AddScoped<IExpertSearchService, ExpertSearchService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IPaymentEscrowService, PaymentEscrowService>();
builder.Services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
builder.Services.AddScoped<IUserSubscriptionService, UserSubscriptionService>();
builder.Services.AddScoped<ISubscriptionQuotaService, SubscriptionQuotaService>();
builder.Services.AddScoped<IAdminSubscriptionService, AdminSubscriptionService>();
builder.Services.AddScoped<IConsultationRealtimeNotifier, TaxKeepVNManagementSystem.Services.ConsultationRealtimeNotifier>();
builder.Services.AddScoped<IConsultationService, ConsultationService>();
builder.Services.AddScoped<IExpertSlotService, ExpertSlotService>();
builder.Services.AddScoped<IDisbursementDisputeService, DisbursementDisputeService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<IPermissionCacheService, RedisPermissionCacheService>();
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

// ── Upstash Redis Cache ─────────────────────────────────────────────────────
var redisConfig = builder.Configuration.GetSection("Redis");
var redisEnabled = Environment.GetEnvironmentVariable("REDIS_ENABLED") != null
    ? bool.TryParse(Environment.GetEnvironmentVariable("REDIS_ENABLED"), out var envRedisBool) && envRedisBool
    : redisConfig.GetValue<bool>("Enabled", true);

var redisConnStr = Environment.GetEnvironmentVariable("REDIS_URL") 
                ?? redisConfig["ConnectionString"]
                ?? redisConfig["Url"];

if (redisEnabled && !string.IsNullOrWhiteSpace(redisConnStr))
{
    try
    {
        ConfigurationOptions redisOptions;
        if (redisConnStr.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase) ||
            redisConnStr.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(redisConnStr);
            var userInfo = uri.UserInfo.Split(':');
            var password = userInfo.Length > 1 ? userInfo[1] : userInfo[0];

            redisOptions = new ConfigurationOptions
            {
                EndPoints = { { uri.Host, uri.Port > 0 ? uri.Port : 6379 } },
                Password = password,
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase),
                AbortOnConnectFail = false,
                ConnectTimeout = 5000,
                SyncTimeout = 5000
            };
        }
        else
        {
            redisOptions = ConfigurationOptions.Parse(redisConnStr);
            redisOptions.AbortOnConnectFail = false;
        }

        var multiplexer = ConnectionMultiplexer.Connect(redisOptions);
        builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Redis Warning] Không thể kết nối Upstash Redis: {ex.Message}. Ứng dụng sẽ tự động fallback sang In-Memory Cache.");
    }
}
// ── HTTP Clients ────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("TaxAIService", client =>
{
    var baseUrl = builder.Configuration["Services:TaxAIService:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(baseUrl);
});

// ── SignalR Real-Time ────────────────────────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, CustomUserIdProvider>();
// ── JWT Authentication ───────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? "TaxKeepVN@SecretKey#2026!Must32CharsLong";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"] ?? "TaxKeepVN",
            ValidAudience = jwtSection["Audience"] ?? "TaxKeepVNClient",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Kiểm tra JTI có trong blacklist không sau khi token hợp lệ về chữ ký
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var revocationService = context.HttpContext.RequestServices
                    .GetRequiredService<ITokenRevocationService>();

                var jti = context.Principal?
                    .FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;

                if (!string.IsNullOrEmpty(jti) && await revocationService.IsRevokedAsync(jti))
                {
                    context.Fail("Token đã bị thu hồi. Vui lòng đăng nhập lại.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

// ── Background Jobs ─────────────────────────────────────────────────────────
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.AgeTransitionReminderJob>();
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.TaxAIConsumerBackgroundService>();
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.OcrAIConsumerBackgroundService>();
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.CertificateOcrConsumerBackgroundService>();
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.DocumentOcrConsumerBackgroundService>();
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.TaxSettlementReminderJob>();
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.BookingExpirationBackgroundService>();
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.ConsultationNoShowBackgroundService>();
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.ConsultationMeetingReminderJob>();
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.EscrowCooldownDisbursementBackgroundService>();
builder.Services.AddHostedService<TaxKeepVNManagementSystem.BackgroundJobs.SubscriptionExpirationAndResetJob>();

// ── QuestPDF Community License Configuration (WBS 3.6.T7) ───────────────────
QuestPDF.Settings.License = LicenseType.Community;
QuestPDF.Settings.UseSystemFonts = true;
QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

var app = builder.Build();

// ── Middleware Pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();

// ── Enable CORS ──────────────────────────────────────────────────────────────
app.UseCors("AllowAll");

// Chỉ chuyển hướng HTTPS khi production để không làm gián đoạn HTTP từ Android Emulator (10.0.2.2:5023)
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TaxAIHub>("/hubs/tax-ai");
app.MapHub<TaxKeepVNManagementSystem.Hubs.ConsultationHub>("/hubs/consultation");

// ── Startup Cache Warmup: Nạp dữ liệu các tính năng vào Redis Cache ───────────
using (var scope = app.Services.CreateScope())
{
    try
    {
        var cacheService = scope.ServiceProvider.GetService<IPermissionCacheService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaxKeepDbContext>();
        if (cacheService != null)
        {
            var roleIds = await dbContext.Roles.Select(r => r.Id).ToListAsync();
            foreach (var roleId in roleIds)
            {
                await cacheService.WarmupRolePermissionsAsync(roleId);
            }
            Console.WriteLine($"[Redis Info] Đã nạp thành công cache phân quyền cho {roleIds.Count} vai trò vào Redis Cache.");
        }

        // 1. Warmup danh sách gói dịch vụ Subscription
        var planService = scope.ServiceProvider.GetService<ISubscriptionPlanService>();
        if (planService != null)
        {
            var plans = await planService.GetAllActivePlansAsync();
            Console.WriteLine($"[Redis Info] Đã nạp thành công {plans.Count} gói dịch vụ vào Redis Cache.");
        }

        // 2. Warmup danh mục chuyên môn Specializations
        var specService = scope.ServiceProvider.GetService<ISpecializationService>();
        if (specService != null)
        {
            var specs = await specService.GetAllAsync();
            Console.WriteLine($"[Redis Info] Đã nạp thành công danh mục chuyên môn vào Redis Cache.");
        }

        // 3. Warmup danh mục nhóm người phụ thuộc
        var depRuleService = scope.ServiceProvider.GetService<IDependentRuleService>();
        if (depRuleService != null)
        {
            var groups = await depRuleService.GetActiveGroupsAsync();
            Console.WriteLine($"[Redis Info] Đã nạp thành công danh mục nhóm người phụ thuộc vào Redis Cache.");
        }

        // 4. Warmup cấu hình khung phí tư vấn
        var feeService = scope.ServiceProvider.GetService<IConsultationFeeConfigService>();
        if (feeService != null)
        {
            await feeService.GetAllAsync();
            Console.WriteLine($"[Redis Info] Đã nạp thành công cấu hình khung phí tư vấn vào Redis Cache.");
        }

        // 5. Warmup cấu hình chứng chỉ chuyên gia
        var certConfigService = scope.ServiceProvider.GetService<IExpertCertificateConfigService>();
        if (certConfigService != null)
        {
            await certConfigService.GetAllAsync();
            Console.WriteLine($"[Redis Info] Đã nạp thành công cấu hình loại chứng chỉ vào Redis Cache.");
        }

        // 6. Warmup danh mục loại chứng từ thuế
        var docTypeService = scope.ServiceProvider.GetService<ITaxDocumentTypeService>();
        if (docTypeService != null)
        {
            await docTypeService.GetAllAsync();
            Console.WriteLine($"[Redis Info] Đã nạp thành công danh mục loại chứng từ thuế vào Redis Cache.");
        }

        // 7. Warmup cấu hình tìm kiếm chuyên gia
        var expertSearchConfigService = scope.ServiceProvider.GetService<IExpertSearchConfigService>();
        if (expertSearchConfigService != null)
        {
            await expertSearchConfigService.GetConfigAsync();
            Console.WriteLine($"[Redis Info] Đã nạp thành công cấu hình tìm kiếm chuyên gia vào Redis Cache.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Redis Warning] Không thể nạp trước dữ liệu vào Redis lúc khởi động: {ex.Message}");
    }
}

app.Run();

/// <summary>
/// OperationFilter chỉ nạp chú thích tóm tắt cho các endpoint có gắn [EndpointSummary] (VD: TaxSettlementController).
/// Các controller khác (như Dependent, Notification...) sẽ không bị hiển thị chú thích.
/// </summary>
public class EndpointSummaryOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        var summaryAttr = context.MethodInfo.GetCustomAttributes(typeof(EndpointSummaryAttribute), false)
            .FirstOrDefault() as EndpointSummaryAttribute;

        if (summaryAttr != null)
        {
            operation.Summary = summaryAttr.Summary;
        }
    }
}
