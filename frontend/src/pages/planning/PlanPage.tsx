import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useParams } from "react-router";
import { api, post } from "../../api/client";
import { bundleStatusLabels, orderStatusLabels, planStatusLabels, type PlanDetail, type PlanOverview } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { ErrorText } from "../../components/Field";
import { DelayBadge, ProgressBar } from "../../components/Progress";
import { formatDate, formatDateTime, formatNumber, toLatinDigits } from "../../lib/format";
import { Kpi } from "../catalog/ProductPage";
import { ScheduleEditor } from "./ScheduleEditor";

export function PlanPage() {
  const { id } = useParams();
  const { can } = useAuth();
  const qc = useQueryClient();
  const plan = useQuery({ queryKey: ["plans", id], queryFn: () => api<PlanDetail>(`/plans/${id}`) });
  const overview = useQuery({ queryKey: ["plans", id, "overview"], queryFn: () => api<PlanOverview>(`/plans/${id}/overview`) });
  const [perBundle, setPerBundle] = useState("12");

  const onSuccess = (data: PlanDetail) => {
    qc.setQueryData(["plans", id], data);
    qc.invalidateQueries({ queryKey: ["plans", id, "overview"] });
    qc.invalidateQueries({ queryKey: ["orders"] });
  };
  const approve = useMutation({ mutationFn: () => post<PlanDetail>(`/plans/${id}/approve`), onSuccess });
  const release = useMutation({
    mutationFn: () => post<PlanDetail>(`/plans/${id}/release`, { maxPairsPerBundle: Number(toLatinDigits(perBundle)) }),
    onSuccess,
  });

  if (!plan.data) return <ErrorText error={plan.error} />;
  const p = plan.data;
  const o = overview.data;
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
        {o?.customerName && <> · مشتری: {o.customerName}</>}
        {o?.orderStatus && <> · سفارش: {orderStatusLabels[o.orderStatus]}</>}
      </p>

      <ErrorText error={overview.error} />
      {o && <PlanProgress o={o} canEdit={can("ManagePlans") && p.status !== "Completed" && p.status !== "Cancelled"} />}

      <details className="card">
        <summary>
          <strong>ردیف‌های زیربرنامه</strong> ({formatNumber(p.lines.length)} ردیف)
        </summary>
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
      </details>

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

/** نمای ۰ تا ۱۰۰: پیشرفت کلی، خط زمانی پیش‌بینی/واقعی، پیشرفت مراحل، ماتریس دسته × مرحله، سهم کارگران. */
function PlanProgress({ o, canEdit }: { o: PlanOverview; canEdit: boolean }) {
  const [editing, setEditing] = useState(false);
  return (
    <>
      <div className="card">
        <div className="row">
          <h3>پیشرفت کلی</h3>
          <DelayBadge days={o.delayDays} done={o.actualEndUtc !== null} />
        </div>
        <ProgressBar percent={o.percent} large />
        <div className="kpis">
          <Kpi label="کل جفت" value={o.totalPairs} />
          <Kpi label="وارد انبار" value={o.pairsInWarehouse} />
          <Kpi label="دسته‌ها" value={o.bundles.length} />
          <Kpi label="شروع پیش‌بینی / واقعی" value={`${formatDate(o.plannedStartDate)} / ${formatDate(o.actualStartUtc)}`} />
          <Kpi label="پایان پیش‌بینی / واقعی" value={`${formatDate(o.plannedEndDate)} / ${formatDate(o.actualEndUtc)}`} />
          {o.orderDueDate && <Kpi label="موعد تحویل سفارش" value={formatDate(o.orderDueDate)} />}
          <Kpi label="دستمزد ثبت‌شده" value={o.laborCost} suffix="ریال" />
        </div>
      </div>

      <div className="card">
        <div className="row">
          <h3>خط زمانی (پیش‌بینی‌شده در برابر واقعی)</h3>
          {canEdit && !editing && (
            <button className="secondary" onClick={() => setEditing(true)}>
              ویرایش زمان‌بندی
            </button>
          )}
        </div>
        {editing && <ScheduleEditor o={o} onDone={() => setEditing(false)} />}
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>قدم</th>
                <th>تاریخ پیش‌بینی</th>
                <th>تاریخ واقعی</th>
                <th>وضعیت</th>
              </tr>
            </thead>
            <tbody>
              {o.milestones.map((m) => (
                <tr key={m.key} className={m.actualAtUtc ? "" : "pending-row"}>
                  <td>
                    <span className={`dot ${m.actualAtUtc ? "done" : ""}`} aria-hidden /> {m.title}
                  </td>
                  <td>{formatDate(m.plannedDate)}</td>
                  <td>{m.actualAtUtc ? formatDateTime(m.actualAtUtc) : "—"}</td>
                  <td>
                    <DelayBadge days={m.delayDays} done={m.actualAtUtc !== null} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {o.stages.length > 0 && (
        <div className="card">
          <h3>پیشرفت مراحل</h3>
          <div className="stage-list">
            {o.stages.map((s) => (
              <div className="stage-row" key={s.stageId}>
                <span className="stage-name">{s.name}</span>
                <ProgressBar percent={s.percent} />
                <span className="muted">
                  {formatNumber(s.pairsDone)} از {formatNumber(s.pairsTotal)} جفت
                </span>
                <span className="muted">پیش‌بینی: {formatDate(s.plannedDate)}</span>
                <DelayBadge days={s.delayDays} done={s.finishedAtUtc !== null} />
              </div>
            ))}
          </div>
        </div>
      )}

      {o.bundles.length > 0 && (
        <div className="card">
          <h3>دسته × مرحله</h3>
          <p className="muted">هر خانه: تاریخ پایان مرحله و کارگر. ↺ یعنی دوباره‌کاری.</p>
          <div className="table-wrap">
            <table className="matrix">
              <thead>
                <tr>
                  <th>دسته</th>
                  <th>پیشرفت</th>
                  {o.stages.map((s) => (
                    <th key={s.stageId}>{s.name}</th>
                  ))}
                  <th>ورود به انبار</th>
                </tr>
              </thead>
              <tbody>
                {o.bundles.map((b) => (
                  <tr key={b.id}>
                    <td>
                      <bdi>{b.number}</bdi>
                      <div className="muted">
                        {b.modelCode} — {b.colorName} · {formatNumber(b.pairs)} جفت
                      </div>
                    </td>
                    <td>
                      <ProgressBar percent={b.percent} />
                      <div className="muted">{b.nextStage ? `بعدی: ${b.nextStage}` : bundleStatusLabels[b.status]}</div>
                    </td>
                    {b.cells.map((c) => (
                      <td key={c.stageId} className={!c.inRoute ? "cell-na" : c.doneAtUtc ? "cell-done" : "cell-todo"}>
                        {!c.inRoute ? (
                          "—"
                        ) : c.doneAtUtc ? (
                          <>
                            {formatDate(c.doneAtUtc)}
                            {c.workerName && <div className="muted">{c.workerName}</div>}
                          </>
                        ) : (
                          ""
                        )}
                        {c.reworkCount > 0 && <span className="rework"> ↺{formatNumber(c.reworkCount)}</span>}
                      </td>
                    ))}
                    <td className={b.inWarehouseAtUtc ? "cell-done" : "cell-todo"}>{b.inWarehouseAtUtc ? formatDate(b.inWarehouseAtUtc) : ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {o.workers.length > 0 && (
        <div className="card">
          <h3>کارگران این زیربرنامه</h3>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>کارگر</th>
                  <th>کد پرسنلی</th>
                  <th>جفت-مرحله</th>
                  <th>دستمزد (ریال)</th>
                </tr>
              </thead>
              <tbody>
                {o.workers.map((w) => (
                  <tr key={w.personnelCode + w.name}>
                    <td>{w.name}</td>
                    <td>
                      <bdi>{w.personnelCode}</bdi>
                    </td>
                    <td>{formatNumber(w.pairs)}</td>
                    <td>{formatNumber(w.wage)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </>
  );
}
