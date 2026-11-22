using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.MedicalCategories;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVNManagementSystem.Attributes;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/medical-eligible-categories")]
    [Authorize]
    public class MedicalEligibleCategoryController : ControllerBase
    {
        private readonly IMedicalEligibleCategoryService _service;

        public MedicalEligibleCategoryController(IMedicalEligibleCategoryService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lấy danh sách các danh mục chi phí y tế theo Quyết định 697/QĐ-BYT & luật thuế TNCN.
        /// Hỗ trợ tìm kiếm theo tên/mã/từ khóa, lọc theo sectionCode (I, II, III...), isEligible, và includeDeleted.
        /// GET /api/v1/medical-eligible-categories?search=kham&sectionCode=I&isEligible=true
        /// </summary>
        [HttpGet(Name = "GetMedicalEligibleCategories")]
        [HasPermission("medical_category.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] MedicalCategoryQueryParameters query)
        {
            var data = await _service.GetAllAsync(query);
            return Ok(ApiResponse<IEnumerable<MedicalEligibleCategoryDto>>.Ok(data, "Lấy danh sách danh mục chi phí y tế thành công."));
        }

        /// <summary>
        /// Lấy thông tin chi tiết một danh mục chi phí y tế theo ID.
        /// GET /api/v1/medical-eligible-categories/{id}
        /// </summary>
        [HttpGet("{id:guid}", Name = "GetMedicalEligibleCategoryById")]
        [HasPermission("medical_category.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var data = await _service.GetByIdAsync(id);
            return Ok(ApiResponse<MedicalEligibleCategoryDto>.Ok(data, "Lấy chi tiết danh mục chi phí y tế thành công."));
        }

        /// <summary>
        /// Thêm mới một danh mục chi phí y tế để AI tham chiếu giảm trừ thuế.
        /// POST /api/v1/medical-eligible-categories
        /// </summary>
        [HttpPost(Name = "CreateMedicalEligibleCategory")]
        [HasPermission("admin.medical_category.manage")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateMedicalEligibleCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.CreateAsync(dto);
            return CreatedAtRoute("GetMedicalEligibleCategoryById", new { id = data.Id },
                ApiResponse<MedicalEligibleCategoryDto>.Ok(data, "Thêm mới danh mục chi phí y tế thành công."));
        }

        /// <summary>
        /// Cập nhật thông tin danh mục chi phí y tế theo ID.
        /// PUT /api/v1/medical-eligible-categories/{id}
        /// </summary>
        [HttpPut("{id:guid}", Name = "UpdateMedicalEligibleCategory")]
        [HasPermission("admin.medical_category.manage")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateMedicalEligibleCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.UpdateAsync(id, dto);
            return Ok(ApiResponse<MedicalEligibleCategoryDto>.Ok(data, "Cập nhật danh mục chi phí y tế thành công."));
        }

        /// <summary>
        /// Xóa mềm (Soft delete) một danh mục chi phí y tế. Dữ liệu không bị mất khỏi database.
        /// DELETE /api/v1/medical-eligible-categories/{id}
        /// </summary>
        [HttpDelete("{id:guid}", Name = "DeleteMedicalEligibleCategory")]
        [HasPermission("admin.medical_category.manage")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse<object>.Ok(null, "Xóa mềm danh mục chi phí y tế thành công."));
        }

        /// <summary>
        /// Khôi phục một danh mục chi phí y tế đã bị xóa mềm trước đó.
        /// PATCH /api/v1/medical-eligible-categories/{id}/restore
        /// </summary>
        [HttpPatch("{id:guid}/restore", Name = "RestoreMedicalEligibleCategory")]
        [HasPermission("admin.medical_category.manage")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Restore([FromRoute] Guid id)
        {
            await _service.RestoreAsync(id);
            return Ok(ApiResponse<object>.Ok(null, "Khôi phục danh mục chi phí y tế thành công."));
        }
    }
}
