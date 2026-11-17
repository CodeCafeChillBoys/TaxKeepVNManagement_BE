namespace TaxKeepDomain.Entities
{
    public class Permission
    {
        public int Id { get; set; }

        // Ví dụ: document.view, document.create
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        // Nhóm để FE hiển thị các checkbox
        public string GroupName { get; set; } = string.Empty;

        public ICollection<RolePermission> RolePermissions { get; set; }
            = new List<RolePermission>();

    }
}