using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Requests.Dependent;
using TaxKeepVN.Application.DTOs.Responses.Dependent;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDependentService
    {
        /// <summary>
        /// Đăng ký người phụ thuộc mới cho người nộp thuế đang đăng nhập.
        /// Tự động kiểm tra trùng lặp khoảng thời gian với các đăng ký khác.
        /// </summary>
        Task<DependentResponse> CreateDependentAsync(Guid taxpayerId, CreateDependentRequest request);
    }
}
