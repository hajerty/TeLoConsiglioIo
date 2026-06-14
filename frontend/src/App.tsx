import { useEffect } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from 'react-router-dom';
import { router } from './router';
import { useAuthStore } from './auth/store';
import { authApi } from './api/endpoints';

const qc = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
});

function AppInner() {
  const token = useAuthStore((s) => s.token);
  const user = useAuthStore((s) => s.user);
  const setUser = useAuthStore((s) => s.setUser);
  const logout = useAuthStore((s) => s.logout);

  useEffect(() => {
    // Se c'è un token ma l'utente non è caricato (es. dopo OAuth callback o hard refresh),
    // recupera i dati utente dalla me
    if (token && !user) {
      authApi
        .me()
        .then((u) => setUser(u))
        .catch(() => logout());
    }
  }, [token, user, setUser, logout]);

  return <RouterProvider router={router} />;
}

export default function App() {
  return (
    <QueryClientProvider client={qc}>
      <AppInner />
    </QueryClientProvider>
  );
}
