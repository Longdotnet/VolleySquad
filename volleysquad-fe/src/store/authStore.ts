// ============================================================
// AUTH STORE - Quản lý trạng thái xác thực toàn cục với Zustand
// ============================================================
//
// TẠI SAO ZUSTAND thay vì Redux?
// ┌─────────────────────────────────────────────────────────────┐
// │ Redux (cũ)    │ Boilerplate nhiều: Action, Reducer, Selector│
// │               │ Cần Provider bọc toàn bộ app               │
// │               │ Phù hợp app lớn, nhiều developer           │
// │ Zustand (mới) │ API đơn giản: 1 hàm create() là đủ         │
// │               │ Không cần Provider                          │
// │               │ Bundle size nhỏ (~1KB)                      │
// │               │ Phù hợp side project, team nhỏ             │
// └─────────────────────────────────────────────────────────────┘
//
// TẠI SAO cần Global State thay vì useState trong component?
//   - Token và thông tin user cần dùng ở NHIỀU nơi:
//     LoginPage → lưu token, DashboardPage → đọc user.name,
//     axiosInstance → đọc token để gắn vào header...
//   - Nếu dùng useState: phải "prop drilling" qua nhiều tầng component
//   - Global State: component nào cũng subscribe trực tiếp được
//
// ============================================================
// ⚠️ SECURITY: localStorage vs httpOnly Cookie
// ============================================================
// localStorage (hiện tại dùng):
//   ✓ Đơn giản, persist sau khi đóng tab
//   ✗ Có thể bị đọc bởi JavaScript => dễ bị XSS attack
//   XSS: Attacker inject JS vào trang => đọc localStorage.getItem('token')
//
// httpOnly Cookie (production nên dùng):
//   ✓ JavaScript KHÔNG đọc được (browser tự gửi theo mỗi request)
//   ✓ Chống XSS hoàn toàn
//   ✗ Cần thêm CSRF protection (SameSite=Strict giải quyết được)
//   ✗ Backend phải set cookie qua Set-Cookie header
//
// Với SPA demo này: localStorage OK. Production app: dùng httpOnly cookie.
import { create } from 'zustand';
import type { Member } from '../types/Member';

// ============================================================
// TYPESCRIPT INTERFACE - Định nghĩa "hình dạng" của state
// ============================================================
// Interface trong TypeScript = hợp đồng (contract) mà object phải tuân theo.
// Khác với type alias:
//   interface: Có thể extends, Declaration merging
//   type:      Linh hoạt hơn (union, intersection), không merging
// Với object shape như thế này, interface và type đều dùng được.
// Convention: dùng interface cho object shape, type cho union/utility types.
interface AuthState {
  token: string | null;   // string | null = union type: có thể là string HOẶC null
  user: Member | null;
  setAuth: (token: string, user: Member) => void;  // Function type signature
  logout: () => void;
}

// create<AuthState>() — Generic function với type parameter <AuthState>
// TypeScript tự kiểm tra: set({ unknownProp: 1 }) => compile error!
export const useAuthStore = create<AuthState>((set) => ({
  // Khởi tạo token từ localStorage để persist sau khi F5 trang
  // Optional chaining: localStorage.getItem('token') có thể null => trả null
  token: localStorage.getItem('token'),
  user: null,

  setAuth: (token, user) => {
    localStorage.setItem('token', token);
    // set(): Zustand tự merge partial state (giống setState trong class component)
    // KHÔNG cần spread operator: set({ ...state, token, user })
    set({ token, user });
  },

  logout: () => {
    localStorage.removeItem('token');
    set({ token: null, user: null });
  },
}));
