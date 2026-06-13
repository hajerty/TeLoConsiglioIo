import { useState } from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import { LogOut, Menu } from 'lucide-react';
import { Sidebar } from './Sidebar';
import { useAuthStore } from '../auth/store';

export function Layout() {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  const doLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      <div className="flex-1 flex flex-col overflow-hidden min-w-0">
        <header className="h-14 border-b border-slate-200 bg-white flex items-center justify-between px-4 md:px-6 flex-shrink-0">
          <div className="flex items-center gap-3">
            {/* Hamburger — visible only on mobile */}
            <button
              className="md:hidden p-2 min-w-[44px] min-h-[44px] flex items-center justify-center text-slate-600 hover:text-slate-900 hover:bg-slate-100 rounded-md"
              onClick={() => setSidebarOpen(true)}
              aria-label="Apri menu"
            >
              <Menu className="w-5 h-5" />
            </button>
            <div className="text-sm text-slate-500 hidden md:block">Benvenuto</div>
          </div>
          <div className="flex items-center gap-3">
            <div className="text-sm hidden sm:block">
              <span className="font-medium text-slate-900">{user?.fullName || user?.email}</span>
              {user?.comune && <span className="text-slate-500"> - {user.comune}</span>}
            </div>
            <button
              onClick={doLogout}
              className="btn-secondary text-xs flex items-center gap-1.5 min-h-[44px] px-3"
              aria-label="Esci"
            >
              <LogOut className="w-4 h-4" />
              <span className="hidden sm:inline">Esci</span>
            </button>
          </div>
        </header>
        <main className="flex-1 overflow-y-auto p-4 md:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
