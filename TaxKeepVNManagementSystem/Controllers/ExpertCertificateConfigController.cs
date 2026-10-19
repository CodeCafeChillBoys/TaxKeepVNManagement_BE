using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.ExpertCertificateConfigurations;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    public class ExpertCertificateConfigController : ControllerBase
    {
        private readonly IExpertCertificateConfigService _service;

        public ExpertCertificateConfigController(IExpertCertificateConfigService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lấy danh sách loại chứng chỉ đang hoạt động (Dành cho App Mobile & Web hiển thị Form chọn chứng chỉ)
        /// GET /api/v1/expert-certificate-configs
        /// </summary>
        [HttpGet("api/v1/expert-certificate-configs")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActiveConfigs()
        {
            var data = await _service.GetAllAsync(new ExpertCertificateConfigQueryParameters { IsActive = true });
            return Ok(ApiResponse<IEnumerable<ExpertCertificateConfigDto>>.Ok(data, "Lấy danh mục chứng chỉ áp dụng thành công."));
        }

        /// <summary>
        /// Admin: Lấy toàn bộ danh sách cấu hình loại chứng chỉ (hỗ trợ lọc IsActive, search từ khóa)
        /// GET /api/v1/admin/expert-certificate-configs
        /// </summary>
        [HttpGet("api/v1/admin/expert-certificate-configs")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AdminGetAll([FromQuery] ExpertCertificateConfigQueryParameters query)
        {
            var data = await _service.GetAllAsync(query);
            return Ok(ApiResponse<IEnumerable<ExpertCertificateConfigDto>>.Ok(data, "Lấy danh sách cấu hình loại chứng chỉ thành công."));
        }

        /// <summary>
        /// Admin: Xem chi tiết cấu hình chứng chỉ theo Id
        /// GET /api/v1/admin/expert-certificate-configs/{id}
        /// </summary>
        [HttpGet("api/v1/admin/expert-certificate-configs/{id:int}")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] int id)
        {
            var data = await _service.GetByIdAsync(id);
            return Ok(ApiResponse<ExpertCertificateConfigDto>.Ok(data, "Lấy chi tiết cấu hình loại chứng chỉ thành công."));
        }

        /// <summary>
        /// Admin: Thêm mới một loại chứng chỉ hành nghề
        /// POST /api/v1/admin/expert-certificate-configs
        /// </summary>
        [HttpPost("api/v1/admin/expert-certificate-configs")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateExpertCertificateConfigRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.CreateAsync(request);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<ExpertCertificateConfigDto>.Ok(data, "Tạo cấu hình loại chứng chỉ mới thành công."));
        }

        /// <summary>
        /// Admin: Cập nhật thông tin loại chứng chỉ
        /// PUT /api/v1/admin/expert-certificate-configs/{id}
        /// </summary>
        [HttpPut("api/v1/admin/expert-certificate-configs/{id:int}")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdateExpertCertificateConfigRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.UpdateAsync(id, request);
            return Ok(ApiResponse<ExpertCertificateConfigDto>.Ok(data, "Cập nhật cấu hình loại chứng chỉ thành công."));
        }

        /// <summary>
        /// Admin: Bật / Tắt trạng thái kích hoạt loại chứng chỉ (Toggle IsActive)
        /// PATCH /api/v1/admin/expert-certificate-configs/{id}/toggle-active
        /// </summary>
        [HttpPatch("api/v1/admin/expert-certificate-configs/{id:int}/toggle-active")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ToggleActive([FromRoute] int id)
        {
            var data = await _service.ToggleActiveAsync(id);
            return Ok(ApiResponse<ExpertCertificateConfigDto>.Ok(data, "Thay đổi trạng thái loại chứng chỉ thành công."));
        }

        /// <summary>
        /// Admin: Xóa loại chứng chỉ (chỉ được xóa nếu chưa có hồ sơ nào dùng)
        /// DELETE /api/v1/admin/expert-certificate-configs/{id}
        /// </summary>
        [HttpDelete("api/v1/admin/expert-certificate-configs/{id:int}")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse<bool>.Ok(true, "Xóa loại chứng chỉ thành công."));
        }
    }
}
