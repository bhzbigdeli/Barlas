using Barlas.Application.Admin;
using Barlas.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Barlas.Api.Controllers;

/// <summary>تعریف کاربران.</summary>
[ApiController]
[Authorize(Policy = nameof(Permission.ManageUsers))]
[Route("api/users")]
public class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UserDto>> List(CancellationToken ct) => users.ListAsync(ct);

    [HttpPost]
    public Task<UserDto> Create(CreateUserRequest request, CancellationToken ct) => users.CreateAsync(request, ct);

    [HttpPut("{id:guid}")]
    public Task<UserDto> Update(Guid id, UpdateUserRequest request, CancellationToken ct) => users.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    public Task<UserDto> Activate(Guid id, CancellationToken ct) => users.SetActiveAsync(id, true, ct);

    [HttpPost("{id:guid}/deactivate")]
    public Task<UserDto> Deactivate(Guid id, CancellationToken ct) => users.SetActiveAsync(id, false, ct);
}

/// <summary>تعریف سطوح دسترسی.</summary>
[ApiController]
[Authorize(Policy = nameof(Permission.ManageUsers))]
[Route("api/roles")]
public class RolesController(RoleService roles) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RoleDto>> List(CancellationToken ct) => roles.ListAsync(ct);

    /// <summary>فهرست مجوزهای قابل‌انتخاب با نام فارسی.</summary>
    [HttpGet("permissions")]
    public IReadOnlyList<PermissionInfo> Permissions() => PermissionCatalog.All;

    [HttpPost]
    public Task<RoleDto> Create(SaveRoleRequest request, CancellationToken ct) => roles.CreateAsync(request, ct);

    [HttpPut("{id:guid}")]
    public Task<RoleDto> Update(Guid id, SaveRoleRequest request, CancellationToken ct) => roles.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await roles.DeleteAsync(id, ct);
        return NoContent();
    }
}
