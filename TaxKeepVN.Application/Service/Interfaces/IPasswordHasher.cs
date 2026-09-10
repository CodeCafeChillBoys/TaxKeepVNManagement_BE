namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IPasswordHasher
    {
        /// <summary>Hash mật khẩu plain-text</summary>
        string Hash(string password);

        /// <summary>Xác minh mật khẩu plain-text với hash đã lưu</summary>
        bool Verify(string password, string hash);
    }
}
