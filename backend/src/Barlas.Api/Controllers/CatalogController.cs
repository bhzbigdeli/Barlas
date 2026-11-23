using Barlas.Application.Catalog;
using Barlas.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Barlas.Api.Controllers;

/// <summary>تعریف محصول و اطلاعات پایه. خواندن برای همه کاربران؛ نوشتن با مجوز «تعریف محصول».</summary>
[ApiController]
[Authorize]
[Route("api/catalog")]
public class CatalogController(CatalogService catalog) : ControllerBase
{
    private const string Write = nameof(Permission.ManageCatalog);

    [HttpGet("products")]
    public Task<IReadOnlyList<ProductDto>> Products(CancellationToken ct) => catalog.ListProductsAsync(ct);

    [HttpGet("products/{id:guid}")]
    public Task<ProductDto> Product(Guid id, CancellationToken ct) => catalog.GetProductAsync(id, ct);

    [HttpGet("products/{id:guid}/overview")]
    public Task<ProductOverview> ProductOverview(Guid id, CancellationToken ct) => catalog.GetProductOverviewAsync(id, ct);

    [HttpGet("products/{id:guid}/image")]
    public async Task<IActionResult> Image(Guid id, CancellationToken ct)
    {
        var image = await catalog.GetImageAsync(id, ct);
        if (image is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=86400";
        return File(image.Data, image.ContentType);
    }

    [Authorize(Policy = Write)]
    [HttpPut("products/{id:guid}/image")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        await catalog.SetImageAsync(id, file.ContentType, ms.ToArray(), ct);
        return NoContent();
    }

    [Authorize(Policy = Write)]
    [HttpDelete("products/{id:guid}/image")]
    public async Task<IActionResult> DeleteImage(Guid id, CancellationToken ct)
    {
        await catalog.DeleteImageAsync(id, ct);
        return NoContent();
    }

    [Authorize(Policy = Write)]
    [HttpPost("products")]
    public Task<ProductDto> CreateProduct(SaveProductRequest r, CancellationToken ct) => catalog.CreateProductAsync(r, ct);

    [Authorize(Policy = Write)]
    [HttpPut("products/{id:guid}")]
    public Task<ProductDto> UpdateProduct(Guid id, SaveProductRequest r, CancellationToken ct) => catalog.UpdateProductAsync(id, r, ct);

    [HttpGet("colors")]
    public Task<IReadOnlyList<CodedItemDto>> Colors(CancellationToken ct) => catalog.ListColorsAsync(ct);

    [Authorize(Policy = Write)]
    [HttpPost("colors")]
    public Task<CodedItemDto> CreateColor(SaveCodedItemRequest r, CancellationToken ct) => catalog.CreateColorAsync(r, ct);

    [Authorize(Policy = Write)]
    [HttpPut("colors/{id:guid}")]
    public Task<CodedItemDto> UpdateColor(Guid id, SaveCodedItemRequest r, CancellationToken ct) => catalog.UpdateColorAsync(id, r, ct);

    [HttpGet("soles")]
    public Task<IReadOnlyList<CodedItemDto>> Soles(CancellationToken ct) => catalog.ListSolesAsync(ct);

    [Authorize(Policy = Write)]
    [HttpPost("soles")]
    public Task<CodedItemDto> CreateSole(SaveCodedItemRequest r, CancellationToken ct) => catalog.CreateSoleAsync(r, ct);

    [Authorize(Policy = Write)]
    [HttpPut("soles/{id:guid}")]
    public Task<CodedItemDto> UpdateSole(Guid id, SaveCodedItemRequest r, CancellationToken ct) => catalog.UpdateSoleAsync(id, r, ct);

    [HttpGet("routes")]
    public Task<IReadOnlyList<RouteDto>> Routes(CancellationToken ct) => catalog.ListRoutesAsync(ct);
}
