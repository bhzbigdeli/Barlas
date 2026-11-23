using Barlas.Application.Abstractions;
using Barlas.Application.Common;
using Barlas.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Admin;

public sealed record UserDto(Guid Id, string UserName, string FullName, Guid RoleId, string RoleName,
    Guid? UnitId, string? UnitName, bool IsActive);

public sealed record CreateUserRequest(string UserName, string FullName, Guid RoleId, Guid? UnitId, string Password);

public sealed record UpdateUserRequest(string FullName, Guid RoleId, Guid? UnitId);

public sealed record ResetPasswordRequest(string NewPassword);

/// <summary>تعریف کاربران. همیشه حداقل یک «مالک» فعال باید بماند.</summary>
public class UserService(IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct);
        var roles = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return users.Select(u => ToDto(u, roles, units)).ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var userName = User.NormalizeUserName(request.UserName);
        if (await db.Users.AnyAsync(u => u.UserName == userName, ct))
            throw new AppException($"نام کاربری «{userName}» قبلاً استفاده شده است.", 409);

        var role = await LoadRoleAsync(request.RoleId, ct);
        await EnsureUnitAsync(request.UnitId, ct);
        User.ValidatePassword(request.Password);

        var user = new User(userName, request.FullName, role, request.UnitId);
        user.SetPasswordHash(hasher.Hash(user, request.Password));
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await LoadUserAsync(id, ct);
        var role = await LoadRoleAsync(request.RoleId, ct);
        await EnsureUnitAsync(request.UnitId, ct);

        if (user.RoleId != role.Id) await EnsureNotLastOwnerAsync(user, ct);
        user.Update(request.FullName, role, request.UnitId);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await LoadUserAsync(id, ct);
        User.ValidatePassword(request.NewPassword);
        user.SetPasswordHash(hasher.Hash(user, request.NewPassword));
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        var user = await LoadUserAsync(id, ct);
        if (!active)
        {
            if (user.Id == currentUser.UserId) throw new AppException("نمی‌توانید حساب خودتان را غیرفعال کنید.");
            await EnsureNotLastOwnerAsync(user, ct);
            user.Deactivate();
        }
        else user.Activate();

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<UserDto> GetAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == id, ct);
        var roles = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return ToDto(user, roles, units);
    }

    private async Task<User> LoadUserAsync(Guid id, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("کاربر");

    private async Task<Role> LoadRoleAsync(Guid id, CancellationToken ct) =>
        await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw AppException.NotFound("سطح دسترسی");

    private async Task EnsureUnitAsync(Guid? unitId, CancellationToken ct)
    {
        if (unitId is { } id && !await db.Units.AnyAsync(u => u.Id == id && u.IsActive, ct))
            throw AppException.NotFound("واحد");
    }

    private async Task EnsureNotLastOwnerAsync(User user, CancellationToken ct)
    {
        var ownerRoleIds = await db.Roles.Where(r => r.IsSystem).Select(r => r.Id).ToListAsync(ct);
        if (!ownerRoleIds.Contains(user.RoleId) || !user.IsActive) return;
        var otherOwners = await db.Users.CountAsync(u => ownerRoleIds.Contains(u.RoleId) && u.IsActive && u.Id != user.Id, ct);
        if (otherOwners == 0) throw new AppException("حداقل یک کاربر «مالک» فعال باید بماند.");
    }

    private static UserDto ToDto(User u, IReadOnlyDictionary<Guid, string> roles, IReadOnlyDictionary<Guid, string> units) =>
        new(u.Id, u.UserName, u.FullName, u.RoleId, roles.GetValueOrDefault(u.RoleId, "?"),
            u.UnitId, u.UnitId is { } id ? units.GetValueOrDefault(id) : null, u.IsActive);
}
