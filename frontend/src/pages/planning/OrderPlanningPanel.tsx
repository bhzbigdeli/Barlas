import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router";
import { api, post } from "../../api/client";
import { planStatusLabels, type OrderPlanning, type PlanDetail, type SalesOrderStatus } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { ErrorText } from "../../components/Field";
import { formatNumber, toLatinDigits } from "../../lib/format";

interface Qty {
  order: string;
  surplus: string;
}

const key = (r: { shoeModelId: string; colorId: string; size: number }) => `${r.shoeModelId}|${r.colorId}|${r.size}`;
const num = (s: string) => Number(toLatinDigits(s)) || 0;

/** بخش برنامه‌ریزی صفحه سفارش: باقیمانده → زیربرنامه (با مازاد اختیاری) و ارسال به تهران. */
export function OrderPlanningPanel({ orderId, status }: { orderId: string; status: SalesOrderStatus }) {
  const { can } = useAuth();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const planning = useQuery({
    queryKey: ["orders", orderId, "planning"],
    queryFn: () => api<OrderPlanning>(`/orders/${orderId}/planning`),
  });
  const [qty, setQty] = useState<Record<string, Qty>>({});

  useEffect(() => {
    if (!planning.data) return;
    setQty(Object.fromEntries(planning.data.rows.map((r) => [key(r), { order: String(r.unplanned), surplus: "0" }])));
  }, [planning.data]);

  const create = useMutation({
    mutationFn: () =>
      post<PlanDetail>("/plans", {
        salesOrderId: orderId,
        lines: planning.data!.rows.map((r) => ({
          shoeModelId: r.shoeModelId,
          colorId: r.colorId,
          size: r.size,
          orderQuantity: num(qty[key(r)]?.order ?? "0"),
          surplusQuantity: num(qty[key(r)]?.surplus ?? "0"),
        })),
      }),
    onSuccess: (plan) => {
      qc.invalidateQueries({ queryKey: ["orders"] });
      navigate(`/plans/${plan.id}`);
    },
  });

  const ship = useMutation({
    mutationFn: () => post<void>(`/orders/${orderId}/ship`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["orders"] }),
  });

  if (!planning.data) return <ErrorText error={planning.error} />;
  const { rows, plans, surplusThreshold } = planning.data;
  const totalUnplanned = rows.reduce((s, r) => s + r.unplanned, 0);
  const totalOrder = rows.reduce((s, r) => s + num(qty[key(r)]?.order ?? "0"), 0);
  const totalSurplus = rows.reduce((s, r) => s + num(qty[key(r)]?.surplus ?? "0"), 0);
  const ratio = totalOrder > 0 ? totalSurplus / totalOrder : totalSurplus > 0 ? 1 : 0;
  const canPlan = can("ManagePlans") && totalUnplanned > 0 && status !== "Shipped" && status !== "Cancelled";

  return (
    <>
      {plans.length > 0 && (
        <div className="card">
          <h3>زیربرنامه‌ها</h3>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>شماره</th>
                  <th>وضعیت</th>
                  <th>سهم سفارش</th>
                  <th>مازاد</th>
                  <th>دسته</th>
                </tr>
              </thead>
              <tbody>
                {plans.map((p) => (
                  <tr key={p.id}>
                    <td dir="ltr">
                      <Link to={`/plans/${p.id}`}>{p.number}</Link>
                    </td>
                    <td>{planStatusLabels[p.status]}</td>
                    <td>{formatNumber(p.orderPairs)}</td>
                    <td>{formatNumber(p.surplusPairs)}</td>
                    <td>{formatNumber(p.bundleCount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {canPlan && (
        <form
          className="card"
          onSubmit={(e) => {
            e.preventDefault();
            create.mutate();
          }}
        >
          <h3>زیربرنامه جدید</h3>
          <p className="muted">
            باقیمانده برنامه‌ریزی‌نشده: {formatNumber(totalUnplanned)} جفت. مازاد بیش از{" "}
            {formatNumber(Math.round(surplusThreshold * 100))}٪ تأیید مدیر کارخانه لازم دارد.
          </p>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>مدل / رنگ</th>
                  <th>سایز</th>
                  <th>باقیمانده</th>
                  <th>سهم سفارش</th>
                  <th>مازاد</th>
                </tr>
              </thead>
              <tbody>
                {rows
                  .filter((r) => r.unplanned > 0)
                  .map((r) => {
                    const k = key(r);
                    const v = qty[k] ?? { order: "0", surplus: "0" };
                    return (
                      <tr key={k}>
                        <td>
                          {r.modelCode} — {r.colorName}
                        </td>
                        <td>{formatNumber(r.size)}</td>
                        <td>{formatNumber(r.unplanned)}</td>
                        <td>
                          <input
                            className="qty"
                            inputMode="numeric"
                            aria-label={`سهم سفارش سایز ${r.size}`}
                            value={v.order}
                            onChange={(e) => setQty({ ...qty, [k]: { ...v, order: e.target.value } })}
                          />
                        </td>
                        <td>
                          <input
                            className="qty"
                            inputMode="numeric"
                            aria-label={`مازاد سایز ${r.size}`}
                            value={v.surplus}
                            onChange={(e) => setQty({ ...qty, [k]: { ...v, surplus: e.target.value } })}
                          />
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>
          <p>
            جمع: {formatNumber(totalOrder)} جفت سفارش + {formatNumber(totalSurplus)} جفت مازاد (
            <span className={ratio > surplusThreshold ? "error" : ""}>{formatNumber(Math.round(ratio * 100))}٪</span>)
          </p>
          <ErrorText error={create.error} />
          <div className="actions">
            <button type="submit" disabled={create.isPending || totalOrder + totalSurplus === 0}>
              ثبت زیربرنامه
            </button>
          </div>
        </form>
      )}

      {status === "ReadyToShip" && can("ManageWarehouse") && (
        <div className="card">
          <h3>ارسال به تهران</h3>
          <p className="muted">همه سایزهای این سفارش کامل شده است.</p>
          <ErrorText error={ship.error} />
          <div className="actions">
            <button onClick={() => ship.mutate()} disabled={ship.isPending}>
              ثبت ارسال سفارش
            </button>
          </div>
        </div>
      )}
      {status === "Shipped" && <p className="ok">این سفارش به تهران ارسال شده است.</p>}
    </>
  );
}
