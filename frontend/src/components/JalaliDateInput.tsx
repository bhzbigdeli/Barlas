import { useEffect, useState } from "react";
import { isoToJalali, jalaliToIso } from "../lib/jalali";

/** ورود تاریخ شمسی به‌صورت ۱۴۰۵/۰۹/۰۳؛ مقدار بیرونی ISO میلادی است (یا "" برای خالی). */
export function JalaliDateInput({
  value,
  onChange,
  label,
  required,
}: {
  value: string;
  onChange: (iso: string) => void;
  label: string;
  required?: boolean;
}) {
  const [text, setText] = useState(isoToJalali(value));
  useEffect(() => {
    if (jalaliToIso(text) !== (value || null)) setText(isoToJalali(value));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value]);
  const invalid = text.trim() !== "" && jalaliToIso(text) === null;

  return (
    <input
      className={`date-input ${invalid ? "invalid" : ""}`}
      aria-label={label}
      aria-invalid={invalid}
      placeholder="۱۴۰۵/۰۹/۰۳"
      dir="ltr"
      inputMode="numeric"
      required={required}
      value={text}
      onChange={(e) => {
        setText(e.target.value);
        const iso = jalaliToIso(e.target.value);
        if (iso) onChange(iso);
        else if (e.target.value.trim() === "") onChange("");
      }}
    />
  );
}
