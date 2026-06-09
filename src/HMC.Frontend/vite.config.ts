import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Set HMC_SERVER env var to override the proxy target
// e.g. HMC_SERVER=http://192.168.31.2:5000 pnpm dev
const serverTarget = process.env.HMC_SERVER || 'http://localhost:5000';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/hub': {
        target: serverTarget,
        ws: true,
      },
      '/api': {
        target: serverTarget,
      },
      '/health': {
        target: serverTarget,
      },
    },
  },
});
