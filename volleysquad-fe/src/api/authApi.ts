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
// Tại sao chỉ truyền username, không có password?
// App này demo kiến trúc JWT + Event-Driven, không có Password trong DB.
// Real app: Gửi { username, password }, BE hash password và so sánh với DB.
// Hashing: bcrypt (cost factor 12+) hoặc Argon2id (khuyên dùng nhất hiện tại).
export const login = (username: string): Promise<LoginResponse> =>
  api.post('/Auth/login', { username }).then((r) => r.data);

