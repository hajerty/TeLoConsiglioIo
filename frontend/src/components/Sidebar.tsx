import { useEffect } from 'react';
import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  User,
  FileText,
  ScrollText,
  Archive,
  CalendarDays,
  UserPlus,
  X,
} from 'lucide-react';
import { useAuthStore } from '../auth/store';

const ADMIN_ROLES = ['Admin', 'Capogruppo', 'Vice'];

const baseItems = [
  { to: '/', label: 'Dashboard', Icon: LayoutDashboard },
  { to: '/profilo', label: 'Profilo politico', Icon: User },
  { to: '/documenti', label: 'Documenti', Icon: FileText },
  { to: '/atti', label: 'Atti', Icon: ScrollText },
  { to: '/archivio', label: 'Archivio', Icon: Archive },
  { to: '/sedute', label: 'Sedute', Icon: CalendarDays },
];

interface SidebarProps {
  isOpen: boolean;
  onClose: () => void;
}

export function Sidebar({ isOpen, onClose }: SidebarProps) {
  const user = useAuthStore((s) => s.user);
  const canManage = user?.roles?.some((r) => ADMIN_ROLES.includes(r)) ?? false;

  const items = canManage
    ? [...baseItems, { to: '/consiglieri', label: 'Gestione consiglieri', Icon: UserPlus }]
    : baseItems;

  // Close drawer on desktop resize
  useEffect(() => {
    const handler = () => {
      if (window.innerWidth >= 768) onClose();
    };
    window.addEventListener('resize', handler);
    return () => window.removeEventListener('resize', handler);
  }, [onClose]);

  const sidebarContent = (
    <aside className="w-64 bg-slate-900 text-slate-100 flex flex-col flex-shrink-0 h-full">
      <div className="p-5 border-b border-slate-800 flex items-center justify-between">
        <div>
          <div className="text-lg font-semibold">TeLoConsiglio</div>
          <div className="text-xs text-slate-400">Assistente del consigliere</div>
        </div>
        {/* Close button — visible only on mobile */}
        <button
          className="md:hidden text-slate-400 hover:text-slate-100 p-1 min-w-[44px] min-h-[44px] flex items-center justify-center"
          onClick={onClose}
          aria-label="Chiudi menu"
        >
          <X className="w-5 h-5" />
        </button>
      </div>
      <nav className="flex-1 p-3 space-y-1">
        {items.map((it) => (
          <NavLink
            key={it.to}
            to={it.to}
            end={it.to === '/'}
            onClick={onClose}
            className={({ isActive }) =>
              `flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium transition min-h-[44px] ${
                isActive ? 'bg-brand-600 text-white' : 'text-slate-300 hover:bg-slate-800'
              }`
            }
          >
            <it.Icon className="w-4 h-4 flex-shrink-0" />
            {it.label}
          </NavLink>
        ))}
      </nav>
      <div className="p-3 text-xs text-slate-500 border-t border-slate-800">v0.1 - dev</div>
    </aside>
  );

  return (
    <>
      {/* Desktop: always visible */}
      <div className="hidden md:flex">{sidebarContent}</div>

      {/* Mobile: drawer with overlay */}
      {isOpen && (
        <div className="md:hidden fixed inset-0 z-50 flex">
          {/* Overlay */}
          <div
            className="absolute inset-0 bg-black/50"
            onClick={onClose}
            aria-hidden="true"
          />
          {/* Drawer slides in from left */}
          <div className="relative flex animate-slide-in-left">
            {sidebarContent}
          </div>
        </div>
      )}
    </>
  );
}
