import { useState } from "react";
import type { SeriesSize } from "../../api/types";
import { formatNumber } from "../../lib/format";

export const MIN_SIZE = 15;
export const MAX_SIZE = 50;

/**
 * ویرایش سری یک محصول: بازه سایز را تعیین می‌کنید (مثلاً ۳۷ تا ۴۲)
 * و برای هر سایز تعداد جفت در یک سری را می‌نویسید.
 */
export function SeriesEditor({
  value,
  onChange,
  disabled,
}: {
  value: SeriesSize[];
  onChange: (series: SeriesSize[]) => void;
  disabled?: boolean;
}) {
  const sorted = [...value].sort((a, b) => a.size - b.size);
  const [from, setFrom] = useState(sorted[0]?.size ?? 37);
  const [to, setTo] = useState(sorted.at(-1)?.size ?? 42);

  const total = value.reduce((sum, s) => sum + (s.quantity || 0), 0);
  const rangeValid = from >= MIN_SIZE && to <= MAX_SIZE && from <= to;

  /** ساخت ردیف‌های بازه؛ تعداد سایزهایی که قبلاً بوده حفظ می‌شود، سایز تازه با ۱ شروع می‌شود. */
  function applyRange() {
    if (!rangeValid) return;
    const existing = new Map(value.map((s) => [s.size, s.quantity]));
    const next: SeriesSize[] = [];
    for (let size = from; size <= to; size++) next.push({ size, quantity: existing.get(size) ?? 1 });
    onChange(next);
  }

  function setQuantity(size: number, quantity: number) {
    onChange(value.map((s) => (s.size === size ? { ...s, quantity } : s)));
  }

  function removeSize(size: number) {
    onChange(value.filter((s) => s.size !== size));
  }

  return (
    <div className="series">
      <div className="series-range">
        <label>
          از سایز
          <input type="number" min={MIN_SIZE} max={MAX_SIZE} value={from} onChange={(e) => setFrom(+e.target.value)} disabled={disabled} />
        </label>
        <label>
          تا سایز
          <input type="number" min={MIN_SIZE} max={MAX_SIZE} value={to} onChange={(e) => setTo(+e.target.value)} disabled={disabled} />
        </label>
        <button type="button" className="secondary" onClick={applyRange} disabled={disabled || !rangeValid}>
          اعمال بازه
        </button>
      </div>

      {sorted.length === 0 ? (
        <p className="muted">بازه سایز را تعیین کنید و «اعمال بازه» را بزنید.</p>
      ) : (
        <div className="series-grid">
          {sorted.map((s) => (
            <div className="series-cell" key={s.size}>
              <span className="series-size">{formatNumber(s.size)}</span>
              <input
                type="number"
                min={0}
                value={s.quantity}
                onChange={(e) => setQuantity(s.size, Math.max(0, +e.target.value))}
                aria-label={`تعداد سایز ${s.size} در هر سری`}
                disabled={disabled}
              />
              {!disabled && (
                <button type="button" className="link danger" onClick={() => removeSize(s.size)} aria-label={`حذف سایز ${s.size}`}>
                  حذف
                </button>
              )}
            </div>
          ))}
        </div>
      )}

      <p>
        هر سری: <strong>{formatNumber(total)}</strong> جفت
        {sorted.length > 0 && (
          <span className="muted">
            {" "}
            · سایز {formatNumber(sorted[0].size)} تا {formatNumber(sorted.at(-1)!.size)}
          </span>
        )}
      </p>
    </div>
  );
}
