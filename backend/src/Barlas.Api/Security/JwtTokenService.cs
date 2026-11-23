using System.Security.Claims;
using System.Text;
using Barlas.Application.Abstractions;
using Barlas.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Barlas.Api.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "barlas";
    public string Audience { get; set; } = "barlas";

    /// <summary>کلید امضا؛ حداقل ۳۲ کاراکتر. در محیط عملیاتی از متغیر محیطی Jwt__SigningKey بیاید.</summary>
    public string SigningKey { get; set; } = "";

    /// <summary>طول اعتبار توکن؛ یک شیفت کاری.</summary>
    public int LifetimeHours { get; set; } = 12;

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));
}

/// <summary>توکن فقط شناسه کاربر را دارد؛ مجوزها در هر درخواست از دیتابیس خوانده می‌شوند.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
    {
        var o = options.Value;
        var expires = clock.UtcNow.AddHours(o.LifetimeHours);

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
            ]),
            Issuer = o.Issuer,
            Audience = o.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(o.Key(), SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }
}
