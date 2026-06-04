import axios from 'axios';
import type { AxiosError, AxiosRequestConfig } from 'axios';
import { useAuthStore } from '../auth/store';

const baseURL = (import.meta.env.VITE_API_URL as string | undefined) || 'http://localhost:5000';

export const api = axios.create({ baseURL });

api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;
  if (token) {
    config.headers = config.headers ?? {};
    (config.headers as Record<string, string>).Authorization = `Bearer ${token}`;
  }
  return config;
});

interface RefreshResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
}

let refreshPromise: Promise<RefreshResponse> | null = null;

async function performRefresh(refreshToken: string): Promise<RefreshResponse> {
  // bare axios call (no interceptor) to avoid infinite recursion
  const res = await axios.post<RefreshResponse>(`${baseURL}/api/auth/refresh`, { refreshToken });
  return res.data;
}

api.interceptors.response.use(
  (r) => r,
  async (err: AxiosError) => {
    const original = err.config as (AxiosRequestConfig & { _retried?: boolean }) | undefined;
    const status = err.response?.status;
    const url = original?.url ?? '';

    // Do not try to refresh for the refresh/login/register endpoints themselves
    const isAuthEndpoint =
      url.includes('/api/auth/refresh') ||
      url.includes('/api/auth/login') ||
      url.includes('/api/auth/register');

    if (status === 401 && original && !original._retried && !isAuthEndpoint) {
      const state = useAuthStore.getState();
      const rt = state.refreshToken;
      if (!rt) {
        state.logout();
        return Promise.reject(err);
      }
      try {
        if (!refreshPromise) {
          refreshPromise = performRefresh(rt).finally(() => {
            // small delay before clearing so concurrent 401s pick up the same promise
            setTimeout(() => {
              refreshPromise = null;
            }, 0);
          });
        }
        const fresh = await refreshPromise;
        useAuthStore.getState().setTokens({
          accessToken: fresh.accessToken,
          refreshToken: fresh.refreshToken,
        });
        original._retried = true;
        original.headers = original.headers ?? {};
        (original.headers as Record<string, string>).Authorization = `Bearer ${fresh.accessToken}`;
        return api.request(original);
      } catch (refreshErr) {
        useAuthStore.getState().logout();
        return Promise.reject(refreshErr);
      }
    }

    return Promise.reject(err);
  }
);
