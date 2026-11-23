import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { api, post, put } from "../../api/client";
import type { Unit, Worker } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { ErrorText } from "../../components/Field";
import { toLatinDigits } from "../../lib/format";

/** کارگران سالن: کد پرسنلی همان است که هنگام اسکن کاردکس وارد می‌شود. */
export function WorkersPage() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const workers = useQuery({ queryKey: ["workers"], queryFn: () => api<Worker[]>("/workers") });
  const units = useQuery({ queryKey: ["units"], queryFn: () => api<Unit[]>("/units") });
  const [form, setForm] = useState({ unitId: "", personnelCode: "", fullName: "" });

  const create = useMutation({
    mutationFn: () =>
      post<Worker>("/workers", {
        unitId: form.unitId || units.data?.[0]?.id || null,
        personnelCode: toLatinDigits(form.personnelCode),
        fullName: form.fullName,
      }),
    onSuccess: () => {
      setForm({ ...form, personnelCode: "", fullName: "" });
      qc.invalidateQueries({ queryKey: ["workers"] });
    },
  });
  const update = useMutation({
    mutationFn: (w: Worker) => put<Worker>(`/workers/${w.id}`, { fullName: w.fullName, isActive: w.isActive }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["workers"] }),
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    create.mutate();
  }

  return (
    <section>
      <h2>کارگران</h2>

      <form className="card" onSubmit={submit}>
        <h3>کارگر جدید</h3>
        <div className="inline-form">
          {user?.seesAllUnits && (
            <select
              aria-label="واحد"
              value={form.unitId || units.data?.[0]?.id || ""}
              onChange={(e) => setForm({ ...form, unitId: e.target.value })}
            >
              {units.data?.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </select>
          )}
          <input
            placeholder="کد پرسنلی"
            aria-label="کد پرسنلی"
            inputMode="numeric"
            dir="ltr"
            value={form.personnelCode}
            onChange={(e) => setForm({ ...form, personnelCode: e.target.value })}
            required
          />
          <input
            placeholder="نام و نام خانوادگی"
            aria-label="نام و نام خانوادگی"
            value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
            required
          />
          <button type="submit" disabled={create.isPending}>
            افزودن
          </button>
        </div>
        <ErrorText error={create.error} />
      </form>

      <ErrorText error={workers.error ?? update.error} />
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>کد پرسنلی</th>
              <th>نام</th>
              <th>واحد</th>
              <th>وضعیت</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {workers.data?.map((w) => (
              <tr key={w.id} className={w.isActive ? "" : "inactive"}>
                <td dir="ltr">{w.personnelCode}</td>
                <td>{w.fullName}</td>
                <td>{w.unitName}</td>
                <td>{w.isActive ? "فعال" : "غیرفعال"}</td>
                <td>
                  <button className="link" onClick={() => update.mutate({ ...w, isActive: !w.isActive })}>
                    {w.isActive ? "غیرفعال کردن" : "فعال کردن"}
                  </button>
                </td>
              </tr>
            ))}
            {workers.data?.length === 0 && (
              <tr>
                <td colSpan={5} className="muted">
                  هنوز کارگری تعریف نشده.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}
