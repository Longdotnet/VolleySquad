// ============================================================
// MATCH TYPES - Map với backend Domain entities
// ============================================================
// Mỗi interface ở đây phải KHỚP với C# class tương ứng ở backend.
// Khi backend thêm field mới → cập nhật interface này → TypeScript compiler
// sẽ báo lỗi tại mọi nơi dùng interface nếu chưa handle field mới.
// Đây là lợi ích của TypeScript: "type-safe contract" giữa FE và BE.

// Map với Domain/Enums/MatchStatus.cs
// TypeScript union type thay vì enum để dễ dùng hơn với JSON response.
// "Upcoming" | "InProgress" | ... đảm bảo chỉ những giá trị hợp lệ được dùng.
export type MatchStatus = 'Upcoming' | 'InProgress' | 'Finished' | 'Settled' | 'Cancelled';

// Map với Domain/Match.cs
export interface Match {
  id: string;           // Guid → string (JSON serialization)
  playDate: string;     // DateTime UTC → ISO string, frontend convert sang +7

  location: string;
  maxSlots: number;     // Default 18

  registeredMemberIds: string[];   // List<Guid> stored as JSON in DB

  // State Machine - map với MatchStatus enum ở backend
  status: MatchStatus;

  // Settlement info - populated sau khi Admin gọi FinalizeMatch
  isSettled: boolean;
  feePerPerson: number; // decimal → number (VNĐ)
}

// Computed helpers - KHÔNG có ở backend, tính ở frontend
export const getAvailableSlots = (match: Match): number =>
  match.maxSlots - match.registeredMemberIds.length;

export const isMatchOpen = (match: Match): boolean =>
  match.status === 'Upcoming' && getAvailableSlots(match) > 0;

// Map với Domain/ValueObjects/TeamResult.cs
export interface TeamResult {
  teamA: TeamMember[];
  teamB: TeamMember[];
  teamC: TeamMember[];
  // Metrics từ backend (computed properties trong TeamResult record)
  teamATotalSkill: number;
  teamBTotalSkill: number;
  teamCTotalSkill: number;
  maxSkillDifference: number;
  isBalanced: boolean;
}

// Map với Member trong context của team (subset của Member đầy đủ)
export interface TeamMember {
  id: string;
  name: string;
  skillPoint: number;
  role: 'Admin' | 'Member';
}

// Map với Domain/SlotTransfer.cs
export type SlotTransferStatus = 'Pending' | 'Completed' | 'Cancelled';

export interface SlotTransfer {
  id: string;
  matchId: string;
  fromMemberId: string;
  toMemberId: string;
  status: SlotTransferStatus;
  createdAt: string;     // DateTime UTC → ISO string
  resolvedAt: string | null;  // nullable DateTime → string | null
}
