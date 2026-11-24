import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { api, post } from "../api/client";
import { orderStatusLabels, type FreeStockRow, type ReceiveResult } from "../api/types";
import { ErrorText } from "../components/Field";
import { QrCamera } from "../components/QrCamera";
import { formatNumber } from "../lib/format";

/** ورود دسته تمام‌شده به انبار با اسکن کاردکس؛ اول سهم سفارش پر می‌شود و بقیه به موجودی آزاد می‌رود. */
export function WarehousePage() {
  const qc = useQueryClient();
  const [qr, setQr] = useState("");
  const stock = useQuery({ queryKey: ["free-stock"], queryFn: () => api<FreeStockRow[]>("/warehouse/free-stock") });

  const receive = useMutation({
    mutationFn: (token: string) => post<ReceiveResult>("/warehouse/receive", { qrToken: token }),
    onSuccess: () => {
      setQr("");
      qc.invalidateQueries({ queryKey: ["free-stock"] });
      qc.invalidateQueries({ queryKey: ["orders"] });
      qc.invalidateQueries({ queryKey: ["plans"] });
    },
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    receive.mutate(qr);
  }

  const r = receive.data;
  return (
    <section>
      <h2>انبار محصول</h2>

      <div className="scan">
        <QrCamera onResult={(token) => receive.mutate(token)} />

        <form className="card" onSubmit={submit}>
          <label>
            کد کاردکس
            <input value={qr} onChange={(e) => setQr(e.target.value.trim())} dir="ltr" required />
          </label>
          <ErrorText error={receive.error} />
          {r && (
            <div className="bundle">
              <strong className="ok">دسته <bdi>{r.bundleNumber}</bdi> وارد انبار شد.</strong>
              <span>
                {formatNumber(r.toOrder)} جفت به سفارش <bdi>{r.orderNumber ?? "—"}</bdi> · {formatNumber(r.toFreeStock)} جفت به
                موجودی آزاد
              </span>
              {r.orderStatus && <span>وضعیت سفارش: {orderStatusLabels[r.orderStatus]}</span>}
            </div>
          )}
          <button type="submit" disabled={receive.isPending}>
            ثبت ورود به انبار
          </button>
        </form>
      </div>

      <h3>موجودی آزاد</h3>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>واحد</th>
              <th>مدل</th>
              <th>رنگ</th>
              <th>سایز</th>
              <th>جفت</th>
            </tr>
          </thead>
          <tbody>
            {stock.data?.map((s) => (
              <tr key={`${s.unitName}-${s.modelCode}-${s.colorName}-${s.size}`}>
                <td>{s.unitName}</td>
                <td>
                  {s.modelName} ({s.modelCode})
                </td>
                <td>{s.colorName}</td>
                <td>{formatNumber(s.size)}</td>
                <td>{formatNumber(s.quantity)}</td>
              </tr>
            ))}
            {stock.data?.length === 0 && (
              <tr>
                <td colSpan={5} className="muted">
                  موجودی آزاد خالی است.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}
