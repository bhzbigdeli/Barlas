import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useSearchParams } from "react-router";
import { api } from "../api/client";
import {
  orderStatusLabels,
  planStatusLabels,
  type DailyProduction,
  type DelayReport,
  type OrderTimeline,
  type ReportSummary,
  type WipReport,
  type WorkerOutput,
} from "../api/types";
import { ErrorText } from "../components/Field";
import { JalaliDateInput } from "../components/JalaliDateInput";
import { DelayBadge } from "../components/Progress";
import { formatDate, formatNumber } from "../lib/format";
import { todayIso } from "../lib/jalali";
import { Kpi } from "./catalog/ProductPage";

const tabs = [
  { key: "summary", label: "خلاصه" },
  { key: "delays", label: "دیرکردها" },
  { key: "orders", label: "زمان‌بندی سفارش‌ها" },
  { key: "daily", label: "تولید روزانه" },
  { key: "workers", label: "کارکرد کارگران" },
  { key: "wip", label: "کار در جریان" },
] as const;
type Tab = (typeof tabs)[number]["key"];

const addDays = (iso: string, days: number) => {
  const d = new Date(`${iso}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

/** گزارش‌های مدیریتی با بازه تاریخ شمسی. */
export function ReportsPage() {
  const [params, setParams] = useSearchParams();
  const tab = (params.get("tab") as Tab) || "summary";
  const today = todayIso();
  const [from, setFrom] = useState(addDays(today, -29));
  const [to, setTo] = useState(today);
  const range = `from=${from}&to=${to}`;
  const usesRange = tab !== "delays" && tab !== "wip";

  return (
    <section>
      <div className="row">
        <h2>گزارش‌ها</h2>
        <button className="secondary no-print" onClick={() => window.print()}>
          چاپ
        </button>
      </div>

      <div className="tabs no-print" role="tablist">
        {tabs.map((t) => (
          <button
            key={t.key}
            role="tab"
            aria-selected={tab === t.key}
            className={tab === t.key ? "tab active" : "tab"}
            onClick={() => setParams({ tab: t.key }, { replace: true })}
          >
            {t.label}
          </button>
        ))}
      </div>

      {usesRange && (
        <div className="range-bar card no-print">
          <label>
            از
            <JalaliDateInput label="از تاریخ" value={from} onChange={(v) => v && setFrom(v)} />
          </label>
          <label>
            تا
            <JalaliDateInput label="تا تاریخ" value={to} onChange={(v) => v && setTo(v)} />
          </label>
          <div className="presets">
            {[
              ["امروز", 0],
              ["۷ روز", 6],
              ["۳۰ روز", 29],
              ["۹۰ روز", 89],
            ].map(([label, days]) => (
              <button
                key={label}
                className="secondary"
                onClick={() => {
                  setTo(today);
                  setFrom(addDays(today, -(days as number)));
                }}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
      )}
      <p className="print-only muted">
        {tabs.find((t) => t.key === tab)?.label}
        {usesRange && ` · ${formatDate(from)} تا ${formatDate(to)}`}
      </p>

      {tab === "summary" && <Summary range={range} />}
      {tab === "delays" && <Delays />}
      {tab === "orders" && <Orders range={range} />}
      {tab === "daily" && <Daily range={range} />}
      {tab === "workers" && <Workers range={range} />}
      {tab === "wip" && <Wip />}
    </section>
  );
}

function Summary({ range }: { range: string }) {
  const q = useQuery({ queryKey: ["reports", "summary", range], queryFn: () => api<ReportSummary>(`/reports/summary?${range}`) });
  if (!q.data) return <ErrorText error={q.error} />;
  const s = q.data;
  return (
    <>
      <div className="kpis">
        <Kpi label="سفارش دریافتی" value={s.ordersReceived} suffix={`(${formatNumber(s.pairsOrdered)} جفت)`} />
        <Kpi label="جفت تولید و انبارشده" value={s.pairsProduced} />
        <Kpi label="جفت ارسال‌شده" value={s.pairsShipped} />
        <Kpi label="سفارش ارسال کامل" value={s.ordersShipped} />
        <Kpi
          label="تحویل به‌موقع"
          value={s.onTimePercent === null ? "—" : `${formatNumber(s.onTimePercent)}٪`}
          tone={s.onTimePercent !== null && s.onTimePercent < 80 ? "bad" : "good"}
        />
        <Kpi label="میانگین زمان ورود تا ارسال" value={s.avgLeadDays === null ? "—" : formatNumber(s.avgLeadDays)} suffix="روز" />
      </div>
      <h3>وضعیت فعلی</h3>
      <div className="kpis">
        <Kpi label="سفارش باز" value={s.openOrders} />
        <Kpi label="سفارش باز گذشته از موعد" value={s.lateOpenOrders} tone={s.lateOpenOrders > 0 ? "bad" : "good"} />
        <Kpi label="جفت در جریان تولید" value={s.pairsInProgress} />
        <Kpi label="دستمزد کارمزدی بازه" value={s.laborCost} suffix="ریال" />
      </div>
    </>
  );
}

function Delays() {
  const q = useQuery({ queryKey: ["reports", "delays"], queryFn: () => api<DelayReport>("/reports/delays") });
  if (!q.data) return <ErrorText error={q.error} />;
  const d = q.data;
  return (
    <>
      <div className="card">
        <h3>سفارش‌های دیرشده ({formatNumber(d.orders.length)})</h3>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>سفارش</th>
                <th>مشتری</th>
                <th>وضعیت</th>
                <th>جفت (ارسال‌شده)</th>
                <th>موعد تحویل</th>
                <th>ارسال کامل</th>
                <th>دیرکرد</th>
              </tr>
            </thead>
            <tbody>
              {d.orders.map((o) => (
                <tr key={o.id}>
                  <td>
                    <Link to={`/orders/${o.id}`}>
                      <bdi>{o.number}</bdi>
                    </Link>
                  </td>
                  <td>{o.customerName ?? "—"}</td>
                  <td>{orderStatusLabels[o.status]}</td>
                  <td>
                    {formatNumber(o.pairs)} ({formatNumber(o.shippedPairs)})
                  </td>
                  <td>{formatDate(o.dueDate)}</td>
                  <td>{formatDate(o.shippedAtUtc)}</td>
                  <td>
                    <DelayBadge days={o.delayDays} done={o.shippedAtUtc !== null} />
                  </td>
                </tr>
              ))}
              <Empty show={d.orders.length === 0} cols={7} text="سفارش دیرشده‌ای نیست." />
            </tbody>
          </table>
        </div>
      </div>

      <div className="card">
        <h3>سفارش‌های در خطر (موعد تا ۷ روز آینده، هنوز آماده نیست)</h3>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>سفارش</th>
                <th>مشتری</th>
                <th>موعد</th>
                <th>روز باقیمانده</th>
                <th>آماده / کل</th>
              </tr>
            </thead>
            <tbody>
              {d.atRisk.map((o) => (
                <tr key={o.id}>
                  <td>
                    <Link to={`/orders/${o.id}`}>
                      <bdi>{o.number}</bdi>
                    </Link>
                  </td>
                  <td>{o.customerName ?? "—"}</td>
                  <td>{formatDate(o.dueDate)}</td>
                  <td>{formatNumber(o.daysLeft)}</td>
                  <td>
                    {formatNumber(o.readyPairs)} / {formatNumber(o.pairs)}
                  </td>
                </tr>
              ))}
              <Empty show={d.atRisk.length === 0} cols={5} text="—" />
            </tbody>
          </table>
        </div>
      </div>

      <div className="card">
        <h3>زیربرنامه‌های دیرشده ({formatNumber(d.plans.length)})</h3>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>زیربرنامه</th>
                <th>سفارش</th>
                <th>وضعیت</th>
                <th>پیشرفت</th>
                <th>پایان پیش‌بینی</th>
                <th>پایان واقعی</th>
                <th>دیرکرد</th>
              </tr>
            </thead>
            <tbody>
              {d.plans.map((p) => (
                <tr key={p.id}>
                  <td>
                    <Link to={`/plans/${p.id}`}>
                      <bdi>{p.number}</bdi>
                    </Link>
                  </td>
                  <td>
                    <bdi>{p.orderNumber ?? "—"}</bdi>
                  </td>
                  <td>{planStatusLabels[p.status]}</td>
                  <td>{formatNumber(p.percent)}٪</td>
                  <td>{formatDate(p.plannedEndDate)}</td>
                  <td>{formatDate(p.completedAtUtc)}</td>
                  <td>
                    <DelayBadge days={p.delayDays} done={p.completedAtUtc !== null} />
                  </td>
                </tr>
              ))}
              <Empty show={d.plans.length === 0} cols={7} text="—" />
            </tbody>
          </table>
        </div>
      </div>

      <div className="card">
        <h3>مراحل عقب‌افتاده ({formatNumber(d.stages.length)})</h3>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>زیربرنامه</th>
                <th>مرحله</th>
                <th>تاریخ پیش‌بینی</th>
                <th>انجام‌شده</th>
                <th>تأخیر</th>
              </tr>
            </thead>
            <tbody>
              {d.stages.map((s) => (
                <tr key={s.planId + s.stageName}>
                  <td>
                    <Link to={`/plans/${s.planId}`}>
                      <bdi>{s.planNumber}</bdi>
                    </Link>
                  </td>
                  <td>{s.stageName}</td>
                  <td>{formatDate(s.plannedDate)}</td>
                  <td>
                    {formatNumber(s.pairsDone)} از {formatNumber(s.pairsTotal)} جفت
                  </td>
                  <td>
                    <DelayBadge days={s.delayDays} done={false} />
                  </td>
                </tr>
              ))}
              <Empty show={d.stages.length === 0} cols={5} text="—" />
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}

function Orders({ range }: { range: string }) {
  const q = useQuery({ queryKey: ["reports", "orders", range], queryFn: () => api<OrderTimeline[]>(`/reports/orders?${range}`) });
  if (!q.data) return <ErrorText error={q.error} />;
  return (
    <div className="card">
      <p className="muted">سفارش‌های ثبت‌شده در بازه و همه سفارش‌های باز. هر ستون تاریخ واقعی آن قدم است.</p>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>سفارش</th>
              <th>مشتری</th>
              <th>وضعیت</th>
              <th>جفت</th>
              <th>ورود</th>
              <th>برنامه‌ریزی</th>
              <th>شروع تولید</th>
              <th>آماده ارسال</th>
              <th>اولین ارسال</th>
              <th>ارسال کامل</th>
              <th>موعد</th>
              <th>دیرکرد</th>
              <th>مدت (روز)</th>
            </tr>
          </thead>
          <tbody>
            {q.data.map((o) => (
              <tr key={o.id}>
                <td>
                  <Link to={`/orders/${o.id}`}>
                    <bdi>{o.number}</bdi>
                  </Link>
                </td>
                <td>{o.customerName ?? "—"}</td>
                <td>{orderStatusLabels[o.status]}</td>
                <td>
                  {formatNumber(o.pairs)}
                  {o.shippedPairs > 0 && o.shippedPairs < o.pairs && <div className="muted">ارسال: {formatNumber(o.shippedPairs)}</div>}
                </td>
                <td>{formatDate(o.receivedAtUtc)}</td>
                <td>{formatDate(o.plannedAtUtc)}</td>
                <td>{formatDate(o.inProductionAtUtc)}</td>
                <td>{formatDate(o.readyAtUtc)}</td>
                <td>{formatDate(o.firstShipmentAtUtc)}</td>
                <td>{formatDate(o.shippedAtUtc)}</td>
                <td>{formatDate(o.dueDate)}</td>
                <td>
                  <DelayBadge days={o.delayDays} done={o.shippedAtUtc !== null} />
                </td>
                <td>{o.leadDays === null ? "—" : formatNumber(o.leadDays)}</td>
              </tr>
            ))}
            <Empty show={q.data.length === 0} cols={13} text="سفارشی در این بازه نیست." />
          </tbody>
        </table>
      </div>
    </div>
  );
}

function Daily({ range }: { range: string }) {
  const q = useQuery({ queryKey: ["reports", "daily", range], queryFn: () => api<DailyProduction>(`/reports/daily?${range}`) });
  if (!q.data) return <ErrorText error={q.error} />;
  const d = q.data;
  const max = Math.max(1, ...d.days.map((x) => x.total));
  return (
    <div className="card">
      <p className="muted">جفت‌های تمام‌شده در هر مرحله (از اسکن کاردکس).</p>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>تاریخ</th>
              {d.stages.map((s) => (
                <th key={s}>{s}</th>
              ))}
              <th>جمع</th>
              <th aria-label="نمودار" />
            </tr>
          </thead>
          <tbody>
            {d.days.map((row) => (
              <tr key={row.date}>
                <td>{formatDate(row.date)}</td>
                {d.stages.map((s) => (
                  <td key={s}>{row.pairsByStage[s] ? formatNumber(row.pairsByStage[s]) : ""}</td>
                ))}
                <td>
                  <strong>{formatNumber(row.total)}</strong>
                </td>
                <td className="bar-cell">
                  <div className="hbar" style={{ width: `${(100 * row.total) / max}%` }} />
                </td>
              </tr>
            ))}
            <Empty show={d.days.length === 0} cols={d.stages.length + 3} text="در این بازه اسکنی ثبت نشده." />
          </tbody>
          {d.days.length > 0 && (
            <tfoot>
              <tr>
                <th>جمع</th>
                {d.stages.map((s) => (
                  <th key={s}>{formatNumber(d.totals[s] ?? 0)}</th>
                ))}
                <th>{formatNumber(Object.values(d.totals).reduce((a, b) => a + b, 0))}</th>
                <th />
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}

function Workers({ range }: { range: string }) {
  const q = useQuery({ queryKey: ["reports", "workers", range], queryFn: () => api<WorkerOutput[]>(`/reports/workers?${range}`) });
  if (!q.data) return <ErrorText error={q.error} />;
  const rows = q.data;
  return (
    <div className="card">
      <p className="muted">دستمزد = جفت × نرخ کارمزدی آن مرحله برای آن مدل در تاریخ اسکن. دوباره‌کاری دستمزد ندارد.</p>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>کارگر</th>
              <th>کد پرسنلی</th>
              <th>واحد</th>
              <th>روز کاری</th>
              <th>جفت-مرحله</th>
              <th>میانگین روزانه</th>
              <th>دوباره‌کاری</th>
              <th>مراحل</th>
              <th>دستمزد (ریال)</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((w) => (
              <tr key={w.workerId}>
                <td>{w.name}</td>
                <td>
                  <bdi>{w.personnelCode}</bdi>
                </td>
                <td>{w.unitName}</td>
                <td>{formatNumber(w.activeDays)}</td>
                <td>{formatNumber(w.pairs)}</td>
                <td>{w.activeDays ? formatNumber(Math.round(w.pairs / w.activeDays)) : "—"}</td>
                <td className={w.reworks > 0 ? "error" : ""}>{formatNumber(w.reworks)}</td>
                <td className="muted">
                  {Object.entries(w.pairsByStage)
                    .map(([s, n]) => `${s}: ${formatNumber(n)}`)
                    .join(" · ")}
                </td>
                <td>{formatNumber(w.wage)}</td>
              </tr>
            ))}
            <Empty show={rows.length === 0} cols={9} text="در این بازه کارکردی ثبت نشده." />
          </tbody>
          {rows.length > 0 && (
            <tfoot>
              <tr>
                <th colSpan={4}>جمع</th>
                <th>{formatNumber(rows.reduce((s, w) => s + w.pairs, 0))}</th>
                <th />
                <th>{formatNumber(rows.reduce((s, w) => s + w.reworks, 0))}</th>
                <th />
                <th>{formatNumber(rows.reduce((s, w) => s + w.wage, 0))}</th>
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}

function Wip() {
  const q = useQuery({ queryKey: ["reports", "wip"], queryFn: () => api<WipReport>("/reports/wip") });
  if (!q.data) return <ErrorText error={q.error} />;
  const w = q.data;
  return (
    <>
      <div className="card">
        <h3>دسته‌ها پشت هر مرحله</h3>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>منتظر مرحله</th>
                <th>دسته</th>
                <th>جفت</th>
                <th>میانگین انتظار (روز)</th>
                <th>بیشترین انتظار (روز)</th>
              </tr>
            </thead>
            <tbody>
              {w.stages.map((s) => (
                <tr key={s.stageName}>
                  <td>{s.stageName}</td>
                  <td>{formatNumber(s.bundles)}</td>
                  <td>{formatNumber(s.pairs)}</td>
                  <td>{formatNumber(s.avgWaitDays)}</td>
                  <td className={s.maxWaitDays > 3 ? "error" : ""}>{formatNumber(s.maxWaitDays)}</td>
                </tr>
              ))}
              <Empty show={w.stages.length === 0} cols={5} text="دسته‌ای در جریان نیست." />
            </tbody>
          </table>
        </div>
      </div>
      <div className="card">
        <h3>قدیمی‌ترین دسته‌های معطل</h3>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>دسته</th>
                <th>زیربرنامه</th>
                <th>مدل / رنگ</th>
                <th>جفت</th>
                <th>منتظر</th>
                <th>از تاریخ</th>
                <th>روز</th>
              </tr>
            </thead>
            <tbody>
              {w.oldest.map((b) => (
                <tr key={b.bundleNumber}>
                  <td>
                    <bdi>{b.bundleNumber}</bdi>
                  </td>
                  <td>
                    <Link to={`/plans/${b.planId}`}>
                      <bdi>{b.planNumber}</bdi>
                    </Link>
                  </td>
                  <td>
                    {b.modelCode} — {b.colorName}
                  </td>
                  <td>{formatNumber(b.pairs)}</td>
                  <td>{b.waiting}</td>
                  <td>{formatDate(b.sinceUtc)}</td>
                  <td>{formatNumber(b.waitDays)}</td>
                </tr>
              ))}
              <Empty show={w.oldest.length === 0} cols={7} text="—" />
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}

function Empty({ show, cols, text }: { show: boolean; cols: number; text: string }) {
  if (!show) return null;
  return (
    <tr>
      <td colSpan={cols} className="muted">
        {text}
      </td>
    </tr>
  );
}
