// کلاینت HTTP: توکن را می‌فرستد و خطای ProblemDetails بک‌اند را به پیام فارسی تبدیل می‌کند.

const TOKEN_KEY = "barlas.token";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
  }
}

export const tokenStore = {
  get: (): string | null => {
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  },
  set: (token: string | null) => {
    try {
      if (token) localStorage.setItem(TOKEN_KEY, token);
      else localStorage.removeItem(TOKEN_KEY);
    } catch {
      /* حالت خصوصی مرورگر */
    }
  },
};

let onUnauthorized: () => void = () => {};
export function setUnauthorizedHandler(fn: () => void) {
  onUnauthorized = fn;
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  const token = tokenStore.get();
  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (init.body && !(init.body instanceof FormData)) headers.set("Content-Type", "application/json");

  const res = await fetch(`/api${path}`, { ...init, headers });

  if (res.status === 401 && token) {
    tokenStore.set(null);
    onUnauthorized();
  }

  if (!res.ok) {
    let message = "خطا در ارتباط با سرور.";
    try {
      const problem = await res.json();
      if (problem?.title) message = problem.title;
    } catch {
      /* بدنه JSON نیست */
    }
    throw new ApiError(message, res.status);
  }

  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

export const post = <T>(path: string, body?: unknown) =>
  api<T>(path, {
    method: "POST",
    body: body === undefined ? undefined : body instanceof FormData ? body : JSON.stringify(body),
  });

export const put = <T>(path: string, body: unknown) => api<T>(path, { method: "PUT", body: JSON.stringify(body) });

export const del = (path: string) => api<void>(path, { method: "DELETE" });
