import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { api, del, post, put } from "../../api/client";
import type { Permission, PermissionInfo, Role } from "../../api/types";
import { ErrorText, Field } from "../../components/Field";

interface Draft {
  id?: string;
  name: string;
  isSystem: boolean;
  permissions: Permission[];
}

/** تعریف سطوح دسترسی: هر سطح یک نام و مجموعه‌ای از مجوزها. */
export function RolesPage() {
  const qc = useQueryClient();
  const roles = useQuery({ queryKey: ["roles"], queryFn: () => api<Role[]>("/roles") });
  const catalog = useQuery({ queryKey: ["permissions"], queryFn: () => api<PermissionInfo[]>("/roles/permissions") });
  const [draft, setDraft] = useState<Draft | null>(null);

  const save = useMutation({
    mutationFn: (d: Draft) => {
      const body = { name: d.name, permissions: d.permissions };
      return d.id ? put<Role>(`/roles/${d.id}`, body) : post<Role>("/roles", body);
    },
    onSuccess: () => {
      setDraft(null);
      return qc.invalidateQueries({ queryKey: ["roles"] });
    },
  });

  const remove = useMutation({
    mutationFn: (r: Role) => del(`/roles/${r.id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["roles"] }),
  });

  const label = (p: Permission) => catalog.data?.find((c) => c.permission === p)?.name ?? p;

  function togglePermission(p: Permission) {
    if (!draft || draft.isSystem) return;
    const has = draft.permissions.includes(p);
    setDraft({ ...draft, permissions: has ? draft.permissions.filter((x) => x !== p) : [...draft.permissions, p] });
  }

  function submit(e: FormEvent) {
    e.preventDefault();
    if (draft) save.mutate(draft);
  }

  return (
    <section>
      <div className="row">
        <h2>سطوح دسترسی</h2>
        <button onClick={() => (save.reset(), setDraft({ name: "", isSystem: false, permissions: [] }))}>سطح جدید</button>
      </div>
      <p className="muted">
        هر کاربر یک سطح دسترسی دارد. تغییر مجوزهای یک سطح، بلافاصله برای همه کاربران آن سطح اعمال می‌شود.
      </p>

      {draft && (
        <form className="card" onSubmit={submit}>
          <h3>{draft.id ? `ویرایش «${draft.name}»` : "سطح دسترسی جدید"}</h3>
          <Field label="نام سطح">
            <input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} required />
          </Field>
          {draft.isSystem && <p className="muted">سطح «مالک» همیشه همه مجوزها را دارد.</p>}
          <fieldset className="permissions">
            <legend>مجوزها</legend>
            {catalog.data?.map((p) => (
              <label key={p.permission} className="check">
                <input
                  type="checkbox"
                  checked={draft.isSystem || draft.permissions.includes(p.permission)}
                  disabled={draft.isSystem}
                  onChange={() => togglePermission(p.permission)}
                />
                <span>
                  <strong>{p.name}</strong>
                  <small className="muted">{p.description}</small>
                </span>
              </label>
            ))}
          </fieldset>
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

      <ErrorText error={roles.error ?? remove.error} />

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>سطح</th>
              <th>مجوزها</th>
              <th>کاربران</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {roles.data?.map((r) => (
              <tr key={r.id}>
                <td>
                  <strong>{r.name}</strong>
                </td>
                <td className="wrap">{r.isSystem ? "همه مجوزها" : r.permissions.map(label).join("، ") || "—"}</td>
                <td>{r.userCount}</td>
                <td className="row-actions">
                  <button
                    className="link"
                    onClick={() => (save.reset(), setDraft({ id: r.id, name: r.name, isSystem: r.isSystem, permissions: r.permissions }))}
                  >
                    ویرایش
                  </button>
                  {!r.isSystem && (
                    <button
                      className="link danger"
                      onClick={() => window.confirm(`سطح «${r.name}» حذف شود؟`) && remove.mutate(r)}
                    >
                      حذف
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
