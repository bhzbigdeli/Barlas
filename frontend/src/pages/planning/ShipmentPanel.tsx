import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { post } from "../../api/client";
import type { OrderDetail } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { ErrorText } from "../../components/Field";
import { formatDate, formatNumber, toLatinDigits } from "../../lib/format";

const num = (s: string) => Number(toLatinDigits(s)) || 0;

/** ارسال کامل یا جزئی به تهران: هر سایز حداکثر به اندازه جفت‌های آماده در انبار. */
export function ShipmentPanel({ order }: { order: OrderDetail }) {
  const { can } = useAuth();
  const qc = useQueryClient();
  const rows = order.lines.flatMap((l) =>
    l.sizes.filter((s) => s.readyToShip > 0).map((s) => ({ line: l, size: s, key: `${l.id}|${s.size}` })),
  );
  const [qty, setQty] = useState<Record<string, string>>({});

  useEffect(() => {
    setQty(Object.fromEntries(rows.map((r) => [r.key, String(r.size.readyToShip)])));
  }, [order]);

  const ship = useMutation({
    mutationFn: () =>
      post(`/orders/${order.summary.id}/shipments`, {
        lines: rows
          .map((r) => ({ salesOrderLineId: r.line.id, size: r.size.size, quantity: num(qty[r.key] ?? "0") }))
          .filter((l) => l.quantity > 0),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["orders"] }),
  });

  const total = rows.reduce((s, r) => s + num(qty[r.key] ?? "0"), 0);
  const ready = rows.reduce((s, r) => s + r.size.readyToShip, 0);
  const remaining = order.summary.totalPairs - order.summary.shippedPairs;

  return (
    <>
      {order.shipments.length > 0 && (
        <div className="card">
          <h3>ارسال‌ها به تهران</h3>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>شماره</th>
                  <th>تاریخ</th>
                  <th>جفت</th>
                </tr>
              </thead>
              <tbody>
                {order.shipments.map((s) => (
                  <tr key={s.number}>
                    <td dir="ltr">{s.number}</td>
                    <td>{formatDate(s.shippedAtUtc)}</td>
                    <td>{formatNumber(s.pairs)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {rows.length > 0 && can("ManageWarehouse") && order.summary.status !== "Cancelled" && (
        <form
          className="card"
          onSubmit={(e) => {
            e.preventDefault();
            ship.mutate();
          }}
        >
          <h3>ارسال به تهران</h3>
          <p className="muted">
            {formatNumber(ready)} جفت آماده در انبار؛ {formatNumber(remaining)} جفت از سفارش هنوز ارسال نشده. برای ارسال جزئی،
            تعداد هر سایز را کم کنید.
          </p>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>مدل / رنگ</th>
                  <th>سایز</th>
                  <th>آماده</th>
                  <th>ارسال</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.key}>
                    <td>
                      {r.line.modelCode} — {r.line.colorName}
                    </td>
                    <td>{formatNumber(r.size.size)}</td>
                    <td>{formatNumber(r.size.readyToShip)}</td>
                    <td>
                      <input
                        className="qty"
                        inputMode="numeric"
                        aria-label={`ارسال سایز ${r.size.size}`}
                        value={qty[r.key] ?? ""}
                        onChange={(e) => setQty({ ...qty, [r.key]: e.target.value })}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <ErrorText error={ship.error} />
          <div className="actions">
            <button type="submit" disabled={ship.isPending || total === 0}>
              {total === remaining ? "ثبت ارسال کامل" : `ثبت ارسال جزئی (${formatNumber(total)} جفت)`}
            </button>
          </div>
        </form>
      )}
      {order.summary.status === "Shipped" && <p className="ok">این سفارش کامل به تهران ارسال شده است.</p>}
    </>
  );
}
