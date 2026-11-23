# نصب بارلاس روی IIS

بارلاس یک سایت جدا در همان IIS است و با سایت‌های دیگر سرور تداخلی ندارد. صفحه‌های وب و API هر دو از یک برنامه
سرو می‌شوند. برنامه «self-contained» است، پس نصب ‎.NET 10 روی سرور لازم نیست.

## پیش‌نیازها

- **ماژول ASP.NET Core در IIS** (AspNetCoreModuleV2). اگر سایت ‎.NET Core دیگری روی سرور دارید، نصب است.
  برای بررسی: IIS Manager ← سرور ← Modules ← باید `AspNetCoreModuleV2` در فهرست باشد.
  اگر نبود: یکی از نسخه‌های «ASP.NET Core Hosting Bundle» را نصب کنید (هنگام نصب IIS چند ثانیه ری‌استارت می‌شود).
- SQL Server (همان نسخه موجود روی سرور).
- یک دامنه یا زیردامنه برای بارلاس، مثلاً `barlas.example.ir`، که به IP سرور اشاره کند.
- **HTTPS** برای اسکن کاردکس با دوربین گوشی لازم است (گواهی روی IIS یا SSL از CDN آروان).

## ۱. دیتابیس

فایل `create-database.sql` را در SSMS باز کنید، رمز `barlas_app` را عوض کنید و اجرا کنید.
دیتابیس `Barlas` و کاربر `barlas_app` ساخته می‌شود که فقط به همین دیتابیس دسترسی دارد.
جدول‌ها و اطلاعات پایه را خود برنامه در اولین اجرا می‌سازد.

## ۲. فایل‌ها

1. پوشه `app` را مثلاً در `D:\Sites\Barlas` کپی کنید.
2. فایل `appsettings.Production.example.json` را با نام `appsettings.Production.json` کنار `Barlas.Api.exe`
   بگذارید و پر کنید:
   - `ConnectionStrings:Barlas`: نام سرور SQL (مثلاً `.` یا `.\SQLEXPRESS`) و رمز `barlas_app`.
   - `Jwt:SigningKey`: یک رشته تصادفی حداقل ۳۲ کاراکتری. در PowerShell سرور (خروجی ۶۴ کاراکتر):
     `$b = New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); -join ($b | % { $_.ToString("x2") })`
   - `Seed:AdminPassword`: رمز اولیه کاربر `admin` (حداقل ۸ کاراکتر، با حرف و عدد). بعد از اولین ورود عوضش کنید.
   - `Seed:MainSupplierCode` و `SandalSupplierCode`: کد تامین‌کننده هر واحد در سیستم فروش تهران.
   - `ReverseProxy:TrustForwardedHeaders`: فقط اگر سرور **فقط** از پشت CDN آروان در دسترس است `true` کنید.
3. به کاربر Application Pool دسترسی بدهید: روی پوشه `D:\Sites\Barlas` ← Properties ← Security ←
   کاربر `IIS AppPool\Barlas` با دسترسی Read و برای پوشه `logs` دسترسی Modify.

## ۳. سایت در IIS

1. **Application Pools ← Add**: نام `Barlas`، ‏.NET CLR version = **No Managed Code**، Pipeline = Integrated.
2. **Sites ← Add Website**:
   - Site name: `Barlas`، Application pool: `Barlas`
   - Physical path: `D:\Sites\Barlas`
   - Binding: `http`، پورت ۸۰، **Host name** = دامنه بارلاس (تا با سایت‌های دیگر قاطی نشود).
     اگر گواهی دارید، binding ‏`https` روی ۴۴۳ با همان Host name و تیک SNI.
3. سایت را Start کنید و دامنه را باز کنید. اولین بار چند ثانیه طول می‌کشد (ساخت جدول‌ها و ورود ۳۴ محصول).
4. با `admin` و رمزی که در تنظیمات گذاشتید وارد شوید.

برای بررسی سلامت: `https://دامنه/health` باید `Healthy` برگرداند.

## اگر سایت بالا نیامد (خطای 500.30 و مانند آن)

در `web.config` مقدار `stdoutLogEnabled="false"` را `true` کنید، سایت را Recycle کنید و فایل تازه در پوشه
`logs` را ببینید. بعد از رفع مشکل دوباره `false` کنید. Event Viewer ← Application هم خطاها را نشان می‌دهد.

خطاهای رایج:
- اتصال به SQL: نام سرور/instance یا رمز `barlas_app` در `appsettings.Production.json`.
- «Jwt:SigningKey باید حداقل ۳۲ کاراکتر باشد»: کلید کوتاه است.
- «Seed:AdminPassword تنظیم نشده است»: رمز اولیه admin خالی است.

## به‌روزرسانی نسخه

1. یک فایل خالی به نام `app_offline.htm` در پوشه سایت بگذارید (برنامه متوقف می‌شود).
2. فایل‌های پوشه `app` نسخه جدید را روی قبلی کپی کنید، **به جز** `appsettings.Production.json`.
3. `app_offline.htm` را پاک کنید. تغییرات دیتابیس خودکار اعمال می‌شود.

## نکته امنیتی

- پورت SQL Server (۱۴۳۳) را در فایروال آروان باز نکنید.
- `SeedData\catalog.json` شامل نرخ دستمزد واقعی است؛ داخل پوشه سایت است ولی از وب قابل دانلود نیست.
