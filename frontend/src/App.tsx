import { Navigate, Route, Routes } from "react-router";
import { useAuth } from "./auth/AuthContext";
import { Layout } from "./components/Layout";
import { RequirePermission } from "./components/RequirePermission";
import { RolesPage } from "./pages/admin/RolesPage";
import { UsersPage } from "./pages/admin/UsersPage";
import { WorkersPage } from "./pages/admin/WorkersPage";
import { BasicDataPage } from "./pages/catalog/BasicDataPage";
import { ProductEditPage } from "./pages/catalog/ProductEditPage";
import { ProductsPage } from "./pages/catalog/ProductsPage";
import { ChangePasswordPage } from "./pages/ChangePasswordPage";
import { DashboardPage } from "./pages/DashboardPage";
import { LoginPage } from "./pages/LoginPage";
import { OrderDetailPage } from "./pages/OrderDetailPage";
import { OrdersPage } from "./pages/OrdersPage";
import { ScanPage } from "./pages/ScanPage";
import { KardexPage } from "./pages/planning/KardexPage";
import { PlanPage } from "./pages/planning/PlanPage";
import { PlansPage } from "./pages/planning/PlansPage";
import { WarehousePage } from "./pages/WarehousePage";

export function App() {
  const { user, loading } = useAuth();

  if (loading) return <p className="center muted">در حال بارگذاری…</p>;

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={user ? <Layout /> : <Navigate to="/login" replace />}>
        <Route index element={<DashboardPage />} />
        <Route path="orders" element={<OrdersPage />} />
        <Route path="orders/:id" element={<OrderDetailPage />} />
        <Route path="plans" element={<PlansPage />} />
        <Route path="plans/:id" element={<PlanPage />} />
        <Route path="plans/:id/kardex" element={<KardexPage />} />
        <Route
          path="warehouse"
          element={
            <RequirePermission permission="ManageWarehouse">
              <WarehousePage />
            </RequirePermission>
          }
        />
        <Route
          path="scan"
          element={
            <RequirePermission permission="ScanStages">
              <ScanPage />
            </RequirePermission>
          }
        />

        <Route path="catalog/products" element={<ProductsPage />} />
        <Route
          path="catalog/products/new"
          element={
            <RequirePermission permission="ManageCatalog">
              <ProductEditPage />
            </RequirePermission>
          }
        />
        <Route path="catalog/products/:id" element={<ProductEditPage />} />
        <Route path="catalog/basic" element={<BasicDataPage />} />

        <Route
          path="admin/users"
          element={
            <RequirePermission permission="ManageUsers">
              <UsersPage />
            </RequirePermission>
          }
        />
        <Route
          path="admin/roles"
          element={
            <RequirePermission permission="ManageUsers">
              <RolesPage />
            </RequirePermission>
          }
        />
        <Route
          path="admin/workers"
          element={
            <RequirePermission permission="ManageUsers">
              <WorkersPage />
            </RequirePermission>
          }
        />
        <Route path="account/password" element={<ChangePasswordPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
