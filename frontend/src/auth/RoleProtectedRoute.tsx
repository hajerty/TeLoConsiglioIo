import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from './store';

interface Props {
  allowedRoles: string[];
  redirectTo?: string;
}

export function RoleProtectedRoute({ allowedRoles, redirectTo = '/dashboard' }: Props) {
  const user = useAuthStore((s) => s.user);
  const hasRole = user?.roles?.some((r) => allowedRoles.includes(r)) ?? false;
  if (!hasRole) return <Navigate to={redirectTo} replace />;
  return <Outlet />;
}
