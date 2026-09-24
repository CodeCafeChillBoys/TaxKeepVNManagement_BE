using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Law;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Law.Query;

namespace TaxKeepVNManagementSystem.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/law")]
    [Authorize(Roles = "TaxAdmin,Admin,taxadmin,admin")]
    public class SystemLawController : ControllerBase
    {
        private readonly ISystemLawQueryService _queryService;

        public SystemLawController(ISystemLawQueryService queryService)
        {
            _queryService = queryService;
        }

        /// <summary>
        /// Truy vấn luật hệ thống theo năm tính thuế và revision
        /// </summary>
        [HttpGet("system-law")]
        [ProducesResponseType(typeof(ApiResponse<EffectiveLawDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSystemLaw([FromQuery] int taxYear, [FromQuery] int? revision, CancellationToken ct)
        {
            var result = await _queryService.GetEffectiveAsync(taxYear, revision, ct);
            return Ok(ApiResponse<EffectiveLawDto>.Ok(result, "Lấy luật hệ thống thành công."));
        }

        /// <summary>
        /// Xem lịch sử thay đổi của một mã quy tắc luật
        /// </summary>
        [HttpGet("system-law/rules/{ruleCode}/history")]
        [ProducesResponseType(typeof(ApiResponse<RuleHistoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRuleHistory([FromRoute] string ruleCode, CancellationToken ct)
        {
            var result = await _queryService.GetRuleHistoryAsync(ruleCode, ct);
            return Ok(ApiResponse<RuleHistoryDto>.Ok(result, "Lấy lịch sử mã quy tắc thành công."));
        }

        /// <summary>
        /// Danh sách các revision đã merge
        /// </summary>
        [HttpGet("revisions")]
        [ProducesResponseType(typeof(ApiResponse<List<RevisionDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRevisions([FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
        {
            var result = await _queryService.GetRevisionsAsync(page, size, ct);
            return Ok(ApiResponse<List<RevisionDto>>.Ok(result, "Lấy danh sách revision thành công."));
        }

        /// <summary>
        /// Chi tiết một revision kèm các thay đổi
        /// </summary>
        [HttpGet("revisions/{revisionNo}")]
        [ProducesResponseType(typeof(ApiResponse<RevisionDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRevisionDetail([FromRoute] int revisionNo, CancellationToken ct = default)
        {
            var result = await _queryService.GetRevisionDetailAsync(revisionNo, ct);
            return Ok(ApiResponse<RevisionDetailDto>.Ok(result, "Lấy chi tiết revision thành công."));
        }

        /// <summary>
        /// Danh mục mã quy tắc luật
        /// </summary>
        [HttpGet("rule-definitions")]
        [ProducesResponseType(typeof(ApiResponse<List<LawRuleDefinitionDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRuleDefinitions([FromQuery] bool? activeOnly, CancellationToken ct = default)
        {
            var result = await _queryService.GetRuleDefinitionsAsync(activeOnly, ct);
            return Ok(ApiResponse<List<LawRuleDefinitionDto>>.Ok(result, "Lấy danh mục mã quy tắc thành công."));
        }

        /// <summary>
        /// Thêm mới mã quy tắc luật
        /// </summary>
        [HttpPost("rule-definitions")]
        [ProducesResponseType(typeof(ApiResponse<LawRuleDefinitionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateRuleDefinition([FromBody] LawRuleDefinitionInput input, CancellationToken ct = default)
        {
            var result = await _queryService.CreateRuleDefinitionAsync(input, ct);
            return Ok(ApiResponse<LawRuleDefinitionDto>.Ok(result, "Tạo mới mã quy tắc thành công."));
        }

        /// <summary>
        /// Cập nhật mã quy tắc luật
        /// </summary>
        [HttpPut("rule-definitions/{ruleCode}")]
        [ProducesResponseType(typeof(ApiResponse<LawRuleDefinitionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateRuleDefinition([FromRoute] string ruleCode, [FromBody] LawRuleDefinitionUpdateInput input, CancellationToken ct = default)
        {
            var result = await _queryService.UpdateRuleDefinitionAsync(ruleCode, input, ct);
            return Ok(ApiResponse<LawRuleDefinitionDto>.Ok(result, "Cập nhật mã quy tắc thành công."));
        }
    }
}
