using System.Globalization;
using Barlas.Application.Common;
using Barlas.Application.Orders;
using ClosedXML.Excel;

namespace Barlas.Infrastructure.Orders;

/// <summary>
/// خواندن سفارش‌ها از خروجی اکسل سیستم فروش تهران.
/// قالب موقت (تا وقتی قالب واقعی را ببینیم) — ردیف اول سرستون، هر ردیف یک سایز:
/// A کد تامین‌کننده | B شماره سفارش | C تاریخ سفارش | D تاریخ تحویل | E مشتری | F کد مدل | G کد رنگ | H سایز | I تعداد
/// اگر «سایز» خالی باشد، «تعداد» یعنی تعداد سری و طبق سری تعریف‌شده آن محصول باز می‌شود.
/// تاریخ‌ها می‌توانند شمسی (۱۴۰۵/۰۹/۰۳) یا میلادی باشند.
/// </summary>
public sealed class ExcelOrderSource : IOrderSource
{
    public Task<IReadOnlyList<ImportedOrder>> ReadAsync(Stream input, CancellationToken ct)
    {
        using var workbook = new XLWorkbook(input);
        var sheet = workbook.Worksheets.First();
        var rows = new List<Row>();

        foreach (var r in sheet.RowsUsed().Skip(1))
        {
            ct.ThrowIfCancellationRequested();
            var n = r.RowNumber();
            rows.Add(new Row(
                Text(r, 1, n), Text(r, 2, n),
                ParseDate(r.Cell(3), n) ?? throw new AppException($"ردیف {n}: تاریخ سفارش خالی است."),
                ParseDate(r.Cell(4), n),
                r.Cell(5).GetString().Trim() is { Length: > 0 } c ? c : null,
                Text(r, 6, n), Text(r, 7, n),
                r.Cell(8).IsEmpty() ? null : Int(r, 8, n), Int(r, 9, n)));
        }

        IReadOnlyList<ImportedOrder> orders = rows
            .GroupBy(x => (x.Supplier, x.Number))
            .Select(g =>
            {
                var first = g.First();
                var lines = g.GroupBy(x => (x.Model, x.Color))
                    .Select(lg => new ImportedOrderLine(lg.Key.Model, lg.Key.Color,
                        lg.Where(x => x.Size is not null).GroupBy(x => x.Size!.Value).ToDictionary(s => s.Key, s => s.Sum(x => x.Qty)),
                        lg.Any(x => x.Size is null) ? lg.Where(x => x.Size is null).Sum(x => x.Qty) : null))
                    .ToList();
                return new ImportedOrder(first.Supplier, first.Number, first.OrderDate, first.DueDate, first.Customer, lines);
            })
            .ToList();

        return Task.FromResult(orders);
    }

    private sealed record Row(string Supplier, string Number, DateOnly OrderDate, DateOnly? DueDate, string? Customer,
        string Model, string Color, int? Size, int Qty);

    private static string Text(IXLRow r, int col, int n) =>
        r.Cell(col).GetString().Trim() is { Length: > 0 } s ? s : throw new AppException($"ردیف {n}، ستون {col}: خالی است.");

    private static int Int(IXLRow r, int col, int n) =>
        int.TryParse(Normalize(r.Cell(col).GetString()), out var v) ? v : throw new AppException($"ردیف {n}، ستون {col}: عدد نیست.");

    internal static DateOnly? ParseDate(IXLCell cell, int n)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());
        return ParseDateText(cell.GetString()) ?? throw new AppException($"ردیف {n}: تاریخ «{cell.GetString()}» قابل خواندن نیست.");
    }

    /// <summary>«1405/09/03» شمسی، «2026-11-24» میلادی.</summary>
    internal static DateOnly? ParseDateText(string raw)
    {
        var parts = Normalize(raw).Split('/', '-', '.');
        if (parts.Length != 3 || !parts.All(p => int.TryParse(p, out _))) return null;
        int y = int.Parse(parts[0]), m = int.Parse(parts[1]), d = int.Parse(parts[2]);
        try
        {
            return y < 1700
                ? DateOnly.FromDateTime(new PersianCalendar().ToDateTime(y, m, d, 0, 0, 0, 0))
                : new DateOnly(y, m, d);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>ارقام فارسی/عربی را به لاتین تبدیل می‌کند.</summary>
    internal static string Normalize(string s)
    {
        var chars = s.Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c is >= '۰' and <= '۹') chars[i] = (char)('0' + (c - '۰'));
            else if (c is >= '٠' and <= '٩') chars[i] = (char)('0' + (c - '٠'));
        }
        return new string(chars);
    }
}
