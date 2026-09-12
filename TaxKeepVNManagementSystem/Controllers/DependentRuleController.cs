using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.Rules;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/dependent-rules")]
    public class DependentRuleController : ControllerBase
    {
        private readonly IDependentRuleService _service;

        public DependentRuleController(IDependentRuleService service)
        {
            _service = service;
        }

        // GET /api/v1/dependent-rules?targetGroup=CHILD_UNDER_18&isActive=true&page=1&size=10&search=birth&sort=docType
        [HttpGet(Name = "GetDependentRules")]
        public async Task<IActionResult> GetAll([FromQuery] DependentRuleQueryParameters query)
        {
            var data = await _service.GetAllAsync(query);
            return Ok(ApiResponse<object>.Ok(data, "Lấy danh sách quy tắc hồ sơ người phụ thuộc thành công."));
        }

        // GET /api/v1/dependent-rules/{id}
        [HttpGet("{id:guid}", Name = "GetDependentRuleById")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var data = await _service.GetByIdAsync(id);
            return Ok(ApiResponse<object>.Ok(data, "Lấy chi tiết quy tắc hồ sơ người phụ thuộc thành công."));
        }

        // POST /api/v1/dependent-rules
        [HttpPost(Name = "CreateDependentRule")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateDependentRuleDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.CreateAsync(dto);
            return CreatedAtRoute("GetDependentRuleById", new { id = data.RuleId },
                ApiResponse<object>.Ok(data, "Thêm mới quy tắc hồ sơ người phụ thuộc thành công."));
        }

        // PUT /api/v1/dependent-rules/{id}
        [HttpPut("{id:guid}", Name = "UpdateDependentRule")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateDependentRuleDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.UpdateAsync(id, dto);
            return Ok(ApiResponse<object>.Ok(data, "Cập nhật quy tắc hồ sơ người phụ thuộc thành công."));
        }

        // DELETE /api/v1/dependent-rules/{id}
        [HttpDelete("{id:guid}", Name = "DeleteDependentRule")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse<object>.Ok(null, "Xóa quy tắc hồ sơ người phụ thuộc thành công."));
        }
    }
}
