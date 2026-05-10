import { create } from 'zustand';
import type { Member } from '../types/Member';

interface AuthState {
  token: string | null;
  user: Member | null;
  setAuth: (token: string, user: Member) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  token: localStorage.getItem('token'),
  user: null,
  setAuth: (token, user) => {
    localStorage.setItem('token', token);
    set({ token, user });
  },
  logout: () => {
    localStorage.removeItem('token');
    set({ token: null, user: null });
  },
}));
