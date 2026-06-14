import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from './store';

export function ProtectedRoute() {
  const token = useAuthStore((s) => s.token);
  const user = useAuthStore((s) => s.user);
  const location = useLocation();

  if (!token) return <Navigate to="/login" replace state={{ from: location }} />;

  // Guard profilo incompleto: se comune o partito mancanti, forza /completa-profilo
  const profileIncomplete =
    user !== null && (!user.comune || !user.partito);

  if (profileIncomplete && location.pathname !== '/completa-profilo') {
    return <Navigate to="/completa-profilo" replace />;
  }

  return <Outlet />;
}
