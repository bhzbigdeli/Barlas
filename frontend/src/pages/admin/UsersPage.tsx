import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { api, post, put } from "../../api/client";
import type { Role, Unit, UserRow } from "../../api/types";
import { ErrorText, Field } from "../../components/Field";

interface Draft {
  id?: string;
  userName: string;
  fullName: string;
  roleId: string;
  unitId: string;
  password: string;
}

const empty: Draft = { userName: "", fullName: "", roleId: "", unitId: "", password: "" };

/** تعریف کاربران: ساخت، ویرایش سطح و واحد، تغییر رمز، فعال/غیرفعال. */
export function UsersPage() {
  const qc = useQueryClient();
  const users = useQuery({ queryKey: ["users"], queryFn: () => api<UserRow[]>("/users") });
  const roles = useQuery({ queryKey: ["roles"], queryFn: () => api<Role[]>("/roles") });
  const units = useQuery({ queryKey: ["units"], queryFn: () => api<Unit[]>("/units") });

  const [draft, setDraft] = useState<Draft | null>(null);
  const [resetFor, setResetFor] = useState<UserRow | null>(null);
  const [newPassword, setNewPassword] = useState("");

  const selectedRole = roles.data?.find((r) => r.id === draft?.roleId);
  const needsUnit = selectedRole ? !(selectedRole.isSystem || selectedRole.permissions.includes("ViewAllUnits")) : true;

  const save = useMutation({
    mutationFn: (d: Draft) => {
      const unitId = needsUnit ? d.unitId || null : null;
      return d.id
        ? put<UserRow>(`/users/${d.id}`, { fullName: d.fullName, roleId: d.roleId, unitId })
        : post<UserRow>("/users", { userName: d.userName, fullName: d.fullName, roleId: d.roleId, unitId, password: d.password });
    },
    onSuccess: () => {
      setDraft(null);
      return qc.invalidateQueries({ queryKey: ["users"] });
    },
  });

  const toggle = useMutation({
    mutationFn: (u: UserRow) => post<UserRow>(`/users/${u.id}/${u.isActive ? "deactivate" : "activate"}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["users"] }),
  });

  const reset = useMutation({
    mutationFn: () => post(`/users/${resetFor!.id}/password`, { newPassword }),
    onSuccess: () => {
      setResetFor(null);
      setNewPassword("");
    },
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    if (draft) save.mutate(draft);
  }

  return (
    <section>
      <div className="row">
        <h2>کاربران</h2>
        <button
          onClick={() => {
            save.reset();
            setDraft({ ...empty, roleId: roles.data?.find((r) => !r.isSystem)?.id ?? "" });
          }}
        >
          کاربر جدید
        </button>
      </div>

      {draft && (
        <form className="card" onSubmit={submit}>
          <h3>{draft.id ? `ویرایش ${draft.userName}` : "کاربر جدید"}</h3>
          <div className="grid-2">
            <Field label="نام کاربری">
              <input
                value={draft.userName}
                onChange={(e) => setDraft({ ...draft, userName: e.target.value })}
                dir="ltr"
                disabled={!!draft.id}
                required
              />
            </Field>
            <Field label="نام و نام خانوادگی">
              <input value={draft.fullName} onChange={(e) => setDraft({ ...draft, fullName: e.target.value })} required />
            </Field>
            <Field label="سطح دسترسی">
              <select value={draft.roleId} onChange={(e) => setDraft({ ...draft, roleId: e.target.value })} required>
                <option value="" disabled>
                  انتخاب کنید
                </option>
                {roles.data?.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="واحد" hint={needsUnit ? undefined : "این سطح همه واحدها را می‌بیند."}>
              <select
                value={needsUnit ? draft.unitId : ""}
                onChange={(e) => setDraft({ ...draft, unitId: e.target.value })}
                disabled={!needsUnit}
                required={needsUnit}
              >
                <option value="">{needsUnit ? "انتخاب کنید" : "همه واحدها"}</option>
                {units.data?.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
            </Field>
            {!draft.id && (
              <Field label="رمز عبور اولیه" hint="حداقل ۸ کاراکتر، شامل حرف و عدد">
                <input
                  type="password"
                  value={draft.password}
                  onChange={(e) => setDraft({ ...draft, password: e.target.value })}
                  dir="ltr"
                  autoComplete="new-password"
                  required
                />
              </Field>
            )}
          </div>
          <ErrorText error={save.error} />
          <div className="actions">
            <button type="submit" disabled={save.isPending}>
              ذخیره
            </button>
            <button type="button" className="secondary" onClick={() => setDraft(null)}>
              انصراف
            </button>
          </div>
        </form>
      )}

      {resetFor && (
        <form
          className="card"
          onSubmit={(e) => {
            e.preventDefault();
            reset.mutate();
          }}
        >
          <h3>رمز جدید برای {resetFor.fullName}</h3>
          <Field label="رمز جدید" hint="حداقل ۸ کاراکتر، شامل حرف و عدد">
            <input
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              dir="ltr"
              autoComplete="new-password"
              required
            />
          </Field>
          <ErrorText error={reset.error} />
          <div className="actions">
            <button type="submit" disabled={reset.isPending}>
              ثبت رمز
            </button>
            <button type="button" className="secondary" onClick={() => setResetFor(null)}>
              انصراف
            </button>
          </div>
        </form>
      )}

      <ErrorText error={users.error ?? toggle.error} />
      {reset.isSuccess && !resetFor && <p className="ok">رمز تغییر کرد.</p>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>نام کاربری</th>
              <th>نام</th>
              <th>سطح دسترسی</th>
              <th>واحد</th>
              <th>وضعیت</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {users.data?.map((u) => (
              <tr key={u.id} className={u.isActive ? "" : "inactive"}>
                <td dir="ltr">{u.userName}</td>
                <td>{u.fullName}</td>
                <td>{u.roleName}</td>
                <td>{u.unitName ?? "همه واحدها"}</td>
                <td>{u.isActive ? "فعال" : "غیرفعال"}</td>
                <td className="row-actions">
                  <button
                    className="link"
                    onClick={() => {
                      save.reset();
                      setDraft({ id: u.id, userName: u.userName, fullName: u.fullName, roleId: u.roleId, unitId: u.unitId ?? "", password: "" });
                    }}
                  >
                    ویرایش
                  </button>
                  <button className="link" onClick={() => (reset.reset(), setResetFor(u))}>
                    تغییر رمز
                  </button>
                  <button className="link" onClick={() => toggle.mutate(u)}>
                    {u.isActive ? "غیرفعال کردن" : "فعال کردن"}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
