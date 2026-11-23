using Barlas.Application.Production;
using Barlas.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Barlas.Api.Controllers;

/// <summary>اسکن کاردکس QR در سالن با گوشی.</summary>
[ApiController]
[Authorize(Policy = nameof(Permission.ScanStages))]
[Route("api/scan")]
public class ScanController(ScanService scans) : ControllerBase
{
    [HttpGet("{qrToken}")]
    public Task<BundleState> Lookup(string qrToken, CancellationToken ct) => scans.LookupAsync(qrToken, ct);

    /// <summary>ثبت پایان مرحله بعدی دسته به نام کارگر.</summary>
    [HttpPost("complete")]
    public Task<BundleState> Complete(ScanRequest request, CancellationToken ct) => scans.CompleteNextStageAsync(request, ct);

    [HttpPost("rework")]
    public Task<BundleState> Rework(ReworkRequest request, CancellationToken ct) => scans.ReworkAsync(request, ct);
}
