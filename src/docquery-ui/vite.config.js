import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Dev server proxies /api to the local DocQuery API so app code only ever
// uses relative URLs. Point VITE_API_PROXY_TARGET elsewhere (e.g. the DGX
// Spark) to develop against a remote backend.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
});
