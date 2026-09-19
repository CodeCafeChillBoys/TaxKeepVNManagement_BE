using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.TaxDocumentTypes;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/tax-document-types")]
    [Authorize]
    public class TaxDocumentTypeController : ControllerBase
    {
        private readonly ITaxDocumentTypeService _service;

        public TaxDocumentTypeController(ITaxDocumentTypeService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lấy danh sách các loại chứng từ thuế (hỗ trợ tìm kiếm theo code/name và lọc theo tính đủ điều kiện giảm trừ).
        /// GET /api/v1/tax-document-types?search=invoice&isTaxEligible=true
        /// </summary>
        [HttpGet(Name = "GetTaxDocumentTypes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] TaxDocumentTypeQueryParameters query)
        {
            var data = await _service.GetAllAsync(query);
            return Ok(ApiResponse<IEnumerable<TaxDocumentTypeDto>>.Ok(data, SuccessMessages.DocTypeListRetrieved));
        }

        /// <summary>
        /// Lấy thông tin chi tiết một loại chứng từ thuế theo mã code.
        /// GET /api/v1/tax-document-types/{code}
        /// </summary>
        [HttpGet("{code}", Name = "GetTaxDocumentTypeByCode")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByCode([FromRoute] string code)
        {
            var data = await _service.GetByCodeAsync(code);
            return Ok(ApiResponse<TaxDocumentTypeDto>.Ok(data, SuccessMessages.DocTypeDetailRetrieved));
        }

        /// <summary>
        /// Thêm mới một loại chứng từ thuế để AI có thể phân loại và gán vào khi bóc tách.
        /// POST /api/v1/tax-document-types
        /// </summary>
        [HttpPost(Name = "CreateTaxDocumentType")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateTaxDocumentTypeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.CreateAsync(dto);
            return CreatedAtRoute("GetTaxDocumentTypeByCode", new { code = data.Code },
                ApiResponse<TaxDocumentTypeDto>.Ok(data, SuccessMessages.DocTypeCreated));
        }

        /// <summary>
        /// Cập nhật thông tin loại chứng từ thuế theo mã code.
        /// PUT /api/v1/tax-document-types/{code}
        /// </summary>
        [HttpPut("{code}", Name = "UpdateTaxDocumentType")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] string code, [FromBody] UpdateTaxDocumentTypeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.UpdateAsync(code, dto);
            return Ok(ApiResponse<TaxDocumentTypeDto>.Ok(data, SuccessMessages.DocTypeUpdated));
        }
    }
}
