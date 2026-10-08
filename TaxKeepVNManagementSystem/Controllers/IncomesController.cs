using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Requests.Income;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.Responses.Income;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/incomes")]
    [Authorize]
    public class IncomesController : ControllerBase
    {
        private readonly IIncomeService _incomeService;

        public IncomesController(IIncomeService incomeService)
        {
            _incomeService = incomeService;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("INVALID_TOKEN", "Không thể xác định danh tính người dùng từ token.");
            }
            return userId;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateIncome([FromBody] CreateIncomeRequest request)
        {
            var userId = GetUserId();
            var result = await _incomeService.CreateIncomeAsync(userId, request);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<IncomeResponseDto>.Ok(result, "Thêm thu nhập thành công."));
        }

        [HttpGet("my-incomes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyIncomes([FromQuery] int year)
        {
            var userId = GetUserId();
            var result = await _incomeService.GetMyIncomesAsync(userId, year);
            return Ok(ApiResponse<object>.Ok(result, "Lấy danh sách thu nhập thành công."));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetIncomeById(Guid id)
        {
            var userId = GetUserId();
            var result = await _incomeService.GetIncomeByIdAsync(userId, id);
            return Ok(ApiResponse<IncomeResponseDto>.Ok(result, "Lấy thông tin thu nhập thành công."));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateIncome(Guid id, [FromBody] UpdateIncomeRequest request)
        {
            var userId = GetUserId();
            var result = await _incomeService.UpdateIncomeAsync(userId, id, request);
            return Ok(ApiResponse<IncomeResponseDto>.Ok(result, "Cập nhật thu nhập thành công."));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteIncome(Guid id)
        {
            var userId = GetUserId();
            await _incomeService.DeleteIncomeAsync(userId, id);
            return NoContent();
        }
    }
}
