using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.TaxSettlement;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVN.Infrastructure.Services
{
    public class DownloadTokenService : IDownloadTokenService
    {
        private readonly byte[] _keyBytes;
        private readonly ILogger<DownloadTokenService> _logger;

        public DownloadTokenService(IConfiguration configuration, ILogger<DownloadTokenService> logger)
        {
            _logger = logger;
            var secretKey = configuration["Jwt:Key"] ?? "TaxKeepVN@SecretKey#2026!Must32CharsLong";
            _keyBytes = Encoding.UTF8.GetBytes(secretKey);
        }

        public string GenerateToken(DownloadTokenPayload payload, TimeSpan? validity = null)
        {
            var span = validity ?? TimeSpan.FromMinutes(30);
            payload.ExpiresAt = DateTime.UtcNow.Add(span);

            var json = JsonSerializer.Serialize(payload);
            var payloadBase64 = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));

            using var hmac = new HMACSHA256(_keyBytes);
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
            var signatureBase64 = WebEncoders.Base64UrlEncode(signatureBytes);

            return $"{payloadBase64}.{signatureBase64}";
        }

        public DownloadTokenPayload? ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var parts = token.Split('.');
            if (parts.Length != 2) return null;

            var payloadPart = parts[0];
            var signaturePart = parts[1];

            try
            {
                using var hmac = new HMACSHA256(_keyBytes);
                var expectedSignatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadPart));
                var expectedSignature = WebEncoders.Base64UrlEncode(expectedSignatureBytes);

                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(signaturePart),
                    Encoding.UTF8.GetBytes(expectedSignature)))
                {
                    _logger.LogWarning("Download token bị từ chối: Chữ ký HMAC không khớp.");
                    return null;
                }

                var jsonBytes = WebEncoders.Base64UrlDecode(payloadPart);
                var json = Encoding.UTF8.GetString(jsonBytes);
                var payload = JsonSerializer.Deserialize<DownloadTokenPayload>(json);

                if (payload == null) return null;

                if (DateTime.UtcNow > payload.ExpiresAt)
                {
                    _logger.LogWarning("Download token cho DossierId '{DossierId}' đã hết hạn vào {ExpiresAt:u}.", payload.DossierId, payload.ExpiresAt);
                    return null;
                }

                return payload;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi giải mã download token: {Message}", ex.Message);
                return null;
            }
        }
    }
}
