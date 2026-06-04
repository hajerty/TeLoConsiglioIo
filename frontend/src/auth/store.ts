import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { User } from '../api/types';

interface AuthState {
  token: string | null;
  refreshToken: string | null;
  user: User | null;
  login: (data: { accessToken: string; refreshToken: string; user: User }) => void;
  logout: () => void;
  setUser: (u: User) => void;
  setTokens: (data: { accessToken: string; refreshToken: string }) => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      refreshToken: null,
      user: null,
      login: ({ accessToken, refreshToken, user }) =>
        set({ token: accessToken, refreshToken, user }),
      logout: () => set({ token: null, refreshToken: null, user: null }),
      setUser: (u) => set({ user: u }),
      setTokens: ({ accessToken, refreshToken }) =>
        set({ token: accessToken, refreshToken }),
    }),
    { name: 'tlc-auth' }
  )
);
