using Barlas.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Barlas.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    [Authorize]
    [HttpGet("me")]
    public Task<UserInfo> Me(CancellationToken ct) => auth.MeAsync(ct);

    /// <summary>تغییر رمز خود کاربر.</summary>
    [Authorize]
    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangeOwnPasswordAsync(request, ct);
        return NoContent();
    }
}
