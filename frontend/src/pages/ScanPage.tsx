import { useMutation } from "@tanstack/react-query";
import QrScanner from "qr-scanner";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { api, post } from "../api/client";
import type { BundleState } from "../api/types";
import { formatNumber, toLatinDigits } from "../lib/format";

const WORKER_KEY = "barlas.lastWorker";

/**
 * اسکن کاردکس با گوشی: QR دسته را می‌خواند، کد پرسنلی کارگر را می‌گیرد و پایان مرحله بعدی را ثبت می‌کند.
 * دوربین فقط روی HTTPS یا localhost کار می‌کند؛ ورود دستی کد همیشه در دسترس است.
 */
export function ScanPage() {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [scanning, setScanning] = useState(false);
  const [qr, setQr] = useState("");
  const [worker, setWorker] = useState(() => {
    try {
      return localStorage.getItem(WORKER_KEY) ?? "";
    } catch {
      return "";
    }
  });

  const lookup = useMutation({ mutationFn: (token: string) => api<BundleState>(`/scan/${encodeURIComponent(token)}`) });
  const complete = useMutation({
    mutationFn: () => post<BundleState>("/scan/complete", { qrToken: qr, workerCode: toLatinDigits(worker) }),
  });

  useEffect(() => {
    if (!scanning || !videoRef.current) return;
    const scanner = new QrScanner(
      videoRef.current,
      (result) => {
        setScanning(false);
        setQr(result.data);
        complete.reset();
        lookup.mutate(result.data);
      },
      { preferredCamera: "environment", highlightScanRegion: true },
    );
    scanner.start().catch(() => setScanning(false));
    return () => scanner.destroy();
  }, [scanning]);

  function submit(e: FormEvent) {
    e.preventDefault();
    try {
      localStorage.setItem(WORKER_KEY, worker);
    } catch {
      /* ignore */
    }
    complete.mutate();
  }

  const bundle = complete.data ?? lookup.data;
  const error = (complete.error ?? lookup.error) as Error | null;

  return (
    <section className="scan">
      <h2>اسکن کاردکس</h2>

      {scanning ? (
        <div className="video-box">
          <video ref={videoRef} muted playsInline />
          <button className="secondary" onClick={() => setScanning(false)}>
            بستن دوربین
          </button>
        </div>
      ) : (
        <button onClick={() => setScanning(true)}>باز کردن دوربین</button>
      )}

      <form className="card" onSubmit={submit}>
        <label>
          کد کاردکس
          <input
            value={qr}
            onChange={(e) => setQr(e.target.value.trim())}
            onBlur={() => qr && lookup.mutate(qr)}
            dir="ltr"
            required
          />
        </label>
        <label>
          کد پرسنلی کارگر
          <input value={worker} onChange={(e) => setWorker(e.target.value)} inputMode="numeric" dir="ltr" required />
        </label>

        {bundle && (
          <div className="bundle">
            <strong>
              دسته {bundle.number} — {bundle.modelName} {bundle.colorName}
            </strong>
            <span>{formatNumber(bundle.pairs)} جفت</span>
            <span>آخرین مرحله: {bundle.lastStage ?? "هنوز شروع نشده"}</span>
            <span>مرحله بعد: {bundle.nextStage ?? "همه مراحل تمام شده"}</span>
          </div>
        )}
        {complete.isSuccess && <p className="ok">ثبت شد.</p>}
        {error && <p className="error">{error.message}</p>}

        <button type="submit" disabled={complete.isPending || !bundle?.nextStage}>
          {bundle?.nextStage ? `ثبت پایان «${bundle.nextStage}»` : "ثبت مرحله"}
        </button>
      </form>
    </section>
  );
}
