import { useEffect, useState } from "react";
import { NavLink, Outlet, useLocation } from "react-router";
import type { Permission } from "../api/types";
import { useAuth } from "../auth/AuthContext";

interface NavItem {
  to: string;
  label: string;
  /** بدون مجوز یعنی برای همه کاربران واردشده */
  permission?: Permission;
}

interface NavSection {
  title: string;
  items: NavItem[];
}

const sections: NavSection[] = [
  {
    title: "تولید",
    items: [
      { to: "/", label: "داشبورد" },
      { to: "/orders", label: "سفارش‌ها" },
      { to: "/scan", label: "اسکن کاردکس", permission: "ScanStages" },
    ],
  },
  {
    title: "اطلاعات پایه",
    items: [
      { to: "/catalog/products", label: "محصولات و سری سایز" },
      { to: "/catalog/basic", label: "رنگ و زیره" },
    ],
  },
  {
    title: "مدیریت",
    items: [
      { to: "/admin/users", label: "کاربران", permission: "ManageUsers" },
      { to: "/admin/roles", label: "سطوح دسترسی", permission: "ManageUsers" },
    ],
  },
];

/** منوی کناری (سمت راست)؛ در گوشی با دکمه منو باز و بسته می‌شود. */
export function Layout() {
  const { user, logout, can } = useAuth();
  const [open, setOpen] = useState(false);
  const location = useLocation();

  useEffect(() => setOpen(false), [location.pathname]);

  return (
    <div className={`app ${open ? "nav-open" : ""}`}>
      <aside className="sidebar" aria-label="منوی اصلی">
        <div className="sidebar-brand">
          <strong>برلاس</strong>
          <span className="muted">برنامه‌ریزی و پیگیری تولید</span>
        </div>

        <nav className="sidebar-nav">
          {sections.map((section) => {
            const items = section.items.filter((i) => !i.permission || can(i.permission));
            if (items.length === 0) return null;
            return (
              <div className="nav-section" key={section.title}>
                <span className="nav-title">{section.title}</span>
                {items.map((item) => (
                  <NavLink key={item.to} to={item.to} end={item.to === "/"}>
                    {item.label}
                  </NavLink>
                ))}
              </div>
            );
          })}
        </nav>

        {user && (
          <div className="sidebar-user">
            <strong>{user.fullName}</strong>
            <span className="muted">
              {user.roleName}
              {user.unitName ? ` · ${user.unitName}` : ""}
            </span>
            <div className="sidebar-actions">
              <NavLink to="/account/password">تغییر رمز</NavLink>
              <button className="link" onClick={logout}>
                خروج
              </button>
            </div>
          </div>
        )}
      </aside>

      <div className="backdrop" onClick={() => setOpen(false)} />

      <div className="main">
        <header className="mobile-bar">
          <button className="secondary" onClick={() => setOpen(true)} aria-label="باز کردن منو">
            ☰
          </button>
          <strong>برلاس</strong>
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
