# CLAUDE.md

نرم‌افزار برنامه‌ریزی و پیگیری تولید کارخانه کفش (بارلاس). طرح مصوب: https://claude.ai/artifact/QfZQZE2Z6kfyhj9xgi7fTz
معماری و نگاشت سند به کد: `docs/architecture.md`.

## زبان
- با کاربر فارسی صحبت کن. متن رابط کاربری و پیام‌های خطای قابل نمایش فارسی؛ نام‌های کد انگلیسی.
- رابط کاربری راست‌چین (`dir="rtl"`)، تاریخ‌ها شمسی در نمایش (Intl `fa-IR-u-ca-persian`)، ذخیره‌سازی میلادی/UTC.

## اولویت‌ها
- فاز ۱ اول در **واحد اصلی** اجرا می‌شود، بعد **سالن صندل**.

## پشته
- backend: .NET 10، ASP.NET Core Web API، EF Core 10، SQL Server. راه‌حل: `backend/Barlas.slnx`.
- frontend: React 19، Vite، TypeScript، TanStack Query، React Router 7.

## دستورها
- تست بک‌اند: `cd backend && dotnet test Barlas.slnx`
- تست فرانت: `cd frontend && npm test && npm run typecheck`
- migration: `cd backend && dotnet ef migrations add <Name> -p src/Barlas.Infrastructure -s src/Barlas.Infrastructure -o Persistence/Migrations`

## قواعد کد
- قواعد کسب‌وکار در Domain (متدهای موجودیت)، نه در کنترلر. کنترلرها نازک‌اند.
- دسترسی با مجوز (`Permission`) است نه نام نقش: `[Authorize(Policy = nameof(Permission.X))]` و در فرانت `can("X")`.
- هر محصول سری سایز خودش را دارد (`ShoeModel.Series`)؛ سری سراسری نداریم.
- رابط کاربری: منوی کناری سمت راست (`components/Layout.tsx`)؛ آیتم جدید را با مجوزش آنجا اضافه کن.
- هر موجودیت مال یک واحد `IUnitScoped` را پیاده کند تا فیلتر واحد خودکار اعمال شود؛ `IgnoreQueryFilters()` فقط با توضیح.
- شناسه‌ها `Guid` و در دامنه ساخته می‌شوند (`ValueGeneratedNever`). فرزند جدیدِ والدِ ردیابی‌شده را صریحاً `Add` کن.
- بعد از تغییر مدل، migration بساز؛ CI با `has-pending-model-changes` بررسی می‌کند.
- `TreatWarningsAsErrors` روشن است.
