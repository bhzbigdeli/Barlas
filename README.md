# بارلاس — نرم‌افزار برنامه‌ریزی و پیگیری تولید

سفارش‌های سیستم فروش تهران را خودکار به واحد مربوط می‌رساند، به زیربرنامه و دسته‌های کار تبدیل می‌کند،
پیشرفت هر دسته را با اسکن کاردکس QR تا ارسال نشان می‌دهد و مازاد تولید را به موجودی آزاد انبار می‌برد.

طرح کلی مصوب: [سند طرح کلی](https://claude.ai/artifact/QfZQZE2Z6kfyhj9xgi7fTz) · معماری: [docs/architecture.md](docs/architecture.md)

**ترتیب اجرا:** آزمایشی فاز ۱ اول در **واحد اصلی**، بعد **سالن صندل**.

## پشته فنی

| بخش | فناوری |
|---|---|
| بک‌اند | ASP.NET Core 10 (Web API)، EF Core 10 |
| دیتابیس | SQL Server 2022 |
| فرانت | React 19 + Vite + TypeScript، راست‌چین، فونت وزیرمتن، PWA برای گوشی |
| استقرار | Docker Compose روی سرور داخل کارخانه |

## ساختار

```
backend/
  src/Barlas.Domain          موجودیت‌ها و قواعد کسب‌وکار (بدون وابستگی)
  src/Barlas.Application     سرویس‌های کاربردی و رابط‌ها (دریافت سفارش، اسکن، ورود)
  src/Barlas.Infrastructure  EF Core/SQL Server، migrationها، خواندن اکسل، seed
  src/Barlas.Api             کنترلرها، JWT، نقش‌ها، مدیریت خطا
  tests/Barlas.Domain.Tests  آزمون‌های چرخه کار
frontend/                    وب‌اپ React (داشبورد، سفارش‌ها، اسکن)
docs/                        مستندات
```

## اجرای محلی برای توسعه

پیش‌نیاز: .NET SDK 10، Node 22+، Docker (برای SQL Server).

```bash
docker compose -f docker-compose.dev.yml up -d
```

```bash
cd backend && dotnet run --project src/Barlas.Api
```

```bash
cd frontend && npm install && npm run dev
```

سپس http://localhost:5173 — کاربر `admin` با رمز `Admin_Dev_123!` (فقط محیط توسعه).
در Development داده نمونه (یک مدل، دو رنگ، سری سایز ۴۰–۴۴، دو کارگر در واحد اصلی) هم ساخته می‌شود.

> روی مک با تراشه Apple، ایمیج SQL Server با شبیه‌سازی amd64 اجرا می‌شود (Docker Desktop → Rosetta روشن باشد).

## آزمون‌ها

```bash
cd backend && dotnet test Barlas.slnx
```

```bash
cd frontend && npm test && npm run typecheck
```

## استقرار روی سرور کارخانه

```bash
cp .env.example .env
```

مقادیر `.env` را پر کنید (رمزها، کلید JWT، کدهای تامین‌کننده واقعی هر واحد)، سپس:

```bash
docker compose up -d --build
```

migrationها هنگام شروع API خودکار اعمال می‌شوند (`Database__MigrateOnStartup=false` برای خاموش‌کردن).
دوربین گوشی برای اسکن QR فقط روی HTTPS کار می‌کند؛ برای شبکه داخلی یک گواهی داخلی روی nginx لازم است.

## migration جدید

```bash
cd backend && dotnet tool restore && dotnet ef migrations add <Name> -p src/Barlas.Infrastructure -s src/Barlas.Infrastructure -o Persistence/Migrations
```
