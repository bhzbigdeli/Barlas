import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { api, post, put } from "../../api/client";
import type { CodedItem } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { ErrorText } from "../../components/Field";

/** رنگ و زیره: فهرست‌های ساده کد + نام. */
export function BasicDataPage() {
  return (
    <section>
      <h2>رنگ و زیره</h2>
      <div className="grid-2">
        <CodedList title="رنگ‌ها" resource="colors" />
        <CodedList title="زیره‌ها" resource="soles" />
      </div>
    </section>
  );
}

function CodedList({ title, resource }: { title: string; resource: "colors" | "soles" }) {
  const { can } = useAuth();
  const editable = can("ManageCatalog");
  const qc = useQueryClient();
  const items = useQuery({ queryKey: [resource], queryFn: () => api<CodedItem[]>(`/catalog/${resource}`) });
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [editing, setEditing] = useState<CodedItem | null>(null);

  const invalidate = () => qc.invalidateQueries({ queryKey: [resource] });

  const create = useMutation({
    mutationFn: () => post<CodedItem>(`/catalog/${resource}`, { code, name }),
    onSuccess: () => {
      setCode("");
      setName("");
      return invalidate();
    },
  });

  const update = useMutation({
    mutationFn: (item: CodedItem) => put<CodedItem>(`/catalog/${resource}/${item.id}`, item),
    onSuccess: () => {
      setEditing(null);
      return invalidate();
    },
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    create.mutate();
  }

  return (
    <div className="card">
      <h3>{title}</h3>
      {editable && (
        <form className="inline-form" onSubmit={submit}>
          <input placeholder="کد" value={code} onChange={(e) => setCode(e.target.value)} dir="ltr" required />
          <input placeholder="نام" value={name} onChange={(e) => setName(e.target.value)} required />
          <button type="submit" disabled={create.isPending}>
            افزودن
          </button>
        </form>
      )}
      <ErrorText error={items.error ?? create.error ?? update.error} />
      <table>
        <tbody>
          {items.data?.map((item) =>
            editing?.id === item.id ? (
              <tr key={item.id}>
                <td dir="ltr">{item.code}</td>
                <td>
                  <input value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} />
                </td>
                <td className="row-actions">
                  <button className="link" onClick={() => update.mutate(editing)}>
                    ذخیره
                  </button>
                  <button className="link" onClick={() => setEditing(null)}>
                    انصراف
                  </button>
                </td>
              </tr>
            ) : (
              <tr key={item.id} className={item.isActive ? "" : "inactive"}>
                <td dir="ltr">{item.code}</td>
                <td>{item.name}</td>
                <td className="row-actions">
                  {editable && (
                    <>
                      <button className="link" onClick={() => setEditing(item)}>
                        ویرایش
                      </button>
                      <button className="link" onClick={() => update.mutate({ ...item, isActive: !item.isActive })}>
                        {item.isActive ? "غیرفعال" : "فعال"}
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ),
          )}
        </tbody>
      </table>
    </div>
  );
}
