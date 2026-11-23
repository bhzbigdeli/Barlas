// قالب‌بندی فارسی: تاریخ شمسی و ارقام فارسی، بدون کتابخانه اضافه (Intl).

const dateFmt = new Intl.DateTimeFormat("fa-IR-u-ca-persian", {
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  timeZone: "Asia/Tehran", // روز کاری کارخانه، مستقل از ساعت دستگاه
});

const numberFmt = new Intl.NumberFormat("fa-IR");

/** «2026-11-24» (DateOnly از API) → «۱۴۰۵/۰۹/۰۳» */
export function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  // DateOnly بدون ساعت می‌آید؛ ظهر UTC تا جابه‌جایی منطقه زمانی روز را عوض نکند.
  // زمان‌های ذخیره‌شده UTC‌اند ولی از دیتابیس گاهی بدون Z می‌آیند؛ بدون Z مرورگر آن را محلی می‌خواند.
  const iso = value.length === 10 ? `${value}T12:00:00Z` : /[zZ]|[+-]\d\d:?\d\d$/.test(value) ? value : `${value}Z`;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "—" : dateFmt.format(d);
}

export function formatNumber(n: number): string {
  return numberFmt.format(n);
}

/** ارقام فارسی/عربی ورودی کاربر را لاتین می‌کند (مثلاً کد پرسنلی). */
export function toLatinDigits(s: string): string {
  return s
    .replace(/[۰-۹]/g, (c) => String(c.charCodeAt(0) - 0x06f0))
    .replace(/[٠-٩]/g, (c) => String(c.charCodeAt(0) - 0x0660));
}
