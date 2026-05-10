import api from './axiosInstance';
import type { Match, Team } from '../types/Match';
import type { Member } from '../types/Member';

export const getMembers = (): Promise<Member[]> =>
  api.get('/match/members').then((r) => r.data);

export const getMatches = (): Promise<Match[]> =>
  api.get('/match').then((r) => r.data);

export const registerSlot = (matchId: string): Promise<void> =>
  api.post(`/match/${matchId}/register`).then((r) => r.data);

export const splitTeams = (): Promise<Team[]> =>
  api.post('/match/split-from-db').then((r) => r.data);
