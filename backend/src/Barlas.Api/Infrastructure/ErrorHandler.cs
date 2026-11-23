using Barlas.Application.Common;
using Barlas.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Api.Infrastructure;

/// <summary>تبدیل خطاهای دامنه و برنامه به ProblemDetails با پیام فارسی قابل نمایش.</summary>
public sealed class ErrorHandler(IProblemDetailsService problems, ILogger<ErrorHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            DomainException d => (StatusCodes.Status422UnprocessableEntity, d.Message),
            AppException a => (a.StatusCode, a.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "هم‌زمان کاربر دیگری این مورد را تغییر داد؛ دوباره تلاش کنید."),
            _ => (StatusCodes.Status500InternalServerError, "خطای پیش‌بینی‌نشده رخ داد."),
        };

        if (status >= 500) logger.LogError(ex, "Unhandled exception");

        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }
}
