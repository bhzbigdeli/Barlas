import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";
import { api } from "../../api/client";
import { planStatusLabels, type PlanSummary } from "../../api/types";
import { ErrorText } from "../../components/Field";
import { formatNumber } from "../../lib/format";

export function PlansPage() {
  const plans = useQuery({ queryKey: ["plans"], queryFn: () => api<PlanSummary[]>("/plans") });

  return (
    <section>
      <h2>زیربرنامه‌ها</h2>
      <p className="muted">زیربرنامه جدید از صفحه هر سفارش ساخته می‌شود.</p>
      <ErrorText error={plans.error} />
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
            {plans.data?.map((p) => (
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
            {plans.data?.length === 0 && (
              <tr>
                <td colSpan={5} className="muted">
                  هنوز زیربرنامه‌ای ثبت نشده.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}
