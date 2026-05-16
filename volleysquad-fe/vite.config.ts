import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: true, // Lắng nghe trên tất cả interfaces (0.0.0.0) để điện thoại cùng mạng có thể truy cập
    port: 7202,
  },
})
