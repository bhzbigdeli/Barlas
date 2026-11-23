import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useParams } from "react-router";
import { api, post } from "../../api/client";
import { bundleStatusLabels, planStatusLabels, type PlanDetail } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { ErrorText } from "../../components/Field";
import { formatNumber, toLatinDigits } from "../../lib/format";

export function PlanPage() {
  const { id } = useParams();
  const { can } = useAuth();
  const qc = useQueryClient();
  const plan = useQuery({ queryKey: ["plans", id], queryFn: () => api<PlanDetail>(`/plans/${id}`) });
  const [perBundle, setPerBundle] = useState("12");

  const onSuccess = (data: PlanDetail) => {
    qc.setQueryData(["plans", id], data);
    qc.invalidateQueries({ queryKey: ["orders"] });
  };
  const approve = useMutation({ mutationFn: () => post<PlanDetail>(`/plans/${id}/approve`), onSuccess });
  const release = useMutation({
    mutationFn: () => post<PlanDetail>(`/plans/${id}/release`, { maxPairsPerBundle: Number(toLatinDigits(perBundle)) }),
    onSuccess,
  });

  if (!plan.data) return <ErrorText error={plan.error} />;
  const p = plan.data;
  const orderPairs = p.lines.reduce((s, l) => s + l.orderQuantity, 0);
  const surplusPairs = p.lines.reduce((s, l) => s + l.surplusQuantity, 0);

  return (
    <section>
      {p.salesOrderId && <Link to={`/orders/${p.salesOrderId}`}>← سفارش {p.orderNumber}</Link>}
      <h2>
        زیربرنامه <span dir="ltr">{p.number}</span>
      </h2>
      <p className="muted">
        {p.unitName} · {planStatusLabels[p.status]} · {formatNumber(orderPairs)} جفت سفارش + {formatNumber(surplusPairs)}{" "}
        جفت مازاد ({formatNumber(Math.round(p.surplusRatio * 100))}٪)
      </p>

      <div className="card">
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>مدل</th>
                <th>رنگ</th>
                <th>سایز</th>
                <th>سهم سفارش</th>
                <th>مازاد</th>
              </tr>
            </thead>
            <tbody>
              {p.lines.map((l) => (
                <tr key={`${l.modelCode}-${l.colorName}-${l.size}`}>
                  <td>
                    {l.modelName} ({l.modelCode})
                  </td>
                  <td>{l.colorName}</td>
                  <td>{formatNumber(l.size)}</td>
                  <td>{formatNumber(l.orderQuantity)}</td>
                  <td>{formatNumber(l.surplusQuantity)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {p.status === "AwaitingApproval" && (
        <div className="card">
          <h3>تأیید مازاد</h3>
          <p>
            مازاد این زیربرنامه {formatNumber(Math.round(p.surplusRatio * 100))}٪ است؛ بیش از آستانه{" "}
            {formatNumber(Math.round(p.surplusThreshold * 100))}٪ واحد.
          </p>
          <ErrorText error={approve.error} />
          {can("ApproveSurplus") ? (
            <div className="actions">
              <button onClick={() => approve.mutate()} disabled={approve.isPending}>
                تأیید مازاد
              </button>
            </div>
          ) : (
            <p className="muted">منتظر تأیید مدیر کارخانه.</p>
          )}
        </div>
      )}

      {p.status === "Approved" && can("ManagePlans") && (
        <form
          className="card"
          onSubmit={(e) => {
            e.preventDefault();
            release.mutate();
          }}
        >
          <h3>صدور دسته‌ها</h3>
          <label className="narrow">
            حداکثر جفت در هر دسته
            <input value={perBundle} onChange={(e) => setPerBundle(e.target.value)} inputMode="numeric" />
          </label>
          <ErrorText error={release.error} />
          <div className="actions">
            <button type="submit" disabled={release.isPending}>
              صدور دسته‌ها و کاردکس
            </button>
          </div>
        </form>
      )}

      {p.bundles.length > 0 && (
        <div className="card">
          <div className="row">
            <h3>دسته‌ها</h3>
            <Link className="button" to={`/plans/${p.id}/kardex`}>
              چاپ کاردکس‌ها
            </Link>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>دسته</th>
                  <th>مدل / رنگ</th>
                  <th>سایزها</th>
                  <th>جفت</th>
                  <th>آخرین مرحله</th>
                  <th>وضعیت</th>
                </tr>
              </thead>
              <tbody>
                {p.bundles.map((b) => (
                  <tr key={b.id}>
                    <td dir="ltr">{b.number}</td>
                    <td>
                      {b.modelCode} — {b.colorName}
                    </td>
                    <td>
                      <div className="series-chips">
                        {b.sizes.map((s) => (
                          <span className="chip" key={s.size}>
                            <strong>{formatNumber(s.size)}</strong> × {formatNumber(s.quantity)}
                          </span>
                        ))}
                      </div>
                    </td>
                    <td>{formatNumber(b.pairs)}</td>
                    <td>{b.lastStage ?? "—"}</td>
                    <td>{bundleStatusLabels[b.status]}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  );
}
