// ============================================================
// LOGIN PAGE - Trang đăng nhập với React Hooks + TypeScript
// ============================================================
//
// REACT HOOKS - Câu hỏi phỏng vấn phổ biến: "Hook là gì?"
// ============================================================
// Hook là function đặc biệt (bắt đầu bằng "use") cho phép function component
// dùng được các tính năng của React (state, lifecycle, context...).
//
// Trước Hook (React 16.8): Phải viết class component để dùng state.
// Sau Hook: Function component nhẹ hơn, dễ test, dễ tái sử dụng logic.
//
// CÁC HOOK DÙNG TRONG FILE NÀY:
// ┌─────────────────────────────────────────────────────────────────────┐
// │ useState<T>(init) │ Tạo state variable + setter function           │
// │                   │ Mỗi lần set → component re-render               │
// │                   │ Generic <T>: TypeScript tự infer type từ init   │
// │ useNavigate()     │ React Router hook — điều hướng programmatic     │
// │                   │ Thay vì <Link> (click), navigate() dùng trong   │
// │                   │ code (sau khi login thành công)                 │
// └─────────────────────────────────────────────────────────────────────┘
//
// TYPESCRIPT TRONG REACT - "Tại sao dùng TypeScript không dùng JavaScript?"
// ============================================================
// React.FormEvent: TypeScript type cho DOM form event.
//   e.preventDefault(): Ngăn form submit theo cách truyền thống (reload trang).
//   Nếu không có TypeScript: biết "e" có .preventDefault() chỉ khi đọc docs hoặc runtime.
//   Có TypeScript: IDE gợi ý .preventDefault() ngay khi gõ "e." → tăng tốc dev.
//
// Type safety ngăn chặn bug:
//   setUsername(123)   → TypeScript lỗi (number ≠ string)
//   setLoading("yes")  → TypeScript lỗi (string ≠ boolean)
//   setUsername("ok")  → Hợp lệ ✓
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { login } from '../api/authApi';
import { useAuthStore } from '../store/authStore';

// ============================================================
// FUNCTION COMPONENT
// ============================================================
// "export default" = có thể import không cần dùng tên:
//   import LoginPage from './pages/LoginPage'  (không cần {})
//   Ngược lại với named export: import { login } from './api/authApi'
//
// React.FC<Props> vs function declaration:
//   Cách 1: const LoginPage: React.FC = () => {}  (explicit type annotation)
//   Cách 2: export default function LoginPage() {}  (TypeScript infer từ JSX return)
//   Cả hai OK. Cách 2 đơn giản hơn cho component không có Props.
export default function LoginPage() {
  // useState<string>('') = state có type string, giá trị ban đầu là ''
  // TypeScript infer type từ initial value nên không cần viết <string> tường minh
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false); // boolean — TypeScript infer từ false

  // Selector pattern trong Zustand:
  //   useAuthStore((s) => s.setAuth): Chỉ subscribe vào "setAuth", không phải toàn bộ state.
  //   Tối ưu: component chỉ re-render khi "setAuth" thay đổi (thực tế không bao giờ đổi).
  //   Nếu viết: useAuthStore() → subscribe toàn bộ state → re-render mỗi khi token/user đổi.
  const setAuth = useAuthStore((s) => s.setAuth);
  const navigate = useNavigate();

  // ============================================================
  // ASYNC EVENT HANDLER
  // ============================================================
  // async/await trong React giống như trong .NET:
  //   - Không block UI thread trong khi chờ API response
  //   - UI vẫn responsive (loading spinner chạy được)
  //
  // try/catch/finally pattern:
  //   try:     Code có thể throw error (API call)
  //   catch:   Xử lý khi có lỗi (show error message)
  //   finally: Luôn chạy dù thành công hay thất bại (tắt loading)
  //            Giống như "using" trong C# (cleanup code)
  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault(); // Ngăn form reload trang (default browser behavior)
    setLoading(true);
    setError('');
    try {
      // Await API call — trong lúc này UI vẫn render, spinner hoạt động
      const data = await login(username, password);
      // Lưu token và user vào global store → axiosInstance tự gắn vào các request sau
      setAuth(data.token, data.member);
      // Điều hướng sang dashboard sau khi login thành công
      navigate('/dashboard');
    } catch {
      // ⚠️ NOTE: Error message cố ý mơ hồ (không nói "user không tồn tại" hay "sai pass")
      // Tránh username enumeration: Hacker thử username → biết username có tồn tại không.
      // Thực tế: "Thông tin đăng nhập không đúng" là message chuẩn bảo mật.
      setError('Thông tin đăng nhập không đúng hoặc server chưa chạy.');
    } finally {
      setLoading(false);
    }
  };

  // ============================================================
  // JSX - "JavaScript XML" - Cú pháp template của React
  // ============================================================
  // JSX được Babel/Vite compile thành: React.createElement('div', {className: '...'}, ...)
  // Khác HTML:
  //   class → className    (vì "class" là từ khóa JS)
  //   for   → htmlFor      (label for)
  //   style={{ }} → object (không phải string)
  //   {expression}         → nhúng JS expression vào template
  //
  // TAILWIND CSS: Utility-first CSS framework.
  //   Thay vì: .login-btn { background: blue; padding: 8px 16px; border-radius: 8px; }
  //   Dùng:    className="bg-blue-600 px-4 py-2 rounded-lg"
  //   Ưu điểm: Không cần đặt tên class, không bị CSS conflict, bundle nhỏ (purge unused)
  return (
    <div className="min-h-screen flex items-center justify-center" style={{ backgroundColor: '#0f172a' }}>
      <div className="w-full max-w-sm p-8 rounded-3xl border border-white/10" style={{ background: 'rgba(30,41,59,0.7)', backdropFilter: 'blur(12px)' }}>
        {/* Logo */}
        <div className="flex items-center justify-center gap-2 mb-8">
          <div className="bg-blue-600 p-2 rounded-lg">
            <i className="fas fa-volleyball text-white text-xl"></i>
          </div>
          <span className="font-extrabold text-xl tracking-tight text-white">
            VOLLEY<span className="text-blue-500">SQUAD</span>
          </span>
        </div>

        <h2 className="text-2xl font-bold text-white text-center mb-1">Đăng nhập</h2>
        <p className="text-slate-400 text-sm text-center mb-6">Nhập username và mật khẩu để tiếp tục</p>

        {/* onSubmit trên form (không phải onClick trên button):
            Bắt cả 2 cách submit: click button VÀ nhấn Enter trong input field */}
        <form onSubmit={handleLogin} className="space-y-4">
          {/* Controlled Input: value={username} + onChange → React kiểm soát hoàn toàn giá trị.
              Uncontrolled: dùng ref đọc DOM trực tiếp (ít dùng hơn với form phức tạp). */}
          <input
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            placeholder="VD: Tài Admin"
            required
            className="w-full bg-slate-800 text-white border border-slate-700 rounded-xl px-4 py-3 text-sm focus:outline-none focus:border-blue-500 placeholder-slate-500"
          />

          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="Mật khẩu"
            minLength={8}
            required
            className="w-full bg-slate-800 text-white border border-slate-700 rounded-xl px-4 py-3 text-sm focus:outline-none focus:border-blue-500 placeholder-slate-500"
          />

          {/* Conditional rendering: {condition && <JSX>}
              Nếu error là '' (falsy) → không render gì.
              Nếu error có giá trị → render thẻ <p>.
              Cách khác: {condition ? <A/> : <B/>} (ternary) */}
          {error && (
            <p className="text-red-400 text-xs text-center">{error}</p>
          )}

          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 rounded-xl font-bold text-white text-sm disabled:opacity-60"
            style={{ background: 'linear-gradient(90deg, #3b82f6, #2563eb)' }}
          >
            {/* Ternary expression để hiển thị text theo trạng thái loading */}
            {loading ? 'Đang đăng nhập...' : 'Đăng nhập'}
          </button>
        </form>

        <p className="text-slate-600 text-xs text-center mt-6">
          VolleySquad v1.0 — Quản lý bóng chuyền phong trào
        </p>
      </div>
    </div>
  );
}
