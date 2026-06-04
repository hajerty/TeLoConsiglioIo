import { NavLink } from 'react-router-dom';

const items = [
  { to: '/', label: 'Dashboard', icon: 'D' },
  { to: '/profilo', label: 'Profilo politico', icon: 'P' },
  { to: '/documenti', label: 'Documenti', icon: 'F' },
  { to: '/atti', label: 'Atti', icon: 'A' },
  { to: '/archivio', label: 'Archivio', icon: 'R' },
  { to: '/sedute', label: 'Sedute', icon: 'S' },
];

export function Sidebar() {
  return (
    <aside className="w-64 bg-slate-900 text-slate-100 flex flex-col flex-shrink-0">
      <div className="p-5 border-b border-slate-800">
        <div className="text-lg font-semibold">TeLoConsiglio</div>
        <div className="text-xs text-slate-400">Assistente del consigliere</div>
      </div>
      <nav className="flex-1 p-3 space-y-1">
        {items.map((it) => (
          <NavLink
            key={it.to}
            to={it.to}
            end={it.to === '/'}
            className={({ isActive }) =>
              `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition ${
                isActive ? 'bg-brand-600 text-white' : 'text-slate-300 hover:bg-slate-800'
              }`
            }
          >
            <span className="w-6 h-6 inline-flex items-center justify-center rounded bg-slate-700 text-xs font-bold">
              {it.icon}
            </span>
            {it.label}
          </NavLink>
        ))}
      </nav>
      <div className="p-3 text-xs text-slate-500 border-t border-slate-800">v0.1 - dev</div>
    </aside>
  );
}
