namespace Barlas.Application.Common;

/// <summary>خطای قابل‌انتظار لایه Application (مثلاً پیدا نشدن رکورد). در API به ProblemDetails تبدیل می‌شود.</summary>
public class AppException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static AppException NotFound(string what) => new($"{what} پیدا نشد.", 404);
    public static AppException Forbidden() => new("دسترسی به این بخش ندارید.", 403);
}
