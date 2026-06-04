import axios from 'axios';
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

api.interceptors.response.use(
  (r) => r,
  (err) => {
    if (err?.response?.status === 401) {
      useAuthStore.getState().logout();
    }
    return Promise.reject(err);
  }
);
