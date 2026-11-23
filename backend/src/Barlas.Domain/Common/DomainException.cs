namespace Barlas.Domain.Common;

/// <summary>نقض یک قاعده کسب‌وکار. پیام برای نمایش به کاربر (فارسی) است.</summary>
public class DomainException(string message) : Exception(message);
