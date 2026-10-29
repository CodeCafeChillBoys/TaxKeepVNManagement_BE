using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.Specializations;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/specializations")]
    public class SpecializationController : ControllerBase
    {
        private readonly ISpecializationService _service;

        public SpecializationController(ISpecializationService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lấy danh sách lĩnh vực chuyên môn (Hỗ trợ lọc search và isActive)
        /// Dùng cho: Form ứng viên chọn (isActive=true) hoặc Admin quản lý
        /// GET /api/v1/specializations?isActive=true&search=thuế
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] SpecializationQueryParameters query)
        {
            var data = await _service.GetAllAsync(query);
            return Ok(ApiResponse<IEnumerable<SpecializationDto>>.Ok(data, "Lấy danh sách lĩnh vực chuyên môn thành công."));
        }

        /// <summary>
        /// Xem chi tiết một lĩnh vực theo Id
        /// GET /api/v1/specializations/{id}
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] int id)
        {
            var data = await _service.GetByIdAsync(id);
            return Ok(ApiResponse<SpecializationDto>.Ok(data, "Lấy thông tin chi tiết thành công."));
        }

        /// <summary>
        /// Thêm mới lĩnh vực chuyên môn (Chỉ Admin)
        /// POST /api/v1/specializations
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateSpecializationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.CreateAsync(dto);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<SpecializationDto>.Ok(data, "Tạo lĩnh vực chuyên môn mới thành công."));
        }

        /// <summary>
        /// Cập nhật lĩnh vực chuyên môn (Chỉ Admin)
        /// PUT /api/v1/specializations/{id}
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdateSpecializationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.UpdateAsync(id, dto);
            return Ok(ApiResponse<SpecializationDto>.Ok(data, "Cập nhật lĩnh vực chuyên môn thành công."));
        }

        /// <summary>
        /// Xóa / Vô hiệu hóa lĩnh vực chuyên môn (Chỉ Admin)
        /// DELETE /api/v1/specializations/{id}
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse<bool>.Ok(true, "Xóa lĩnh vực chuyên môn thành công."));
        }
    }
}