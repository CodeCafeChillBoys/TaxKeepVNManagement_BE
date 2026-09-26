using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVN.Infrastructure.Storage
{
    public class LocalFileStorageService : IFileStorageService
    {
        public async Task<string> SaveFileAsync(IFormFile file, string folderName)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folderName);
            
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Return relative path or fake absolute URL for now
            // Adjust base URL as needed based on configuration
            return $"/uploads/{folderName}/{uniqueFileName}";
        }

        public Task DeleteFileAsync(string fileUrl)
        {
            const string uploadsPrefix = "/uploads/";
            if (!fileUrl.StartsWith(uploadsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("URL file local không hợp lệ.", nameof(fileUrl));
            }

            var relativePath = fileUrl[1..].Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath));
            var uploadsRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!filePath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("File không nằm trong thư mục uploads.", nameof(fileUrl));
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.CompletedTask;
        }
    }
}
