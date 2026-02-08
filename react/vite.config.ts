import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    allowedHosts: ['00c6-87-116-134-30.ngrok-free.app'],
    proxy: {
      '/gamehub': {
        target: 'http://npp-backend:8080',
        ws: true,
        changeOrigin: true
      },
      '/auth': {
        target: 'http://npp-backend:8080',
        changeOrigin: true
      },
      '/player': {
        target: 'http://npp-backend:8080',
        changeOrigin: true
      },
      '/api': {
        target: 'http://npp-backend:8080',
        changeOrigin: true
      }
    }
  }
})
