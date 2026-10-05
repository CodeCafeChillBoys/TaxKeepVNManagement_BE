using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.ConsultationFeeConfigurations;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/consultation-fee-configs")]
    public class ConsultationFeeConfigController : ControllerBase
    {
        private readonly IConsultationFeeConfigService _service;

        public ConsultationFeeConfigController(IConsultationFeeConfigService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lấy danh sách cấu hình mức phí tư vấn (Hỗ trợ lọc sessionType, isActive)
        /// Dùng cho: Form ứng viên chuyên gia tham khảo (isActive=true) hoặc Admin quản lý
        /// GET /api/v1/consultation-fee-configs?sessionType=ONLINE_MEETING&isActive=true
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] ConsultationFeeConfigQueryParameters query)
        {
            var data = await _service.GetAllAsync(query);
            return Ok(ApiResponse<IEnumerable<ConsultationFeeConfigDto>>.Ok(data, "Lấy danh sách cấu hình mức phí thành công."));
        }

        /// <summary>
        /// Xem chi tiết một cấu hình mức phí theo Id
        /// GET /api/v1/consultation-fee-configs/{id}
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] int id)
        {
            var data = await _service.GetByIdAsync(id);
            return Ok(ApiResponse<ConsultationFeeConfigDto>.Ok(data, "Lấy chi tiết cấu hình mức phí thành công."));
        }

        /// <summary>
        /// Thêm mới cấu hình khung mức phí sàn/trần (Chỉ Admin)
        /// POST /api/v1/consultation-fee-configs
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateConsultationFeeConfigDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.CreateAsync(dto);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<ConsultationFeeConfigDto>.Ok(data, "Tạo cấu hình mức phí mới thành công."));
        }

        /// <summary>
        /// Cập nhật cấu hình khung mức phí (Chỉ Admin)
        /// PUT /api/v1/consultation-fee-configs/{id}
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdateConsultationFeeConfigDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.UpdateAsync(id, dto);
            return Ok(ApiResponse<ConsultationFeeConfigDto>.Ok(data, "Cập nhật cấu hình mức phí thành công."));
        }

        /// <summary>
        /// Xóa / Vô hiệu hóa cấu hình mức phí (Chỉ Admin)
        /// DELETE /api/v1/consultation-fee-configs/{id}
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse<bool>.Ok(true, "Xóa cấu hình mức phí thành công."));
        }
    }
}
