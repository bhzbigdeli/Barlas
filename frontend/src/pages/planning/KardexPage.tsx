import { useQuery } from "@tanstack/react-query";
import QRCode from "qrcode";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router";
import { api } from "../../api/client";
import type { Bundle, PlanDetail } from "../../api/types";
import { ErrorText } from "../../components/Field";
import { formatNumber } from "../../lib/format";

/** کاردکس‌های قابل چاپ: هر دسته یک برگه با QR، جدول سایز و خانه مراحل. */
export function KardexPage() {
  const { id } = useParams();
  const plan = useQuery({ queryKey: ["plans", id], queryFn: () => api<PlanDetail>(`/plans/${id}`) });

  if (!plan.data) return <ErrorText error={plan.error} />;
  const p = plan.data;

  return (
    <section>
      <div className="row no-print">
        <Link to={`/plans/${p.id}`}>← زیربرنامه {p.number}</Link>
        <button onClick={() => window.print()}>چاپ</button>
      </div>
      <div className="kardex-list">
        {p.bundles.map((b) => (
          <KardexCard key={b.id} bundle={b} plan={p} />
        ))}
      </div>
    </section>
  );
}

function KardexCard({ bundle, plan }: { bundle: Bundle; plan: PlanDetail }) {
  const [qr, setQr] = useState<string>();
  useEffect(() => {
    QRCode.toDataURL(bundle.qrToken, { margin: 1, width: 220 }).then(setQr);
  }, [bundle.qrToken]);

  return (
    <article className="kardex" data-qr={bundle.qrToken}>
      <header>
        <div>
          <strong className="kardex-title">کاردکس دسته {bundle.number}</strong>
          <span className="muted">
            {plan.unitName} · زیربرنامه <bdi>{plan.number}</bdi>
            {plan.orderNumber && (
              <>
                {" · سفارش "}
                <bdi>{plan.orderNumber}</bdi>
              </>
            )}
          </span>
          <span>
            {bundle.modelName} ({bundle.modelCode}) — {bundle.colorName} · {formatNumber(bundle.pairs)} جفت
          </span>
        </div>
        {qr && <img src={qr} alt={`QR دسته ${bundle.number}`} width={120} height={120} />}
      </header>
      <table>
        <thead>
          <tr>
            {bundle.sizes.map((s) => (
              <th key={s.size}>{formatNumber(s.size)}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          <tr>
            {bundle.sizes.map((s) => (
              <td key={s.size}>{formatNumber(s.quantity)}</td>
            ))}
          </tr>
        </tbody>
      </table>
      <ol className="kardex-stages">
        {bundle.route.map((stage) => (
          <li key={stage}>
            <span>{stage}</span>
            <span className="muted">کارگر: ……… تاریخ: ………</span>
          </li>
        ))}
      </ol>
      <small className="muted" dir="ltr">
        {bundle.qrToken}
      </small>
    </article>
  );
}
