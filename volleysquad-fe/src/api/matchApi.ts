// ============================================================
// MATCH API - Lớp abstraction cho HTTP calls liên quan đến Match
// ============================================================
//
// TẠI SAO tách API calls vào file riêng thay vì gọi trực tiếp trong component?
//   Nếu gọi trực tiếp trong component:
//     const res = await api.get('/match/members')  // trong DashboardPage.tsx
//
//   Vấn đề:
//     1. Nếu URL thay đổi → phải sửa ở tất cả component dùng nó
//     2. Component chứa cả UI logic và data-fetching logic → khó test, khó đọc
//     3. Duplicate code: nhiều component cùng gọi cùng 1 endpoint
//
//   API layer giải quyết (Separation of Concerns):
//     - Component chỉ biết gọi getMembers(), không cần biết URL hay method
//     - Thay đổi URL/header → chỉ sửa 1 chỗ ở đây
//     - Dễ mock khi viết unit test: jest.mock('../api/matchApi')
//
// ============================================================
// TYPESCRIPT GENERICS trong Promise
// ============================================================
// Promise<Member[]> = hàm trả về Promise chứa mảng Member khi resolve.
// TypeScript tự kiểm tra: nếu API thay đổi shape → compile error ngay lập tức.
// Khác JavaScript: JS chỉ biết lỗi lúc runtime.
import api from './axiosInstance';
import type { Match, Team, TeamMember } from '../types/Match';
import type { Member } from '../types/Member';

// Lấy danh sách thành viên (Admin only — backend sẽ từ chối nếu không phải Admin)
export const getMembers = (): Promise<Member[]> =>
  api.get('/match/members').then((r) => r.data);

// Lấy tất cả trận đấu
export const getMatches = (): Promise<Match[]> =>
  api.get('/match/all-matches').then((r) => r.data);

// Đăng ký slot — backend tự lấy memberId từ JWT claim, không cần truyền
export const registerSlot = (matchId: string): Promise<void> =>
  api.post(`/match/register-slot/${matchId}`).then((r) => r.data);

// Chia đội bằng Snake Draft algorithm (Admin only)
// Trả về Team[] — mỗi Team có teamName, members, totalSkillPoint
export const splitTeams = (): Promise<Team[]> =>
  api.post('/match/split-from-db').then((r) => {
    // Backend trả { TeamA: [...], TeamB: [...], TeamC: [...] }
    // FE cần mảng để render vòng lặp → map thành Team[] với tên đội
    const data = r.data as { TeamA: TeamMember[]; TeamB: TeamMember[]; TeamC: TeamMember[] };
    const toTeam = (name: string, members: TeamMember[]): Team => ({
      teamName: name,
      members,
      totalSkillPoint: members.reduce((sum, m) => sum + m.skillPoint, 0),
    });
    return [toTeam('Đội A', data.TeamA), toTeam('Đội B', data.TeamB), toTeam('Đội C', data.TeamC)];
  });

