import api from './axiosInstance';
import type { Member } from '../types/Member';

export interface LoginResponse {
  token: string;
  member: Member;
}

export const login = (username: string): Promise<LoginResponse> =>
  api.post('/Auth/login', { username }).then((r) => r.data);
