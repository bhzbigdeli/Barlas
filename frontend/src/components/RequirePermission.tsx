import type { ReactNode } from "react";
import type { Permission } from "../api/types";
import { useAuth } from "../auth/AuthContext";

/** صفحه‌ای که مجوز خاص می‌خواهد. سرور هم همین را بررسی می‌کند؛ این فقط برای نمایش است. */
export function RequirePermission({ permission, children }: { permission: Permission; children: ReactNode }) {
  const { can } = useAuth();
  if (!can(permission)) return <p className="error">دسترسی به این بخش ندارید.</p>;
  return <>{children}</>;
}
