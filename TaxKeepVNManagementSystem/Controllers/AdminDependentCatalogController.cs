using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Catalog;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/admin")]
    [Authorize(Roles = "ADMIN")]
    public class AdminDependentCatalogController : ControllerBase
    {
        private readonly IDependentCatalogService _catalogService;

        public AdminDependentCatalogController(IDependentCatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        #region Groups Management

        /// <summary>
        /// [Admin] Lấy toàn bộ danh sách nhóm người phụ thuộc trong catalog (có thể lọc theo isActive).
        /// GET /api/v1/admin/dependent-groups?isActive=true
        /// </summary>
        [HttpGet("dependent-groups")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllGroups([FromQuery] bool? isActive = null)
        {
            var groups = await _catalogService.GetAllGroupsAsync(isActive);
            return Ok(ApiResponse<List<AdminDependentGroupDto>>.Ok(groups, "Lấy danh sách nhóm người phụ thuộc thành công."));
        }

        /// <summary>
        /// [Admin] Lấy chi tiết nhóm người phụ thuộc theo mã.
        /// GET /api/v1/admin/dependent-groups/{code}
        /// </summary>
        [HttpGet("dependent-groups/{code}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupByCode([FromRoute] string code)
        {
            var group = await _catalogService.GetGroupByCodeAsync(code);
            return Ok(ApiResponse<AdminDependentGroupDto>.Ok(group, "Lấy chi tiết nhóm người phụ thuộc thành công."));
        }

        /// <summary>
        /// [Admin] Tạo mới một nhóm người phụ thuộc vào catalog.
        /// POST /api/v1/admin/dependent-groups
        /// </summary>
        [HttpPost("dependent-groups")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateGroup([FromBody] CreateDependentGroupRequest request)
        {
            var created = await _catalogService.CreateGroupAsync(request);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<AdminDependentGroupDto>.Ok(created, "Tạo mới nhóm người phụ thuộc thành công."));
        }

        /// <summary>
        /// [Admin] Cập nhật thông tin nhóm người phụ thuộc trong catalog.
        /// PUT /api/v1/admin/dependent-groups/{code}
        /// </summary>
        [HttpPut("dependent-groups/{code}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateGroup([FromRoute] string code, [FromBody] AdminUpdateDependentGroupRequest request)
        {
            var updated = await _catalogService.UpdateGroupAsync(code, request);
            return Ok(ApiResponse<AdminDependentGroupDto>.Ok(updated, "Cập nhật nhóm người phụ thuộc thành công."));
        }

        /// <summary>
        /// [Admin] Xóa nhóm người phụ thuộc khỏi catalog.
        /// DELETE /api/v1/admin/dependent-groups/{code}
        /// </summary>
        [HttpDelete("dependent-groups/{code}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteGroup([FromRoute] string code)
        {
            await _catalogService.DeleteGroupAsync(code);
            return Ok(ApiResponse<object>.Ok(null, $"Đã xóa nhóm người phụ thuộc '{code}' thành công."));
        }

        /// <summary>
        /// [Admin] Kích hoạt nhóm người phụ thuộc do AI đề xuất (bắt buộc chọn relationshipCode).
        /// PATCH /api/v1/admin/dependent-groups/{code}/activate
        /// </summary>
        [HttpPatch("dependent-groups/{code}/activate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ActivateAiGroup([FromRoute] string code, [FromBody] ActivateAiGroupRequest request)
        {
            var activated = await _catalogService.ActivateAiGroupAsync(code, request);
            return Ok(ApiResponse<AdminDependentGroupDto>.Ok(activated, $"Kích hoạt nhóm '{code}' thành công."));
        }

        #endregion

        #region Relationships Management

        /// <summary>
        /// [Admin] Lấy toàn bộ danh sách quan hệ người phụ thuộc trong catalog.
        /// GET /api/v1/admin/dependent-relationships?isActive=true
        /// </summary>
        [HttpGet("dependent-relationships")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllRelationships([FromQuery] bool? isActive = null)
        {
            var rels = await _catalogService.GetAllRelationshipsAsync(isActive);
            return Ok(ApiResponse<List<AdminDependentRelationshipDto>>.Ok(rels, "Lấy danh sách quan hệ người phụ thuộc thành công."));
        }

        /// <summary>
        /// [Admin] Lấy chi tiết quan hệ người phụ thuộc theo mã.
        /// GET /api/v1/admin/dependent-relationships/{code}
        /// </summary>
        [HttpGet("dependent-relationships/{code}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRelationshipByCode([FromRoute] string code)
        {
            var rel = await _catalogService.GetRelationshipByCodeAsync(code);
            return Ok(ApiResponse<AdminDependentRelationshipDto>.Ok(rel, "Lấy chi tiết quan hệ người phụ thuộc thành công."));
        }

        /// <summary>
        /// [Admin] Tạo mới quan hệ người phụ thuộc vào catalog.
        /// POST /api/v1/admin/dependent-relationships
        /// </summary>
        [HttpPost("dependent-relationships")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateRelationship([FromBody] CreateDependentRelationshipRequest request)
        {
            var created = await _catalogService.CreateRelationshipAsync(request);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<AdminDependentRelationshipDto>.Ok(created, "Tạo mới quan hệ người phụ thuộc thành công."));
        }

        /// <summary>
        /// [Admin] Cập nhật thông tin quan hệ người phụ thuộc.
        /// PUT /api/v1/admin/dependent-relationships/{code}
        /// </summary>
        [HttpPut("dependent-relationships/{code}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateRelationship([FromRoute] string code, [FromBody] UpdateDependentRelationshipRequest request)
        {
            var updated = await _catalogService.UpdateRelationshipAsync(code, request);
            return Ok(ApiResponse<AdminDependentRelationshipDto>.Ok(updated, "Cập nhật quan hệ người phụ thuộc thành công."));
        }

        /// <summary>
        /// [Admin] Xóa quan hệ người phụ thuộc.
        /// DELETE /api/v1/admin/dependent-relationships/{code}
        /// </summary>
        [HttpDelete("dependent-relationships/{code}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteRelationship([FromRoute] string code)
        {
            await _catalogService.DeleteRelationshipAsync(code);
            return Ok(ApiResponse<object>.Ok(null, $"Đã xóa quan hệ '{code}' thành công."));
        }

        #endregion
    }
}
