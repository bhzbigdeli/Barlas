import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { api, post, setUnauthorizedHandler, tokenStore } from "../api/client";
import type { LoginResponse, Permission, UserInfo } from "../api/types";

interface AuthState {
  user: UserInfo | null;
  loading: boolean;
  login: (userName: string, password: string) => Promise<void>;
  logout: () => void;
  can: (permission: Permission) => boolean;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null);
  const [loading, setLoading] = useState(() => tokenStore.get() !== null);

  const logout = useCallback(() => {
    tokenStore.set(null);
    setUser(null);
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(logout);
    if (!tokenStore.get()) return;
    api<UserInfo>("/auth/me")
      .then(setUser)
      .catch(logout)
      .finally(() => setLoading(false));
  }, [logout]);

  const login = useCallback(async (userName: string, password: string) => {
    const res = await post<LoginResponse>("/auth/login", { userName, password });
    tokenStore.set(res.accessToken);
    setUser(res.user);
  }, []);

  const can = useCallback((p: Permission) => user?.permissions.includes(p) ?? false, [user]);

  const value = useMemo(() => ({ user, loading, login, logout, can }), [user, loading, login, logout, can]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
