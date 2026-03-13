import { NavLink } from 'react-router-dom';
import { Upload, LayoutDashboard, ArrowLeftRight, Coins, FileText } from 'lucide-react';

const nav = [
  { to: '/', icon: Upload, label: 'Import' },
  { to: '/dashboard', icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/transactions', icon: ArrowLeftRight, label: 'Transactions' },
  { to: '/dividends', icon: Coins, label: 'Dividends' },
  { to: '/pit38', icon: FileText, label: 'PIT-38 Guide' },
];

export default function Sidebar({ year }: { year: number | null }) {
  return (
    <aside className="no-print group fixed left-0 top-0 h-screen w-16 hover:w-56 bg-slate-950 border-r border-slate-800 z-50 transition-all duration-200 flex flex-col overflow-hidden">
      <div className="flex items-center gap-3 px-4 py-5 border-b border-slate-800 min-h-16">
        <FileText className="w-7 h-7 text-blue-500 shrink-0" />
        <span className="text-white font-semibold text-lg whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity">
          Pitly
        </span>
      </div>

      <nav className="flex-1 py-4 space-y-1">
        {nav.map(({ to, icon: Icon, label }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            className={({ isActive }) =>
              `flex items-center gap-3 px-5 py-2.5 text-sm transition-colors whitespace-nowrap ${
                isActive
                  ? 'text-blue-400 bg-blue-500/10 border-r-2 border-blue-500'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
              }`
            }
          >
            <Icon className="w-5 h-5 shrink-0" />
            <span className="opacity-0 group-hover:opacity-100 transition-opacity">{label}</span>
          </NavLink>
        ))}
      </nav>

      {year && (
        <div className="px-4 py-4 border-t border-slate-800">
          <span className="text-slate-500 text-xs opacity-0 group-hover:opacity-100 transition-opacity whitespace-nowrap">
            Tax year: {year}
          </span>
        </div>
      )}
    </aside>
  );
}
