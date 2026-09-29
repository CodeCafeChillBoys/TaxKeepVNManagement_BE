namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Định nghĩa các vai trò (Roles) trong hệ thống TaxKeepVN
    /// </summary>
    public static class UserRoles
    {
        /// <summary>Người nộp thuế (Người dùng cá nhân thông thường)</summary>
        public const string Taxpayer = "taxpayer";

        /// <summary>Quản trị viên / Thẩm định viên hệ thống</summary>
        public const string Admin = "admin";

        /// <summary>Chuyên gia tư vấn thuế (sau khi hồ sơ được phê duyệt)</summary>
        public const string Expert = "expert";
    }
}
