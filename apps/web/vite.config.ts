import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { "@": path.join(import.meta.dirname, "src") },
  },
  build: {
    rolldownOptions: {
      output: {
        // Keep each chunk under 500 kB: React and the other libraries the
        // page needs up front get their own chunks. Recharts is not listed,
        // so it stays in the lazily loaded chart chunk.
        codeSplitting: {
          groups: [
            {
              name: "react",
              test: /node_modules[\\/](react|react-dom|scheduler)[\\/]/,
            },
            {
              name: "vendor",
              test: /node_modules[\\/](zod|@tanstack|react-hook-form|@hookform|decimal\.js|@radix-ui|radix-ui|tailwind-merge|clsx|class-variance-authority|lucide-react)[\\/]/,
            },
          ],
        },
      },
    },
  },
  server: {
    port: 5173,
    proxy: { "/api": "http://127.0.0.1:3001" },
  },
  test: {
    name: "web",
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
  },
});
