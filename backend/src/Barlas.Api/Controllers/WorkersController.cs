using Barlas.Application.Organization;
using Barlas.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Barlas.Api.Controllers;

[ApiController]
[Authorize(Policy = nameof(Permission.ManageUsers))]
[Route("api/workers")]
public class WorkersController(WorkerService workers) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<WorkerDto>> List(CancellationToken ct) => workers.ListAsync(ct);

    [HttpPost]
    public Task<WorkerDto> Create(CreateWorkerRequest request, CancellationToken ct) => workers.CreateAsync(request, ct);

    [HttpPut("{id:guid}")]
    public Task<WorkerDto> Update(Guid id, UpdateWorkerRequest request, CancellationToken ct) => workers.UpdateAsync(id, request, ct);
}
