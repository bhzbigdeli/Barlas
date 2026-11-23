-- ساخت دیتابیس و کاربر جدای برلاس روی SQL Server موجود.
-- قبل از اجرا رمز را عوض کنید (همان رمزی که در appsettings.Production.json می‌گذارید).
-- اجرا در SSMS با کاربری که دسترسی sysadmin دارد.
USE [master];
GO
IF DB_ID(N'Barlas') IS NULL
    CREATE DATABASE [Barlas] COLLATE Persian_100_CI_AI;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'barlas_app')
    CREATE LOGIN [barlas_app] WITH PASSWORD = N'رمز_قوی_را_اینجا_بگذارید', CHECK_POLICY = ON, DEFAULT_DATABASE = [Barlas];
GO
USE [Barlas];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'barlas_app')
    CREATE USER [barlas_app] FOR LOGIN [barlas_app];
-- برنامه هنگام شروع جدول‌ها را می‌سازد (migration)، پس مالک همین دیتابیس است؛ به دیتابیس‌های دیگر دسترسی ندارد.
ALTER ROLE [db_owner] ADD MEMBER [barlas_app];
GO
