import { Outlet, useNavigate } from 'react-router-dom';
import { Sidebar } from './Sidebar';
import { useAuthStore } from '../auth/store';

export function Layout() {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const navigate = useNavigate();

  const doLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar />
      <div className="flex-1 flex flex-col overflow-hidden">
        <header className="h-14 border-b border-slate-200 bg-white flex items-center justify-between px-6">
          <div className="text-sm text-slate-500">Benvenuto</div>
          <div className="flex items-center gap-4">
            <div className="text-sm">
              <span className="font-medium text-slate-900">{user?.fullName || user?.email}</span>
              {user?.comune && <span className="text-slate-500"> - {user.comune}</span>}
            </div>
            <button onClick={doLogout} className="btn-secondary text-xs">
              Esci
            </button>
          </div>
        </header>
        <main className="flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
