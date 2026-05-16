// ============================================================
// AUTH API - HTTP calls cho Authentication
// ============================================================
//
// "import type" vs "import":
//   import type { Member }: Chỉ import type definition, KHÔNG import runtime value.
//   TypeScript xóa hoàn toàn khi compile → bundle nhỏ hơn.
//   Dùng khi chỉ cần type để type-check, không cần object/function/class thật.
//
//   import { Member } (không có type): Import cả type lẫn runtime value.
//   Dùng khi cần gọi hàm, tạo instance, hoặc dùng constant.
import api from './axiosInstance';
import type { Member } from '../types/Member';

// ============================================================
// RESPONSE TYPE - Định nghĩa shape của API response
// ============================================================
// Interface export để các file khác (LoginPage) biết type chính xác.
// Đây là "contract" giữa FE và BE — nếu BE thay đổi response shape,
// TypeScript báo lỗi compile ngay tại file này thay vì runtime crash.
export interface LoginResponse {
  token: string;
  expires: string;
  member: Member;
}

// ============================================================
// ARROW FUNCTION + IMPLICIT RETURN
// ============================================================
// Dạng đầy đủ:
//   export function login(username: string): Promise<LoginResponse> {
//     return api.post('/Auth/login', { username }).then((r) => r.data);
//   }
//
// Arrow function + implicit return (concise form):
//   const fn = (param) => expression  // Không có {} → tự động return expression
//
// Interview note: Login request nên luôn có password, kể cả app demo.
// Nếu chỉ dùng username thì ai biết tên người dùng cũng lấy được JWT => auth bị vô hiệu.
export const login = (username: string, password: string): Promise<LoginResponse> =>
  api.post('/Auth/login', { username, password }).then((r) => r.data);

export interface RegisterResponse {
  message: string;
  memberName: string;
}

export const register = (name: string, password: string): Promise<RegisterResponse> =>
  api.post('/Auth/register', { name, password }).then((r) => r.data);

