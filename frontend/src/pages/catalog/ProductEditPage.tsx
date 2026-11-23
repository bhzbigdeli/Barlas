import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { api, post, put } from "../../api/client";
import type { CodedItem, Product, ProductionRoute, SeriesSize } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { ErrorText, Field } from "../../components/Field";
import { formatDate, formatNumber } from "../../lib/format";
import { SeriesEditor } from "./SeriesEditor";

interface Draft {
  code: string;
  name: string;
  group: string;
  routeId: string;
  soleId: string;
  isActive: boolean;
  series: SeriesSize[];
}

/** تعریف یا ویرایش محصول با سری سایز خودش. بدون مجوز «تعریف محصول» فقط خواندنی است. */
export function ProductEditPage() {
  const { id } = useParams();
  const isNew = id === undefined;
  const { can } = useAuth();
  const readOnly = !can("ManageCatalog");
  const navigate = useNavigate();
  const qc = useQueryClient();

  const product = useQuery({ queryKey: ["products", id], queryFn: () => api<Product>(`/catalog/products/${id}`), enabled: !isNew });
  const routes = useQuery({ queryKey: ["routes"], queryFn: () => api<ProductionRoute[]>("/catalog/routes") });
  const soles = useQuery({ queryKey: ["soles"], queryFn: () => api<CodedItem[]>("/catalog/soles") });

  const [draft, setDraft] = useState<Draft | null>(null);

  useEffect(() => {
    if (isNew && routes.data && !draft)
      setDraft({ code: "", name: "", group: "", routeId: routes.data[0]?.id ?? "", soleId: "", isActive: true, series: [] });
    if (!isNew && product.data && !draft) {
      const p = product.data;
      setDraft({ code: p.code, name: p.name, group: p.group ?? "", routeId: p.routeId, soleId: p.soleId ?? "", isActive: p.isActive, series: p.series });
    }
  }, [isNew, routes.data, product.data, draft]);

  const save = useMutation({
    mutationFn: (d: Draft) => {
      const body = { ...d, soleId: d.soleId || null, group: d.group || null, series: d.series.filter((s) => s.quantity > 0) };
      return isNew ? post<Product>("/catalog/products", body) : put<Product>(`/catalog/products/${id}`, body);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ["products"] });
      navigate("/catalog/products");
    },
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    if (draft) save.mutate(draft);
  }

  if (product.isError) return <ErrorText error={product.error} />;
  if (!draft) return <p className="muted">در حال بارگذاری…</p>;

  return (
    <section>
      <Link to="/catalog/products">← محصولات</Link>
      <h2>{isNew ? "محصول جدید" : `محصول ${draft.code}`}</h2>

      <form className="card" onSubmit={submit}>
        <div className="grid-2">
          <Field label="کد محصول" hint={isNew ? "همان کدی که در سفارش‌های تهران می‌آید؛ بعداً قابل تغییر نیست." : undefined}>
            <input
              value={draft.code}
              onChange={(e) => setDraft({ ...draft, code: e.target.value })}
              dir="ltr"
              disabled={!isNew || readOnly}
              required
            />
          </Field>
          <Field label="نام محصول">
            <input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} disabled={readOnly} required />
          </Field>
          <Field label="گروه" hint="مثل زنانه صنعتی، صندل اکو (طبق آنالیز بهای تمام‌شده)">
            <input value={draft.group} onChange={(e) => setDraft({ ...draft, group: e.target.value })} disabled={readOnly} />
          </Field>
          <Field label="مسیر مراحل تولید">
            <select value={draft.routeId} onChange={(e) => setDraft({ ...draft, routeId: e.target.value })} disabled={readOnly} required>
              {routes.data?.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name} ({r.stages.join(" ← ")})
                </option>
              ))}
            </select>
          </Field>
          <Field label="زیره">
            <select value={draft.soleId} onChange={(e) => setDraft({ ...draft, soleId: e.target.value })} disabled={readOnly}>
              <option value="">—</option>
              {soles.data
                ?.filter((s) => s.isActive || s.id === draft.soleId)
                .map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name} ({s.code})
                  </option>
                ))}
            </select>
          </Field>
        </div>

        <fieldset>
          <legend>سری سایز</legend>
          {draft.series.length === 0 && (
            <p className="warn">سری سایز این محصول هنوز تعریف نشده؛ تا تعریف نشود سفارش «تعداد سری» برایش باز نمی‌شود.</p>
          )}
          <SeriesEditor value={draft.series} onChange={(series) => setDraft({ ...draft, series })} disabled={readOnly} />
        </fieldset>

        <label className="check">
          <input
            type="checkbox"
            checked={draft.isActive}
            onChange={(e) => setDraft({ ...draft, isActive: e.target.checked })}
            disabled={readOnly}
          />
          <span>فعال (در سفارش‌ها و برنامه‌ریزی قابل استفاده است)</span>
        </label>

        {product.data && product.data.rates.length > 0 && (
          <fieldset>
            <legend>دستمزد کارمزدی هر مرحله (ریال برای هر جفت)</legend>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>مرحله</th>
                    <th>دستمزد</th>
                    <th>از تاریخ</th>
                  </tr>
                </thead>
                <tbody>
                  {product.data.rates.map((r) => (
                    <tr key={r.stageName + r.effectiveFrom}>
                      <td>{r.stageName}</td>
                      <td>{formatNumber(r.ratePerPair)}</td>
                      <td>{formatDate(r.effectiveFrom)}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <th>جمع</th>
                    <th>{formatNumber(product.data.rates.reduce((s, r) => s + r.ratePerPair, 0))}</th>
                    <th />
                  </tr>
                </tfoot>
              </table>
            </div>
          </fieldset>
        )}

        <ErrorText error={save.error} />
        {!readOnly && (
          <div className="actions">
            <button type="submit" disabled={save.isPending}>
              ذخیره
            </button>
            <Link className="button secondary" to="/catalog/products">
              انصراف
            </Link>
          </div>
        )}
      </form>
    </section>
  );
}
