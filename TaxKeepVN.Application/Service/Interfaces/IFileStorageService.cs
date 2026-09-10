using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, string folderName);
    }
}
