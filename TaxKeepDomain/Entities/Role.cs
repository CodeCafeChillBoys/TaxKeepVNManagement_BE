using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Bảng quản lý vai trò trong hệ thống (RBAC)
    /// </summary>
    public class Role
    {
        public int Id { get; set; }

        /// <summary>Mã định danh vai trò (VD: taxpayer, admin, expert)</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Tên hiển thị vai trò (VD: Người nộp thuế, Quản trị viên, Chuyên gia)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Mô tả chi tiết quyền hạn của vai trò</summary>
        public string? Description { get; set; }

        /// <summary>Trạng thái kích hoạt</summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation property (1 Role có nhiều User)
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
