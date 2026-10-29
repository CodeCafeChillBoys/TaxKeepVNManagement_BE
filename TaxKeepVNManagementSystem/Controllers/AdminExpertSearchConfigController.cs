using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.SearchConfigs;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/admin/expert-search-configs")]
    [Authorize(Roles = "admin")]
    public class AdminExpertSearchConfigController : ControllerBase
    {
        private readonly IExpertSearchConfigService _service;

        public AdminExpertSearchConfigController(IExpertSearchConfigService service)
        {
            _service = service;
        }

        /// <summary>
        /// Admin: Lấy toàn bộ cấu hình tiêu chí tìm kiếm và sắp xếp chuyên gia hiện tại
        /// </summary>
        [HttpGet(Name = "GetExpertSearchConfig")]
        [ProducesResponseType(typeof(ApiResponse<ExpertSearchCriteriaConfigDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetConfig()
        {
            var config = await _service.GetConfigAsync();
            return Ok(ApiResponse<ExpertSearchCriteriaConfigDto>.Ok(config, "Lấy cấu hình tìm kiếm chuyên gia thành công."));
        }

        /// <summary>
        /// Admin: Cập nhật cấu hình tiêu chí tìm kiếm, sắp xếp mặc định và trọng số xếp hạng chuyên gia
        /// </summary>
        [HttpPut(Name = "UpdateExpertSearchConfig")]
        [ProducesResponseType(typeof(ApiResponse<ExpertSearchCriteriaConfigDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateConfig([FromBody] ExpertSearchCriteriaConfigDto request)
        {
            var updated = await _service.UpdateConfigAsync(request);
            return Ok(ApiResponse<ExpertSearchCriteriaConfigDto>.Ok(updated, "Cập nhật cấu hình tìm kiếm thành công. Giao diện người dùng sẽ áp dụng ngay tức thì."));
        }

        /// <summary>
        /// Admin: Khôi phục cấu hình tìm kiếm về giá trị mặc định của hệ thống
        /// </summary>
        [HttpPost("reset-defaults", Name = "ResetExpertSearchConfigDefaults")]
        [ProducesResponseType(typeof(ApiResponse<ExpertSearchCriteriaConfigDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ResetDefaults()
        {
            var reset = await _service.ResetToDefaultsAsync();
            return Ok(ApiResponse<ExpertSearchCriteriaConfigDto>.Ok(reset, "Khôi phục cấu hình tìm kiếm mặc định thành công."));
        }
    }
}
