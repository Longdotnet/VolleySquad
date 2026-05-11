// ============================================================
// MEMBER TYPES - Map với backend Domain/Member.cs
// ============================================================
// TypeScript interfaces là "structural typing" (duck typing):
//   Nếu object có đủ các field, nó thỏa mãn interface — không cần khai báo explicit.
//   Khác với C# là "nominal typing" (class phải implements interface tường minh).

export type MemberRole = 'Admin' | 'Member';

// Map với Domain/Member.cs — full entity
export interface Member {
  id: string;           // Guid → string
  name: string;
  skillPoint: number;   // int, 1-100 (enforced bởi domain methods)
  balance: number;      // decimal → number (VNĐ, 2 decimal places)
  role: MemberRole;
}

// Subset dùng trong Leaderboard — không cần balance (sensitive)
export interface LeaderboardEntry {
  rank: number;         // computed ở frontend (index + 1)
  id: string;
  name: string;
  skillPoint: number;
  role: MemberRole;
}

// DTO khi Admin tạo member mới — không có id (server tự generate)
export interface CreateMemberRequest {
  name: string;
  skillPoint: number;
  balance: number;
  role: MemberRole;
}

// Helper để tính rank từ list (thay vì computed ở backend)
export const toLeaderboard = (members: Member[]): LeaderboardEntry[] =>
  [...members]
    .sort((a, b) => b.skillPoint - a.skillPoint)
    .map((m, idx) => ({ rank: idx + 1, ...m }));
