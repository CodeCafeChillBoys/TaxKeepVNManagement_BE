using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Application.Validators;
using TaxKeepVN.Domain.IRepositories;
using TaxKeepVN.Infrastructure.Contexts;
using TaxKeepVN.Infrastructure.Repositories;
using TaxKeepVN.Infrastructure.Services;
using TaxKeepVN.Infrastructure.Storage;
using TaxKeepVNManagementSystem.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers, Content Negotiation & FluentValidation ─────────────────────
builder.Services.AddControllers(options =>
{
    // Return 406 Not Acceptable if client requests unsupported format
    options.ReturnHttpNotAcceptable = true;
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
    ?? "Host=localhost;Database=TaxKeepVNDB;Username=postgres;Password=12345";

builder.Services.AddDbContext<TaxKeepDbContext>(options =>
    options.UseNpgsql(connString));

// ── Repository & UnitOfWork ─────────────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// ── Application & Infrastructure Services ───────────────────────────────────
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IDependentDocumentService, DependentDocumentService>();
builder.Services.AddScoped<IDependentReminderService, DependentReminderService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IIncomeSourceService, IncomeSourceService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITokenRevocationService, TokenRevocationService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IDependentService, DependentService>();
builder.Services.AddScoped<ITaxAIProducerService, TaxAIProducerService>();
builder.Services.AddScoped<IDependentRuleService, DependentRuleService>();

// ── JWT Authentication ───────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];

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

var app = builder.Build();

// ── Middleware Pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();

// ── Enable CORS ──────────────────────────────────────────────────────────────
app.UseCors("AllowAll");

// Chỉ chuyển hướng HTTPS khi production để không làm gián đoạn HTTP từ Android Emulator (10.0.2.2:5023)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
