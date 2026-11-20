using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IMedicalRuleEngineService
    {
        /// <summary>
        /// Thực thi Rule Engine (R0 - R3, L1 - L6) thẩm định hồ sơ y tế.
        /// Cập nhật trực tiếp các trường DeductionStatus, DeductionReason, EligibleAmount, SelfPayAmount trên từng dòng
        /// và MedicalClaimStatus, EligibleAmount trên Hóa đơn và Bảng kê.
        /// </summary>
        Task EvaluateClaimAsync(Document receipt, Document? statement, Guid userId, int taxYear);

        /// <summary>
        /// Đối chiếu tên người bệnh với người nộp thuế hoặc danh sách người phụ thuộc ACTIVE.
        /// </summary>
        Task<(bool isMatch, bool isNearMatch, Guid? matchedDependentId)> MatchPatientAsync(string? patientName, Guid userId);
    }
}
