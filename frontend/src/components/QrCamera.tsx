import QrScanner from "qr-scanner";
import { useEffect, useRef, useState } from "react";

/** دوربین زنده (getUserMedia) فقط در صفحه امن (HTTPS یا localhost) وجود دارد؛ در آیفون روی http اصلاً تعریف نشده است. */
const liveCameraAvailable = () => window.isSecureContext && !!navigator.mediaDevices?.getUserMedia;

function cameraError(e: unknown): string {
  const name = e instanceof DOMException ? e.name : typeof e === "string" ? e : "";
  if (name === "NotAllowedError" || name === "SecurityError")
    return "اجازه دسترسی به دوربین داده نشده. در آیفون: Settings ← Safari ← Camera ← Allow، سپس صفحه را دوباره باز کنید.";
  if (name === "NotFoundError" || /camera not found/i.test(String(e))) return "دوربینی روی این دستگاه پیدا نشد.";
  if (name === "NotReadableError") return "دوربین در اختیار برنامه دیگری است؛ آن برنامه را ببندید و دوباره امتحان کنید.";
  return "دوربین باز نشد. اگر لینک را از داخل تلگرام، واتساپ یا اینستاگرام باز کرده‌اید، آن را در Safari باز کنید.";
}

/**
 * خواندن QR کاردکس: دوربین زنده، و اگر در دسترس نبود (مثلاً سایت روی http)، عکس گرفتن با دوربین گوشی
 * که روی همه آیفون‌ها و بدون HTTPS هم کار می‌کند.
 */
export function QrCamera({ onResult }: { onResult: (text: string) => void }) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const [live, setLive] = useState(false);
  const [message, setMessage] = useState<string>();
  const [reading, setReading] = useState(false);
  const onResultRef = useRef(onResult);
  onResultRef.current = onResult;

  useEffect(() => {
    if (!live || !videoRef.current) return;
    const scanner = new QrScanner(
      videoRef.current,
      (result) => {
        setLive(false);
        onResultRef.current(result.data.trim());
      },
      { preferredCamera: "environment", highlightScanRegion: true, returnDetailedScanResult: true },
    );
    scanner.start().catch((e) => {
      setLive(false);
      setMessage(cameraError(e));
    });
    return () => scanner.destroy();
  }, [live]);

  function openLive() {
    setMessage(undefined);
    if (!liveCameraAvailable()) {
      setMessage(
        window.isSecureContext
          ? "این مرورگر دوربین زنده ندارد. از «عکس گرفتن از کاردکس» استفاده کنید."
          : "دوربین زنده فقط وقتی کار می‌کند که آدرس سایت با https:// شروع شود. فعلاً از «عکس گرفتن از کاردکس» استفاده کنید.",
      );
      return;
    }
    setLive(true);
  }

  async function readPhoto(file: File) {
    setMessage(undefined);
    setReading(true);
    try {
      const result = await QrScanner.scanImage(file, { returnDetailedScanResult: true });
      onResult(result.data.trim());
    } catch {
      setMessage("QR در عکس پیدا نشد. از نزدیک‌تر، صاف و با نور کافی دوباره عکس بگیرید.");
    } finally {
      setReading(false);
    }
  }

  return (
    <div className="qr-camera">
      {live ? (
        <div className="video-box">
          <video ref={videoRef} muted playsInline autoPlay />
          <button className="secondary" onClick={() => setLive(false)}>
            بستن دوربین
          </button>
        </div>
      ) : (
        <div className="actions">
          <button onClick={openLive}>باز کردن دوربین</button>
          <button className="secondary" onClick={() => fileRef.current?.click()} disabled={reading}>
            {reading ? "در حال خواندن…" : "عکس گرفتن از کاردکس"}
          </button>
        </div>
      )}
      <input
        ref={fileRef}
        type="file"
        accept="image/*"
        capture="environment"
        hidden
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (f) void readPhoto(f);
          e.target.value = "";
        }}
      />
      {message && <p className="error">{message}</p>}
    </div>
  );
}
