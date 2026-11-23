import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// در توسعه، درخواست‌های /api به بک‌اند .NET روی پورت 5080 می‌روند.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    host: true, // برای تست اسکن با گوشی روی شبکه داخلی
    proxy: {
      "/api": "http://localhost:5080",
      "/health": "http://localhost:5080",
    },
  },
  test: {
    environment: "node",
  },
});
