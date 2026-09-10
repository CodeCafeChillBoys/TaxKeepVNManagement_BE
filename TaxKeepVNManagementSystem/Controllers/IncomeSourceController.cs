using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.IncomeSources;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/income-sources")]
    // [Authorize(Roles = "TAXPAYER")]
    public class IncomeSourceController : ControllerBase
    {
        private readonly IIncomeSourceService _service;

        public IncomeSourceController(IIncomeSourceService service)
        {
            _service = service;
        }

        // GET /api/v1/income-sources?page=1&size=10&search=cty&sort=-createdAt
        [HttpGet(Name = "GetIncomeSources")]
        public async Task<IActionResult> GetAll([FromQuery] QueryParameters query)
        {
            Guid userId = GetMockUserId();
            var data = await _service.GetAllByUserIdAsync(userId, query);
            return Ok(ApiResponse<object>.Ok(data, "Lấy danh sách nơi chi trả thu nhập thành công."));
        }

        // GET /api/v1/income-sources/{id}
        [HttpGet("{id:guid}", Name = "GetIncomeSourceById")]
        public async Task<IActionResult> GetById(Guid id)
        {
            Guid userId = GetMockUserId();
            var data = await _service.GetByIdAsync(id, userId);
            return Ok(ApiResponse<object>.Ok(data, "Lấy thông tin nơi chi trả thu nhập thành công."));
        }

        // POST /api/v1/income-sources
        [HttpPost(Name = "CreateIncomeSource")]
        public async Task<IActionResult> Create([FromBody] IncomeSourceCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "Dữ liệu không hợp lệ."));

            Guid userId = GetMockUserId();
            var data = await _service.CreateAsync(userId, dto);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<object>.Ok(data, "Khai báo nơi chi trả thu nhập thành công."));
        }

        // PUT /api/v1/income-sources/{id}
        [HttpPut("{id:guid}", Name = "UpdateIncomeSource")]
        public async Task<IActionResult> Update(Guid id, [FromBody] IncomeSourceUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "Dữ liệu không hợp lệ."));

            Guid userId = GetMockUserId();
            var data = await _service.UpdateAsync(id, userId, dto);
            return Ok(ApiResponse<object>.Ok(data, "Cập nhật nơi chi trả thu nhập thành công."));
        }

        // DELETE /api/v1/income-sources/{id}
        [HttpDelete("{id:guid}", Name = "DeleteIncomeSource")]
        public async Task<IActionResult> Delete(Guid id)
        {
            Guid userId = GetMockUserId();
            await _service.DeleteAsync(id, userId);
            return Ok(ApiResponse<object>.Ok(null, "Xóa nơi chi trả thu nhập thành công."));
        }

        private static Guid GetMockUserId()
        {
            _ = Guid.TryParse("c1234567-89ab-cdef-0123-456789abcdef", out Guid userId);
            return userId;
        }
    }
}
