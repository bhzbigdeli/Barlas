#!/usr/bin/env bash
# ساخت بسته نصب بارلاس برای IIS (ویندوز ۶۴ بیتی، بدون نیاز به نصب .NET روی سرور).
# اجرا از ریشه مخزن:  ./deploy/iis/publish.sh
# خروجی: deploy/out/barlas-iis-<تاریخ>.zip  (شامل اطلاعات پایه واقعی؛ در گیت نیست)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="$ROOT/deploy/out"
STAGE="$OUT/barlas-iis"
SEED="$ROOT/backend/src/Barlas.Api/SeedData/catalog.json"

[ -f "$SEED" ] || { echo "فایل اطلاعات پایه پیدا نشد: $SEED" >&2; exit 1; }

rm -rf "$STAGE"
mkdir -p "$STAGE"

echo "== فرانت"
npm --prefix "$ROOT/frontend" ci --no-audit --no-fund
npm --prefix "$ROOT/frontend" run build

echo "== بک‌اند (win-x64، self-contained)"
dotnet publish "$ROOT/backend/src/Barlas.Api/Barlas.Api.csproj" -c Release -r win-x64 --self-contained true \
  -p:PublishReadyToRun=false -o "$STAGE/app"

mkdir -p "$STAGE/app/wwwroot" "$STAGE/app/logs"
cp -R "$ROOT/frontend/dist/." "$STAGE/app/wwwroot/"
rm -f "$STAGE/app/appsettings.Development.json"
test -f "$STAGE/app/SeedData/catalog.json"

cp "$ROOT/deploy/iis/create-database.sql" "$ROOT/deploy/iis/appsettings.Production.example.json" "$ROOT/deploy/iis/README.md" "$STAGE/"

STAMP="$(date +%Y%m%d-%H%M)"
ZIP="$OUT/barlas-iis-$STAMP.zip"
(cd "$OUT" && rm -f "$ZIP" && zip -qr "$ZIP" barlas-iis)
echo "== آماده: $ZIP ($(du -h "$ZIP" | cut -f1))"
