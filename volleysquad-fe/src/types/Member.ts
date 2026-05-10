// types/Member.ts — map với Member.cs ở backend
export interface Member {
  id: string;        // Guid
  name: string;
  skillPoint: number; // 1-100
  balance: number;   // decimal
  role: 'Admin' | 'Member';
}

// types/Match.ts — map với Match.cs
export interface Match {
  id: string;
  playDate: string;  // ISO datetime
  location: string;
  maxSlots: number;  // default 18
  registeredMemberIds: string[];
}