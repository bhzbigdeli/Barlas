import type { ReactNode } from "react";

/** برچسب + کنترل فرم، با متن راهنما یا خطای اختیاری. */
export function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
      {hint && <small className="muted">{hint}</small>}
    </label>
  );
}

export function ErrorText({ error }: { error: unknown }) {
  if (!error) return null;
  return <p className="error">{error instanceof Error ? error.message : String(error)}</p>;
}
