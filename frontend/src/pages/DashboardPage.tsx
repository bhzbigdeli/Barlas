import { useQuery } from "@tanstack/react-query";
import { api } from "../api/client";
import { orderStatusLabels, type OrderSummary, type SalesOrderStatus, type Unit } from "../api/types";
import { formatNumber } from "../lib/format";

/** داشبورد اولیه: شمار سفارش‌ها و جفت‌ها به تفکیک وضعیت. گزارش‌های روزانه در ادامه فاز ۱ اضافه می‌شوند. */
export function DashboardPage() {
  const units = useQuery({ queryKey: ["units"], queryFn: () => api<Unit[]>("/units") });
  const orders = useQuery({ queryKey: ["orders"], queryFn: () => api<OrderSummary[]>("/orders") });

  const byStatus = new Map<SalesOrderStatus, { count: number; pairs: number }>();
  for (const o of orders.data ?? []) {
    const s = byStatus.get(o.status) ?? { count: 0, pairs: 0 };
    s.count += 1;
    s.pairs += o.totalPairs;
    byStatus.set(o.status, s);
  }

  return (
    <section>
      <h2>داشبورد</h2>
      <p className="muted">واحدها: {units.data?.map((u) => u.name).join("، ") ?? "…"}</p>
      {orders.isError && <p className="error">{(orders.error as Error).message}</p>}
      <div className="tiles">
        {(Object.keys(orderStatusLabels) as SalesOrderStatus[]).map((status) => {
          const s = byStatus.get(status);
          return (
            <div className="tile" key={status}>
              <span className="muted">{orderStatusLabels[status]}</span>
              <strong>{formatNumber(s?.count ?? 0)}</strong>
              <span className="muted">{formatNumber(s?.pairs ?? 0)} جفت</span>
            </div>
          );
        })}
      </div>
    </section>
  );
}
