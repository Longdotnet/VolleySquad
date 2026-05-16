import api from './axiosInstance';
import type { Match, Team, TeamMember } from '../types/Match';
import type { Member } from '../types/Member';

// ─── Paged response wrapper (mirrors backend PagedResult<T>) ─────────────────
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ─── Members ──────────────────────────────────────────────────────────────────

/** Admin: lấy danh sách thành viên có phân trang */
export const getMembersPaged = (
  page = 1,
  pageSize = 20,
  search = ''
): Promise<PagedResult<Member>> =>
  api.get('/match/members', { params: { page, pageSize, search: search || undefined } })
     .then((r) => r.data);

/** Admin: thêm thành viên mới */
export const addMember = (data: {
  name: string;
  skillPoint: number;
  balance: number;
  role: string;
  password: string;
}): Promise<Member> =>
  api.post('/match/add-member', data).then((r) => r.data);

/** Admin: cập nhật thông tin thành viên */
export const updateMember = (
  id: string,
  data: { name?: string; skillPoint: number; balance: number }
): Promise<Member> =>
  api.put(`/match/update-member/${id}`, data).then((r) => r.data);

/** Admin: xoá thành viên */
export const deleteMember = (id: string): Promise<void> =>
  api.delete(`/match/delete-member/${id}`).then(() => undefined);

// ─── Matches ──────────────────────────────────────────────────────────────────

/** Lấy danh sách trận đấu có phân trang (dùng cho history table) */
export const getMatchesPaged = (
  page = 1,
  pageSize = 10,
  status = ''
): Promise<PagedResult<Match>> =>
  api.get('/match/all-matches', { params: { page, pageSize, status: status || undefined } })
     .then((r) => r.data);

/** Lấy tất cả trận — dùng nội bộ để tìm trận upcoming/inprogress */
export const getMatches = (): Promise<Match[]> =>
  api.get('/match/all-matches', { params: { page: 1, pageSize: 100 } })
     .then((r) => (r.data as PagedResult<Match>).items);

/** Lấy danh sách thành viên (toàn bộ) — dùng cho Leaderboard, RegisteredPlayers */
export const getMembers = (): Promise<Member[]> =>
  api.get('/match/members', { params: { page: 1, pageSize: 200 } })
     .then((r) => (r.data as PagedResult<Member>).items);

/** Đăng ký slot — backend tự lấy memberId từ JWT claim */
export const registerSlot = (matchId: string): Promise<void> =>
  api.post(`/match/register-slot/${matchId}`).then((r) => r.data);

/** Admin: tạo trận mới */
export const createMatch = (data: {
  playDate: string;
  location: string;
  maxSlots: number;
}): Promise<Match> =>
  api.post('/match/create-match', data).then((r) => r.data);

/** Admin: cập nhật trận đấu */
export const updateMatch = (
  id: string,
  data: { playDate: string; location: string; maxSlots: number; feePerPerson?: number }
): Promise<Match> =>
  api.put(`/match/update-match/${id}`, data).then((r) => r.data);

/** Admin: xóa trận đấu */
export const deleteMatch = (id: string): Promise<void> =>
  api.delete(`/match/delete-match/${id}`).then(() => undefined);

/** Member: rời khỏi trận (chỉ trận Upcoming) */
export const leaveSlot = (matchId: string): Promise<void> =>
  api.post(`/match/leave-slot/${matchId}`).then(() => undefined);

// ─── Current user ─────────────────────────────────────────────────────────────

/** Lấy thông tin thành viên hiện tại (dựa trên JWT) */
export const getMe = (): Promise<Member> =>
  api.get('/user/me').then((r) => r.data);

// ─── Leaderboard ──────────────────────────────────────────────────────────────

/** Lấy bảng xếp hạng thành viên (mọi role đã login) */
export const getLeaderboard = (): Promise<Member[]> =>
  api.get('/match/leaderboard').then((r) => r.data);

// ─── Team split ───────────────────────────────────────────────────────────────

/** Chia đội bằng Snake Draft algorithm (Admin only) */
export const splitTeams = (): Promise<Team[]> =>
  api.post('/match/split-from-db').then((r) => {
    const data = r.data as { TeamA: TeamMember[]; TeamB: TeamMember[]; TeamC: TeamMember[] };
    const toTeam = (name: string, members: TeamMember[]): Team => ({
      teamName: name,
      members,
      totalSkillPoint: members.reduce((sum, m) => sum + m.skillPoint, 0),
    });
    return [toTeam('Đội A', data.TeamA), toTeam('Đội B', data.TeamB), toTeam('Đội C', data.TeamC)];
  });
