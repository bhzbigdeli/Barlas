using Barlas.Application.Abstractions;
using Barlas.Application.Common;
using Barlas.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Auth;

public sealed record LoginRequest(string UserName, string Password);

public sealed record UserInfo(
    Guid Id, string UserName, string FullName, Guid RoleId, string RoleName,
    Guid? UnitId, string? UnitName, bool SeesAllUnits, IReadOnlyList<Permission> Permissions);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, UserInfo User);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public class AuthService(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, ICurrentUser currentUser)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var userName = User.NormalizeUserName(request.UserName);
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName, ct);

        if (user is null || !user.IsActive || !hasher.Verify(user, request.Password))
            throw new AppException("نام کاربری یا رمز عبور اشتباه است.", 401);

        var (token, expires) = tokens.CreateToken(user);
        return new LoginResponse(token, expires, await DescribeAsync(user, ct));
    }

    public async Task<UserInfo> MeAsync(CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct) ?? throw AppException.NotFound("کاربر");
        return await DescribeAsync(user, ct);
    }

    public async Task ChangeOwnPasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct) ?? throw AppException.NotFound("کاربر");
        if (!hasher.Verify(user, request.CurrentPassword))
            throw new AppException("رمز فعلی اشتباه است.");
        User.ValidatePassword(request.NewPassword);
        user.SetPasswordHash(hasher.Hash(user, request.NewPassword));
        await db.SaveChangesAsync(ct);
    }

    private async Task<UserInfo> DescribeAsync(User user, CancellationToken ct)
    {
        var role = await db.Roles.FirstAsync(r => r.Id == user.RoleId, ct);
        var unitName = user.UnitId is null ? null
            : await db.Units.Where(u => u.Id == user.UnitId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        var permissions = Enum.GetValues<Permission>().Where(role.Has).ToList();
        return new UserInfo(user.Id, user.UserName, user.FullName, role.Id, role.Name,
            user.UnitId, unitName, role.SeesAllUnits, permissions);
    }
}
