import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router";
import { api } from "../api/client";
import { orderStatusLabels, type OrderDetail } from "../api/types";
import { formatDate, formatNumber } from "../lib/format";
import { OrderPlanningPanel } from "./planning/OrderPlanningPanel";

export function OrderDetailPage() {
  const { id } = useParams();
  const order = useQuery({ queryKey: ["orders", id], queryFn: () => api<OrderDetail>(`/orders/${id}`) });

  if (order.isLoading) return <p className="muted">در حال بارگذاری…</p>;
  if (order.isError) return <p className="error">{(order.error as Error).message}</p>;
  if (!order.data) return null;

  const { summary, lines } = order.data;
  return (
    <section>
      <Link to="/orders">← سفارش‌ها</Link>
      <h2>سفارش {summary.externalNumber}</h2>
      <p className="muted">
        {summary.unitName} · {formatDate(summary.orderDate)} · تحویل {formatDate(summary.dueDate)} ·{" "}
        {orderStatusLabels[summary.status]}
      </p>

      {lines.map((line) => (
        <div className="card" key={line.lineNumber}>
          <h3>
            {line.modelName} ({line.modelCode}) — {line.colorName}
          </h3>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>سایز</th>
                  <th>سفارش</th>
                  <th>از موجودی آزاد</th>
                  <th>از تولید</th>
                  <th>باقیمانده</th>
                </tr>
              </thead>
              <tbody>
                {line.sizes.map((s) => (
                  <tr key={s.size}>
                    <td>{formatNumber(s.size)}</td>
                    <td>{formatNumber(s.quantity)}</td>
                    <td>{formatNumber(s.fromStock)}</td>
                    <td>{formatNumber(s.fromProduction)}</td>
                    <td>{formatNumber(s.outstanding)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ))}

      <OrderPlanningPanel orderId={summary.id} status={summary.status} />
    </section>
  );
}
