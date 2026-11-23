import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef } from "react";
import { Link, useParams } from "react-router";
import { api, del } from "../../api/client";
import { orderStatusLabels, planStatusLabels, type ProductOverview } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { AuthImage } from "../../components/AuthImage";
import { ErrorText } from "../../components/Field";
import { formatDate, formatNumber } from "../../lib/format";

/** صفحه اختصاصی محصول: عکس، مشخصات، سری، دستمزد، سفارش‌ها، زیربرنامه‌ها، موجودی و کار در جریان. */
export function ProductPage() {
  const { id } = useParams();
  const { can } = useAuth();
  const qc = useQueryClient();
  const fileRef = useRef<HTMLInputElement>(null);
  const overview = useQuery({
    queryKey: ["products", id, "overview"],
    queryFn: () => api<ProductOverview>(`/catalog/products/${id}/overview`),
  });

  const refresh = () => qc.invalidateQueries({ queryKey: ["products"] });
  const upload = useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append("file", file);
      return api<void>(`/catalog/products/${id}/image`, { method: "PUT", body: form });
    },
    onSuccess: refresh,
  });
  const removeImage = useMutation({ mutationFn: () => del(`/catalog/products/${id}/image`), onSuccess: refresh });

  if (!overview.data) return <ErrorText error={overview.error} />;
  const o = overview.data;
  const p = o.product;
  const canEdit = can("ManageCatalog");

  return (
    <section>
      <Link to="/catalog/products">← محصولات</Link>
      <div className="row">
        <h2>
          {p.name} <bdi className="muted">{p.code}</bdi>
        </h2>
        {canEdit && (
          <Link className="button" to={`/catalog/products/${p.id}/edit`}>
            ویرایش مشخصات
          </Link>
        )}
      </div>

      <div className="product-head">
        <div className="product-photo card">
          {p.imageUploadedAtUtc ? (
            <AuthImage src={`/catalog/products/${p.id}/image?v=${encodeURIComponent(p.imageUploadedAtUtc)}`} alt={`عکس ${p.name}`} />
          ) : (
            <div className="img-placeholder">بدون عکس</div>
          )}
          {canEdit && (
            <div className="actions">
              <input
                ref={fileRef}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                hidden
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  if (f) upload.mutate(f);
                  e.target.value = "";
                }}
              />
              <button className="secondary" onClick={() => fileRef.current?.click()} disabled={upload.isPending}>
                {p.imageUploadedAtUtc ? "تغییر عکس" : "افزودن عکس"}
              </button>
              {p.imageUploadedAtUtc && (
                <button className="secondary" onClick={() => removeImage.mutate()} disabled={removeImage.isPending}>
                  حذف عکس
                </button>
              )}
            </div>
          )}
          <ErrorText error={upload.error ?? removeImage.error} />
          {canEdit && <small className="muted">JPG، PNG یا WebP، حداکثر ۲ مگابایت</small>}
        </div>

        <div className="card">
          <dl className="facts">
            <dt>گروه</dt>
            <dd>{p.group ?? "—"}</dd>
            <dt>زیره</dt>
            <dd>{p.soleName ?? "—"}</dd>
            <dt>مسیر تولید</dt>
            <dd>
              {p.routeName}
              {o.routeStages && <div className="muted">{o.routeStages}</div>}
            </dd>
            <dt>سری سایز</dt>
            <dd>
              {p.series.length === 0 ? (
                <span className="warn">تعریف نشده</span>
              ) : (
                <div className="series-chips">
                  {p.series.map((s) => (
                    <span key={s.size} className="chip">
                      <strong>{formatNumber(s.size)}</strong> × {formatNumber(s.quantity)}
                    </span>
                  ))}
                  <span className="muted">({formatNumber(p.pairsPerSeries)} جفت در سری)</span>
                </div>
              )}
            </dd>
            <dt>دستمزد هر جفت</dt>
            <dd>{formatNumber(o.laborPerPair)} ریال</dd>
            <dt>وضعیت</dt>
            <dd>{p.isActive ? "فعال" : "غیرفعال"}</dd>
          </dl>
          {p.description && <p className="pre-line">{p.description}</p>}
        </div>
      </div>

      <div className="kpis">
        <Kpi label="سفارش‌داده‌شده" value={o.orderedPairs} />
        <Kpi label="تولید و انبارشده" value={o.producedPairs} />
        <Kpi label="ارسال‌شده" value={o.shippedPairs} />
        <Kpi label="در جریان تولید" value={o.wip.reduce((s, w) => s + w.pairs, 0)} />
        <Kpi label="موجودی آزاد" value={o.freeStockPairs} />
      </div>

      {p.rates.length > 0 && (
        <div className="card">
          <h3>دستمزد کارمزدی هر مرحله (ریال برای هر جفت)</h3>
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
                {p.rates.map((r) => (
                  <tr key={r.stageName + r.effectiveFrom}>
                    <td>{r.stageName}</td>
                    <td>{formatNumber(r.ratePerPair)}</td>
                    <td>{formatDate(r.effectiveFrom)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <div className="card">
        <h3>سفارش‌ها</h3>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>سفارش</th>
                <th>مشتری</th>
                <th>رنگ</th>
                <th>جفت</th>
                <th>ارسال‌شده</th>
                <th>وضعیت</th>
                <th>تاریخ سفارش</th>
                <th>موعد تحویل</th>
              </tr>
            </thead>
            <tbody>
              {o.orders.map((r) => (
                <tr key={r.orderId + r.colorName}>
                  <td>
                    <Link to={`/orders/${r.orderId}`}>
                      <bdi>{r.orderNumber}</bdi>
                    </Link>
                  </td>
                  <td>{r.customerName ?? "—"}</td>
                  <td>{r.colorName}</td>
                  <td>{formatNumber(r.pairs)}</td>
                  <td>{formatNumber(r.shippedPairs)}</td>
                  <td>{orderStatusLabels[r.status]}</td>
                  <td>{formatDate(r.orderDate)}</td>
                  <td>{formatDate(r.dueDate)}</td>
                </tr>
              ))}
              {o.orders.length === 0 && (
                <tr>
                  <td colSpan={8} className="muted">
                    سفارشی برای این محصول ثبت نشده.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      <div className="grid-2">
        <div className="card">
          <h3>زیربرنامه‌ها</h3>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>شماره</th>
                  <th>وضعیت</th>
                  <th>جفت</th>
                  <th>پایان پیش‌بینی</th>
                  <th>پایان واقعی</th>
                </tr>
              </thead>
              <tbody>
                {o.plans.map((r) => (
                  <tr key={r.planId}>
                    <td>
                      <Link to={`/plans/${r.planId}`}>
                        <bdi>{r.planNumber}</bdi>
                      </Link>
                    </td>
                    <td>{planStatusLabels[r.status]}</td>
                    <td>{formatNumber(r.pairs)}</td>
                    <td>{formatDate(r.plannedEndDate)}</td>
                    <td>{formatDate(r.completedAtUtc)}</td>
                  </tr>
                ))}
                {o.plans.length === 0 && (
                  <tr>
                    <td colSpan={5} className="muted">
                      —
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="card">
          <h3>کار در جریان</h3>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>منتظر مرحله</th>
                  <th>دسته</th>
                  <th>جفت</th>
                </tr>
              </thead>
              <tbody>
                {o.wip.map((w) => (
                  <tr key={w.stageName}>
                    <td>{w.stageName}</td>
                    <td>{formatNumber(w.bundles)}</td>
                    <td>{formatNumber(w.pairs)}</td>
                  </tr>
                ))}
                {o.wip.length === 0 && (
                  <tr>
                    <td colSpan={3} className="muted">
                      —
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <h3>موجودی آزاد</h3>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>واحد</th>
                  <th>رنگ</th>
                  <th>سایز</th>
                  <th>جفت</th>
                </tr>
              </thead>
              <tbody>
                {o.stock.map((s) => (
                  <tr key={`${s.unitName}-${s.colorName}-${s.size}`}>
                    <td>{s.unitName}</td>
                    <td>{s.colorName}</td>
                    <td>{formatNumber(s.size)}</td>
                    <td>{formatNumber(s.quantity)}</td>
                  </tr>
                ))}
                {o.stock.length === 0 && (
                  <tr>
                    <td colSpan={4} className="muted">
                      —
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </section>
  );
}

export function Kpi({ label, value, suffix, tone }: { label: string; value: number | string; suffix?: string; tone?: "bad" | "good" }) {
  return (
    <div className={`kpi ${tone ?? ""}`}>
      <span className="muted">{label}</span>
      <strong>
        {typeof value === "number" ? formatNumber(value) : value}
        {suffix && <small> {suffix}</small>}
      </strong>
    </div>
  );
}
