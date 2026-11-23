import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { post } from "../api/client";
import { ErrorText, Field } from "../components/Field";

export function ChangePasswordPage() {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [repeat, setRepeat] = useState("");
  const [mismatch, setMismatch] = useState(false);

  const change = useMutation({
    mutationFn: () => post("/auth/password", { currentPassword: current, newPassword: next }),
    onSuccess: () => {
      setCurrent("");
      setNext("");
      setRepeat("");
    },
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    setMismatch(next !== repeat);
    if (next === repeat) change.mutate();
  }

  return (
    <section className="narrow">
      <h2>تغییر رمز</h2>
      <form className="card" onSubmit={submit}>
        <Field label="رمز فعلی">
          <input type="password" value={current} onChange={(e) => setCurrent(e.target.value)} dir="ltr" autoComplete="current-password" required />
        </Field>
        <Field label="رمز جدید" hint="حداقل ۸ کاراکتر، شامل حرف و عدد">
          <input type="password" value={next} onChange={(e) => setNext(e.target.value)} dir="ltr" autoComplete="new-password" required />
        </Field>
        <Field label="تکرار رمز جدید">
          <input type="password" value={repeat} onChange={(e) => setRepeat(e.target.value)} dir="ltr" autoComplete="new-password" required />
        </Field>
        {mismatch && <p className="error">تکرار رمز با رمز جدید یکی نیست.</p>}
        <ErrorText error={change.error} />
        {change.isSuccess && <p className="ok">رمز شما تغییر کرد.</p>}
        <button type="submit" disabled={change.isPending}>
          ذخیره
        </button>
      </form>
    </section>
  );
}
