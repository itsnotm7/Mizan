import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    // The browser only talks to localhost:5173. Vite forwards /api calls to the API,
    // so the browser sees one origin and CORS never comes into play.
    proxy: {
      '/api': 'http://localhost:5199',
    },
  },
})
