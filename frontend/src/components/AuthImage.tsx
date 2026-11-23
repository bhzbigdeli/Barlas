import { useEffect, useState } from "react";
import { tokenStore } from "../api/client";

/** عکسی که فقط با توکن قابل دریافت است (img معمولی هدر Authorization نمی‌فرستد). */
export function AuthImage({ src, alt, className }: { src: string; alt: string; className?: string }) {
  const [url, setUrl] = useState<string>();
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let objectUrl: string | undefined;
    let cancelled = false;
    setFailed(false);
    const token = tokenStore.get();
    fetch(`/api${src}`, { headers: token ? { Authorization: `Bearer ${token}` } : {} })
      .then((r) => (r.ok ? r.blob() : Promise.reject(new Error(String(r.status)))))
      .then((blob) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch(() => !cancelled && setFailed(true));
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [src]);

  if (failed) return <div className={`img-placeholder ${className ?? ""}`}>بدون عکس</div>;
  if (!url) return <div className={`img-placeholder ${className ?? ""}`} aria-busy="true" />;
  return <img src={url} alt={alt} className={className} />;
}
