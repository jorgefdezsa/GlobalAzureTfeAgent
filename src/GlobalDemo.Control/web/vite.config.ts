import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// El build va a ../wwwroot, que sirve el backend; en desarrollo /api se proxifica al backend en el 5200.
export default defineConfig({
  plugins: [react()],
  build: { outDir: '../wwwroot', emptyOutDir: true },
  server: { proxy: { '/api': 'http://127.0.0.1:5200' } },
})
