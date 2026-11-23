import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef } from "react";
import { Link } from "react-router";
import { api, post } from "../api/client";
import { orderStatusLabels, type IntakeResult, type OrderSummary } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { formatDate, formatNumber } from "../lib/format";

export function OrdersPage() {
  const { user, can } = useAuth();
  const queryClient = useQueryClient();
  const fileRef = useRef<HTMLInputElement>(null);
  const orders = useQuery({ queryKey: ["orders"], queryFn: () => api<OrderSummary[]>("/orders") });

  const importMutation = useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append("file", file);
      return post<IntakeResult>("/orders/import", form);
    },
    onSettled: () => {
      if (fileRef.current) fileRef.current.value = "";
      return queryClient.invalidateQueries({ queryKey: ["orders"] });
    },
  });

  return (
    <section>
      <div className="row">
        <h2>سفارش‌های تهران</h2>
        {can("ImportOrders") && (
          <label className="button">
            ورود فایل اکسل
            <input
              ref={fileRef}
              type="file"
              accept=".xlsx"
              hidden
              onChange={(e) => e.target.files?.[0] && importMutation.mutate(e.target.files[0])}
            />
          </label>
        )}
      </div>

      {importMutation.isPending && <p className="muted">در حال خواندن فایل…</p>}
      {importMutation.isError && <p className="error">{(importMutation.error as Error).message}</p>}
      {importMutation.data && (
        <div className="card">
          <p>
            {formatNumber(importMutation.data.created)} سفارش جدید ثبت شد؛ {formatNumber(importMutation.data.skipped)}{" "}
            سفارش تکراری بود.
          </p>
          {importMutation.data.errors.length > 0 && (
            <ul className="error">
              {importMutation.data.errors.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      {orders.isLoading && <p className="muted">در حال بارگذاری…</p>}
      {orders.isError && <p className="error">{(orders.error as Error).message}</p>}
      {orders.data && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>شماره</th>
                {user?.seesAllUnits && <th>واحد</th>}
                <th>تاریخ</th>
                <th>تحویل</th>
                <th>وضعیت</th>
                <th>پیشرفت</th>
              </tr>
            </thead>
            <tbody>
              {orders.data.map((o) => (
                <tr key={o.id}>
                  <td>
                    <Link to={`/orders/${o.id}`}>{o.externalNumber}</Link>
                  </td>
                  {user?.seesAllUnits && <td>{o.unitName}</td>}
                  <td>{formatDate(o.orderDate)}</td>
                  <td>{formatDate(o.dueDate)}</td>
                  <td>{orderStatusLabels[o.status]}</td>
                  <td>
                    {formatNumber(o.fulfilledPairs)} از {formatNumber(o.totalPairs)}
                  </td>
                </tr>
              ))}
              {orders.data.length === 0 && (
                <tr>
                  <td colSpan={6} className="muted">
                    هنوز سفارشی نیست.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
