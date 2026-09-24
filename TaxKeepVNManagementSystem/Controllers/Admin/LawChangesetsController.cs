using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Law;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Law.Changeset;

namespace TaxKeepVNManagementSystem.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/law/changesets")]
    [Authorize(Roles = "TaxAdmin,Admin,taxadmin,admin")]
    public class LawChangesetsController : ControllerBase
    {
        private readonly ILawChangesetService _changesetService;

        public LawChangesetsController(ILawChangesetService changesetService)
        {
            _changesetService = changesetService;
        }

        private Guid GetAdminId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
        }

        /// <summary>
        /// Danh sách các bản đề xuất
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<ChangesetListItemDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetChangesets([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
        {
            var result = await _changesetService.GetChangesetsAsync(status, page, size, ct);
            return Ok(ApiResponse<List<ChangesetListItemDto>>.Ok(result, "Lấy danh sách bản đề xuất thành công."));
        }

        /// <summary>
        /// Lấy bản đề xuất đang mở (nếu có)
        /// </summary>
        [HttpGet("open")]
        [ProducesResponseType(typeof(ApiResponse<ChangesetDetailDto?>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOpenChangeset(CancellationToken ct = default)
        {
            var result = await _changesetService.GetOpenChangesetAsync(ct);
            return Ok(ApiResponse<ChangesetDetailDto?>.Ok(result, "Lấy bản đề xuất đang mở thành công."));
        }

        /// <summary>
        /// Chi tiết một bản đề xuất kèm kết quả kiểm tra
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<ChangesetDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetChangesetDetail([FromRoute] Guid id, CancellationToken ct = default)
        {
            var result = await _changesetService.GetChangesetDetailAsync(id, ct);
            return Ok(ApiResponse<ChangesetDetailDto>.Ok(result, "Lấy chi tiết bản đề xuất thành công."));
        }

        /// <summary>
        /// Tạo bản đề xuất thủ công (MANUAL)
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ChangesetDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateManualChangeset([FromBody] ManualChangesetInput input, CancellationToken ct = default)
        {
            var result = await _changesetService.CreateManualChangesetAsync(input, GetAdminId(), ct);
            return Ok(ApiResponse<ChangesetDetailDto>.Ok(result, "Tạo bản đề xuất thủ công thành công."));
        }

        /// <summary>
        /// Tạo bản đề xuất từ file import JSON
        /// </summary>
        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<ChangesetDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ImportChangeset([FromForm] IFormFile file, [FromForm] string reason, [FromForm] Guid? documentId, CancellationToken ct = default)
        {
            var result = await _changesetService.ImportChangesetAsync(file, reason, documentId, GetAdminId(), ct);
            return Ok(ApiResponse<ChangesetDetailDto>.Ok(result, "Import bản đề xuất thành công."));
        }

        /// <summary>
        /// Thêm dòng thay đổi vào bản đề xuất
        /// </summary>
        [HttpPost("{id:guid}/ops")]
        [ProducesResponseType(typeof(ApiResponse<OpDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AddOp([FromRoute] Guid id, [FromBody] OperationInput input, CancellationToken ct = default)
        {
            var result = await _changesetService.AddOpAsync(id, input, GetAdminId(), ct);
            return Ok(ApiResponse<OpDto>.Ok(result, "Thêm dòng thay đổi thành công."));
        }

        /// <summary>
        /// Chỉnh sửa nội dung hoặc quyết định của dòng thay đổi
        /// </summary>
        [HttpPatch("{id:guid}/ops/{opId:guid}")]
        [ProducesResponseType(typeof(ApiResponse<OpDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PatchOp([FromRoute] Guid id, [FromRoute] Guid opId, [FromBody] OperationPatchInput input, CancellationToken ct = default)
        {
            var result = await _changesetService.PatchOpAsync(id, opId, input, GetAdminId(), ct);
            return Ok(ApiResponse<OpDto>.Ok(result, "Cập nhật dòng thay đổi thành công."));
        }

        /// <summary>
        /// Chấp nhận toàn bộ các dòng và quan hệ đang PENDING hợp lệ
        /// </summary>
        [HttpPost("{id:guid}/accept-all")]
        [ProducesResponseType(typeof(ApiResponse<ChangesetDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AcceptAll([FromRoute] Guid id, CancellationToken ct = default)
        {
            var result = await _changesetService.AcceptAllAsync(id, GetAdminId(), ct);
            return Ok(ApiResponse<ChangesetDetailDto>.Ok(result, "Chấp nhận toàn bộ thành công."));
        }

        /// <summary>
        /// Thêm quan hệ văn bản vào bản đề xuất
        /// </summary>
        [HttpPost("{id:guid}/relations")]
        [ProducesResponseType(typeof(ApiResponse<RelationItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddRelation([FromRoute] Guid id, [FromBody] RelationInput input, CancellationToken ct = default)
        {
            var result = await _changesetService.AddRelationAsync(id, input, GetAdminId(), ct);
            return Ok(ApiResponse<RelationItemDto>.Ok(result, "Thêm quan hệ thành công."));
        }

        /// <summary>
        /// Cập nhật quan hệ văn bản
        /// </summary>
        [HttpPatch("{id:guid}/relations/{relId:guid}")]
        [ProducesResponseType(typeof(ApiResponse<RelationItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PatchRelation([FromRoute] Guid id, [FromRoute] Guid relId, [FromBody] RelationPatchInput input, CancellationToken ct = default)
        {
            var result = await _changesetService.PatchRelationAsync(id, relId, input, GetAdminId(), ct);
            return Ok(ApiResponse<RelationItemDto>.Ok(result, "Cập nhật quan hệ thành công."));
        }

        /// <summary>
        /// Xử lý quy tắc bị bỏ lại (Orphan rule)
        /// </summary>
        [HttpPost("{id:guid}/orphans/resolve")]
        [ProducesResponseType(typeof(ApiResponse<ChangesetDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResolveOrphan([FromRoute] Guid id, [FromBody] OrphanResolveInput input, CancellationToken ct = default)
        {
            var result = await _changesetService.ResolveOrphanAsync(id, input, GetAdminId(), ct);
            return Ok(ApiResponse<ChangesetDetailDto>.Ok(result, "Xử lý quy tắc bị bỏ lại thành công."));
        }

        /// <summary>
        /// Rebase bản đề xuất lên revision HEAD mới nhất
        /// </summary>
        [HttpPost("{id:guid}/rebase")]
        [ProducesResponseType(typeof(ApiResponse<ChangesetDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Rebase([FromRoute] Guid id, CancellationToken ct = default)
        {
            var result = await _changesetService.RebaseAsync(id, GetAdminId(), ct);
            return Ok(ApiResponse<ChangesetDetailDto>.Ok(result, "Rebase bản đề xuất thành công."));
        }

        /// <summary>
        /// Chạy kiểm tra bộ quy tắc (Check) cho bản đề xuất
        /// </summary>
        [HttpPost("{id:guid}/check")]
        [ProducesResponseType(typeof(ApiResponse<CheckResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Check([FromRoute] Guid id, CancellationToken ct = default)
        {
            var result = await _changesetService.CheckAsync(id, ct);
            return Ok(ApiResponse<CheckResultDto>.Ok(result, "Kiểm tra bản đề xuất thành công."));
        }

        /// <summary>
        /// Merge bản đề xuất vào luật hệ thống, sinh revision mới
        /// </summary>
        [HttpPost("{id:guid}/merge")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Merge([FromRoute] Guid id, CancellationToken ct = default)
        {
            int revisionNo = await _changesetService.MergeAsync(id, GetAdminId(), ct);
            return Ok(ApiResponse<object>.Ok(new { revisionNo }, $"Merge bản đề xuất thành công vào revision {revisionNo}."));
        }

        /// <summary>
        /// Từ chối bản đề xuất
        /// </summary>
        [HttpPost("{id:guid}/reject")]
        [ProducesResponseType(typeof(ApiResponse<ChangesetDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reject([FromRoute] Guid id, [FromBody] RejectChangesetInput input, CancellationToken ct = default)
        {
            var result = await _changesetService.RejectAsync(id, input.Reason, GetAdminId(), ct);
            return Ok(ApiResponse<ChangesetDetailDto>.Ok(result, "Từ chối bản đề xuất thành công."));
        }

        /// <summary>
        /// Thử lại bóc tách AI cho bản đề xuất FAILED
        /// </summary>
        [HttpPost("{id:guid}/retry")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Retry([FromRoute] Guid id, CancellationToken ct = default)
        {
            await _changesetService.RetryAsync(id, GetAdminId(), ct);
            return Ok(ApiResponse<object>.Ok(null, "Yêu cầu xử lý lại AI đã được gửi."));
        }
    }
}
