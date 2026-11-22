using System.Security.Cryptography;
using System.Text;
using BlogEngine.Web.Data.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace BlogEngine.Web.Services;

public interface IPasswordResetTokenService
{
    /// <summary>Tạo token đặt lại mật khẩu (có hạn dùng) cho user.</summary>
    string Create(User user);

    /// <summary>Kiểm tra token còn hạn và chưa bị vô hiệu; trả về Id user nếu hợp lệ.</summary>
    bool TryRead(string? token, out int userId, out string fingerprint);

    /// <summary>Dấu vân tay của mật khẩu hiện tại: đổi mật khẩu thì token cũ tự mất hiệu lực (dùng một lần).</summary>
    string Fingerprint(User user);
}

/// <summary>
/// Token không lưu DB: dùng ASP.NET Core Data Protection (có chữ ký + mã hóa + hạn dùng).
/// Nội dung token = "userId|fingerprint(PasswordHash)". Khi mật khẩu đổi, fingerprint đổi
/// nên link cũ không dùng lại được.
/// Lưu ý triển khai: khóa Data Protection phải được lưu bền vững (và dùng chung nếu chạy nhiều
/// instance), nếu không link trong email sẽ hỏng sau khi restart app.
/// </summary>
public class PasswordResetTokenService : IPasswordResetTokenService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly ITimeLimitedDataProtector _protector;

    public PasswordResetTokenService(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("BlogEngine.PasswordReset.v1").ToTimeLimitedDataProtector();

    public string Create(User user)
        => _protector.Protect($"{user.Id}|{Fingerprint(user)}", Lifetime);

    public bool TryRead(string? token, out int userId, out string fingerprint)
    {
        userId = 0;
        fingerprint = "";
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            var parts = _protector.Unprotect(token).Split('|', 2);
            if (parts.Length != 2 || !int.TryParse(parts[0], out userId)) return false;
            fingerprint = parts[1];
            return true;
        }
        catch (CryptographicException)
        {
            return false; // sai chữ ký, hết hạn hoặc bị sửa
        }
    }

    public string Fingerprint(User user)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash ?? ""))).Substring(0, 32);
}
