using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.MedicalClaims;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVNManagementSystem.Attributes;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/admin/bhyt-catalog")]
    [Authorize]
    public class AdminBhytCatalogController : ControllerBase
    {
        private readonly IBhytCatalogService _catalogService;

        public AdminBhytCatalogController(IBhytCatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        /// <summary>
        /// Danh sách danh mục thuốc, dịch vụ kỹ thuật, vật tư y tế thuộc phạm vi BHYT chi trả.
        /// GET /api/v1/admin/bhyt-catalog
        /// </summary>
        [HttpGet]
        [HasPermission("system_config.view")]
        public async Task<IActionResult> GetCatalog(
            [FromQuery] string? type,
            [FromQuery] string? search,
            [FromQuery] bool? isActive)
        {
            var result = await _catalogService.GetCatalogAsync(type, search, isActive);
            return Ok(ApiResponse<List<BhytCatalogItemDto>>.Ok(result, "Lấy danh mục BHYT thành công."));
        }

        /// <summary>
        /// Chi tiết 1 mục danh mục BHYT.
        /// GET /api/v1/admin/bhyt-catalog/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        [HasPermission("system_config.view")]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var result = await _catalogService.GetByIdAsync(id);
            return Ok(ApiResponse<BhytCatalogItemDto>.Ok(result, "Lấy chi tiết mục danh mục BHYT thành công."));
        }

        /// <summary>
        /// Thêm mới 1 mục danh mục BHYT.
        /// POST /api/v1/admin/bhyt-catalog
        /// </summary>
        [HttpPost]
        [HasPermission("system_config.edit")]
        public async Task<IActionResult> CreateItem([FromBody] CreateBhytCatalogItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var result = await _catalogService.CreateItemAsync(request);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<BhytCatalogItemDto>.Ok(result, "Tạo mới mục danh mục BHYT thành công."));
        }

        /// <summary>
        /// Cập nhật 1 mục danh mục BHYT.
        /// PUT /api/v1/admin/bhyt-catalog/{id}
        /// </summary>
        [HttpPut("{id:guid}")]
        [HasPermission("system_config.edit")]
        public async Task<IActionResult> UpdateItem([FromRoute] Guid id, [FromBody] UpdateBhytCatalogItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var result = await _catalogService.UpdateItemAsync(id, request);
            return Ok(ApiResponse<BhytCatalogItemDto>.Ok(result, "Cập nhật mục danh mục BHYT thành công."));
        }

        /// <summary>
        /// Xóa 1 mục danh mục BHYT.
        /// DELETE /api/v1/admin/bhyt-catalog/{id}
        /// </summary>
        [HttpDelete("{id:guid}")]
        [HasPermission("system_config.edit")]
        public async Task<IActionResult> DeleteItem([FromRoute] Guid id)
        {
            await _catalogService.DeleteItemAsync(id);
            return Ok(ApiResponse<object>.Ok(null, "Xóa mục danh mục BHYT thành công."));
        }
    }
}
