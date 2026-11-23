import { formatNumber } from "../lib/format";

export function ProgressBar({ percent, large }: { percent: number; large?: boolean }) {
  const p = Math.max(0, Math.min(100, percent));
  return (
    <div className={`progress ${large ? "large" : ""}`} role="progressbar" aria-valuenow={p} aria-valuemin={0} aria-valuemax={100}>
      <div className="progress-fill" style={{ width: `${p}%` }} />
      <span className="progress-label">{formatNumber(p)}٪</span>
    </div>
  );
}

/** دیرکرد نسبت به تاریخ پیش‌بینی‌شده: مثبت دیر، منفی زودتر. */
export function DelayBadge({ days, done }: { days: number | null; done: boolean }) {
  if (days === null) return done ? <span className="badge ok">انجام شد</span> : null;
  if (days > 0) return <span className="badge bad">{formatNumber(days)} روز {done ? "دیرتر" : "تأخیر"}</span>;
  if (days < 0) return <span className="badge ok">{formatNumber(-days)} روز زودتر</span>;
  return <span className="badge ok">به‌موقع</span>;
}
