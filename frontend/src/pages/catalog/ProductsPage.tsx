import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";
import { api } from "../../api/client";
import type { Product } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { AuthImage } from "../../components/AuthImage";
import { ErrorText } from "../../components/Field";
import { formatNumber } from "../../lib/format";

export function ProductsPage() {
  const { can } = useAuth();
  const products = useQuery({ queryKey: ["products"], queryFn: () => api<Product[]>("/catalog/products") });

  return (
    <section>
      <div className="row">
        <h2>محصولات و سری سایز</h2>
        {can("ManageCatalog") && (
          <Link className="button" to="/catalog/products/new">
            محصول جدید
          </Link>
        )}
      </div>

      <ErrorText error={products.error} />

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th aria-label="عکس" />
              <th>کد</th>
              <th>نام</th>
              <th>گروه</th>
              <th>سری (سایز × تعداد)</th>
              <th>جفت در سری</th>
              <th>زیره</th>
              <th>وضعیت</th>
            </tr>
          </thead>
          <tbody>
            {products.data?.map((p) => (
              <tr key={p.id} className={p.isActive ? "" : "inactive"}>
                <td className="thumb-cell">
                  {p.imageUploadedAtUtc && (
                    <AuthImage
                      className="thumb"
                      src={`/catalog/products/${p.id}/image?v=${encodeURIComponent(p.imageUploadedAtUtc)}`}
                      alt=""
                    />
                  )}
                </td>
                <td dir="ltr">
                  <Link to={`/catalog/products/${p.id}`}>{p.code}</Link>
                </td>
                <td>
                  <Link to={`/catalog/products/${p.id}`}>{p.name}</Link>
                </td>
                <td>{p.group ?? "—"}</td>
                <td>
                  {p.series.length === 0 && <span className="warn">تعریف نشده</span>}
                  <div className="series-chips">
                    {p.series.map((s) => (
                      <span key={s.size} className="chip" title={`سایز ${s.size}: ${s.quantity} جفت در هر سری`}>
                        <strong>{formatNumber(s.size)}</strong> × {formatNumber(s.quantity)}
                      </span>
                    ))}
                  </div>
                </td>
                <td>{formatNumber(p.pairsPerSeries)}</td>
                <td>{p.soleName ?? "—"}</td>
                <td>{p.isActive ? "فعال" : "غیرفعال"}</td>
              </tr>
            ))}
            {products.data?.length === 0 && (
              <tr>
                <td colSpan={8} className="muted">
                  هنوز محصولی تعریف نشده.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}
