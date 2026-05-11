// ============================================================
// AXIOS INSTANCE - HTTP Client tập trung cho toàn bộ ứng dụng
// ============================================================
//
// TẠI SAO tạo instance riêng thay vì dùng axios trực tiếp?
//   import axios from 'axios'
//   axios.get('https://localhost:7202/api/match/members')  // ← Dùng trực tiếp
//
// Vấn đề khi dùng trực tiếp:
//   1. Lặp lại baseURL ở mọi nơi → khó thay đổi khi deploy
//   2. Mỗi file phải tự thêm header Authorization → dễ quên, bug security
//   3. Không thể cấu hình tập trung (timeout, interceptor, error handling)
//
// Instance tập trung giải quyết: 1 chỗ cấu hình, dùng ở khắp nơi.
//
// ============================================================
// INTERCEPTOR PATTERN - Middleware cho HTTP request/response
// ============================================================
// Interceptor hoạt động giống Middleware trong ASP.NET Core:
//   Request interceptor:  Can thiệp TRƯỚC khi request ra ngoài
//                         → Tự động gắn JWT token vào header
//   Response interceptor: Can thiệp KHI nhận được response
//                         → Tự động redirect khi nhận 401 (token hết hạn)
//
// Luồng: Component gọi api.get() → [interceptor] → gắn token → gửi request
//        Nhận response → [interceptor] → xử lý lỗi chung → trả về component
import axios from 'axios';
import { useAuthStore } from '../store/authStore';

// ============================================================
// BASE URL từ Environment Variable (Vite)
// ============================================================
// import.meta.env.VITE_API_URL: Vite inject env var lúc build.
// .env.local:     VITE_API_URL=https://localhost:7202/api  (local dev, không commit)
// .env.production: VITE_API_URL=https://api.volleysquad.com/api (production)
//
// Fallback về localhost khi không có env var (dev nhanh không cần .env file).
// Tại sao VITE_ prefix? Vite chỉ expose env var có prefix VITE_ vào client bundle
// để tránh vô tình lộ server-side secrets.
const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'https://localhost:7202/api',
  timeout: 10000, // 10 giây timeout — tránh request treo mãi khi server chết
});

// ============================================================
// REQUEST INTERCEPTOR - Tự động gắn JWT vào mọi request
// ============================================================
// useAuthStore.getState(): Đọc state trực tiếp (không dùng hook).
// Hook (useAuthStore()) chỉ dùng được trong React component/hook.
// Ở đây là file .ts thuần → phải dùng .getState() thay thế.
api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;
  // Chỉ gắn header nếu token tồn tại (request không cần auth sẽ không có header này)
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// ============================================================
// RESPONSE INTERCEPTOR - Xử lý lỗi chung
// ============================================================
// Tự động logout khi nhận 401 (token hết hạn hoặc không hợp lệ).
// Thay vì mỗi component phải tự xử lý 401 → tập trung 1 chỗ.
api.interceptors.response.use(
  (response) => response, // Request thành công: trả nguyên response
  (error) => {
    if (error.response?.status === 401) {
      // Token hết hạn → xóa state và redirect về login
      useAuthStore.getState().logout();
      window.location.href = '/login';
    }
    // Ném lỗi tiếp để component catch và hiển thị error message cụ thể nếu cần
    return Promise.reject(error);
  }
);

export default api;
