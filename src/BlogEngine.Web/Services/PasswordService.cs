using System.Security.Cryptography;
using System.Text;
using BlogEngine.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace BlogEngine.Web.Services;

public enum PasswordCheck { Failed, Success, SuccessRehashNeeded }

public interface IPasswordService
{
    string Hash(User user, string password);
    PasswordCheck Verify(User user, string password);
}

/// <summary>
/// Băm mật khẩu bằng PasswordHasher của ASP.NET Core Identity (PBKDF2, có salt).
/// Đồng thời đọc được hash SHA-256 không salt của BlogEngine.NET cũ (tiền tố "LEGACY:")
/// và yêu cầu nâng cấp lại hash khi đăng nhập thành công.
/// </summary>
public class PasswordService : IPasswordService
{
    public const string LegacyPrefix = "LEGACY:";
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public PasswordCheck Verify(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(password))
            return PasswordCheck.Failed;

        if (user.PasswordHash.StartsWith(LegacyPrefix, StringComparison.Ordinal))
        {
            var expected = user.PasswordHash[LegacyPrefix.Length..];
            var actual = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual))
                ? PasswordCheck.SuccessRehashNeeded
                : PasswordCheck.Failed;
        }

        return _hasher.VerifyHashedPassword(user, user.PasswordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed
        };
    }
}
