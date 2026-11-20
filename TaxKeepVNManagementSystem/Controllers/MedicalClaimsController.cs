using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.MedicalClaims;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVNManagementSystem.Attributes;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/tax-periods/{periodId:guid}/documents/{documentId:guid}")]
    [Authorize]
    public class MedicalClaimsController : ControllerBase
    {
        private readonly IMedicalClaimService _medicalClaimService;

        public MedicalClaimsController(IMedicalClaimService medicalClaimService)
        {
            _medicalClaimService = medicalClaimService;
        }

        /// <summary>
        /// Lấy thông tin hồ sơ y tế chi tiết: hóa đơn, bảng kê, từng dòng chi phí KCB và trạng thái thẩm định.
        /// GET /api/v1/tax-periods/{periodId}/documents/{documentId}/medical-claim
        /// </summary>
        [HttpGet("medical-claim")]
        [HasPermission("document.view")]
        public async Task<IActionResult> GetMedicalClaim(
            [FromRoute] Guid periodId,
            [FromRoute] Guid documentId)
        {
            var userId = GetUserIdFromToken();
            var claim = await _medicalClaimService.GetMedicalClaimAsync(userId, periodId, documentId);
            return Ok(ApiResponse<MedicalClaimDto>.Ok(claim, "Lấy thông tin hồ sơ y tế thành công."));
        }

        /// <summary>
        /// Tải lên Bảng kê chi phí KCB (Mẫu 01/KBCB) gắn trực tiếp vào Hóa đơn y tế.
        /// POST /api/v1/tax-periods/{periodId}/documents/{documentId}/medical-statement
        /// </summary>
        [HttpPost("medical-statement")]
        [HasPermission("document.upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadMedicalStatement(
            [FromRoute] Guid periodId,
            [FromRoute] Guid documentId,
            IFormFile file)
        {
            var userId = GetUserIdFromToken();
            var claim = await _medicalClaimService.UploadMedicalStatementAsync(userId, periodId, documentId, file);
            return Ok(ApiResponse<MedicalClaimDto>.Ok(claim, "Tải lên và gắn Bảng kê chi phí KCB thành công."));
        }

        /// <summary>
        /// Ghép tay Bảng kê chi phí KCB đã có vào Hóa đơn y tế.
        /// PUT /api/v1/tax-periods/{periodId}/documents/{documentId}/medical-statement/link
        /// </summary>
        [HttpPut("medical-statement/link")]
        [HasPermission("document.edit")]
        public async Task<IActionResult> LinkMedicalStatement(
            [FromRoute] Guid periodId,
            [FromRoute] Guid documentId,
            [FromBody] LinkMedicalStatementRequestDto request)
        {
            var userId = GetUserIdFromToken();
            var claim = await _medicalClaimService.LinkMedicalStatementAsync(userId, periodId, documentId, request.StatementDocumentId);
            return Ok(ApiResponse<MedicalClaimDto>.Ok(claim, "Ghép Bảng kê chi phí KCB với Hóa đơn thành công."));
        }

        /// <summary>
        /// Người dùng thay đổi trạng thái giảm trừ 1 dòng (ELIGIBLE/EXCLUDED) hoặc bật/tắt tính phần tự trả.
        /// PATCH /api/v1/tax-periods/{periodId}/documents/{documentId}/items/{itemId}/deduction
        /// </summary>
        [HttpPatch("items/{itemId:long}/deduction")]
        [HasPermission("document.edit")]
        public async Task<IActionResult> UpdateItemDeduction(
            [FromRoute] Guid periodId,
            [FromRoute] Guid documentId,
            [FromRoute] long itemId,
            [FromBody] UpdateItemDeductionRequestDto request)
        {
            var userId = GetUserIdFromToken();
            var claim = await _medicalClaimService.UpdateItemDeductionAsync(userId, periodId, documentId, itemId, request);
            return Ok(ApiResponse<MedicalClaimDto>.Ok(claim, "Cập nhật trạng thái dòng chi phí thành công."));
        }

        /// <summary>
        /// Lấy danh sách các hóa đơn y tế ứng viên để ghép với bảng kê chi phí KCB.
        /// GET /api/v1/tax-periods/{periodId}/documents/{documentId}/candidate-receipts
        /// </summary>
        [HttpGet("candidate-receipts")]
        [HasPermission("document.view")]
        public async Task<IActionResult> GetCandidateReceipts(
            [FromRoute] Guid periodId,
            [FromRoute] Guid documentId)
        {
            var userId = GetUserIdFromToken();
            var candidates = await _medicalClaimService.GetCandidateReceiptsForStatementAsync(userId, periodId, documentId);
            return Ok(ApiResponse<List<CandidateReceiptDto>>.Ok(candidates, "Lấy danh sách hóa đơn ứng viên thành công."));
        }

        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException(ErrorCodes.InvalidToken, ErrorMessages.InvalidToken);
            }
            return userId;
        }
    }
}
