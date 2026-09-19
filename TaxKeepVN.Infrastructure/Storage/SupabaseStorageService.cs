using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVN.Infrastructure.Storage
{
    public class SupabaseStorageService : IFileStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly string _supabaseUrl;
        private readonly string _apiKey;
        private readonly string _bucketName;
        private readonly ILogger<SupabaseStorageService> _logger;

        public SupabaseStorageService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<SupabaseStorageService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _supabaseUrl = (configuration["Supabase:Url"] ?? string.Empty).TrimEnd('/');
            _apiKey = configuration["Supabase:ApiKey"] ?? string.Empty;
            _bucketName = configuration["Supabase:BucketName"] ?? "taxkeep-documents";

            if (string.IsNullOrEmpty(_supabaseUrl))
            {
                throw new InvalidOperationException("Cấu hình 'Supabase:Url' không được để trống trong appsettings.json.");
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Cấu hình 'Supabase:ApiKey' không được để trống trong appsettings.json.");
            }
        }

        public async Task<string> SaveFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File rỗng hoặc không tồn tại.", nameof(file));
            }

            // 1. Tạo tên file duy nhất tránh trùng lặp
            var cleanFileName = Path.GetFileName(file.FileName).Replace(" ", "_");
            var uniqueFileName = $"{Guid.NewGuid()}_{cleanFileName}";
            var sanitizedFolderName = folderName?.Trim().Trim('/') ?? "default";
            var storagePath = $"{sanitizedFolderName}/{uniqueFileName}";

            // 2. Endpoint upload của Supabase REST API:
            // POST {supabaseUrl}/storage/v1/object/{bucketName}/{storagePath}
            var uploadUrl = $"{_supabaseUrl}/storage/v1/object/{_bucketName}/{storagePath}";

            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            request.Headers.Add("apikey", _apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                // Gán binary content của file vào request body
                using var stream = file.OpenReadStream();
                using var content = new StreamContent(stream);
                var mimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
                content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                request.Content = content;

            // 3. Gửi request lên Supabase
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi upload file lên Supabase Storage. Status: {StatusCode}, Body: {ErrorBody}",
                    response.StatusCode, errorBody);
                throw new InvalidOperationException($"Lỗi lưu file lên Supabase Storage ({response.StatusCode}): {errorBody}");
            }

            // 4. Trả về Public URL hoàn chỉnh (HTTPS)
            var publicUrl = $"{_supabaseUrl}/storage/v1/object/public/{_bucketName}/{storagePath}";
            _logger.LogInformation("Upload thành công lên Supabase Storage: {PublicUrl}", publicUrl);

            return publicUrl;
        }
    }
}
