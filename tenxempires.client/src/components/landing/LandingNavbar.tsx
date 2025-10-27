import { Link, NavLink, useLocation } from 'react-router-dom'

interface Props {
  compact?: boolean
}

export function LandingNavbar({ compact }: Props) {
  const { pathname } = useLocation()
  return (
    <header className={`sticky top-0 z-30 ${compact ? 'py-2' : 'py-4'}`}>
      <div className="flex items-center justify-between rounded-xl border border-slate-700/60 bg-slate-900/70 px-4 py-2 shadow-lg backdrop-blur supports-[backdrop-filter]:bg-slate-900/50">
        <Link
          to="/"
          className="text-base font-semibold tracking-tight text-white"
          aria-current={pathname === '/' ? 'page' : undefined}
          aria-label="Home: TenX Empires"
        >
          <span className="bg-gradient-to-r from-indigo-400 to-violet-400 bg-clip-text text-transparent">TenX</span> Empires
        </Link>
        <nav aria-label="Primary navigation" className="flex items-center gap-1 text-sm">
          <NavLink to="/about" className={({ isActive }) => `rounded-md px-3 py-1.5 ${isActive ? 'bg-indigo-600 text-white' : 'text-slate-300 hover:bg-slate-800'}`}>About</NavLink>
          <NavLink to="/gallery" className={({ isActive }) => `rounded-md px-3 py-1.5 ${isActive ? 'bg-indigo-600 text-white' : 'text-slate-300 hover:bg-slate-800'}`}>Gallery</NavLink>
          <NavLink to="/privacy" className={({ isActive }) => `rounded-md px-3 py-1.5 ${isActive ? 'bg-indigo-600 text-white' : 'text-slate-300 hover:bg-slate-800'}`}>Privacy</NavLink>
          <NavLink to="/cookies" className={({ isActive }) => `rounded-md px-3 py-1.5 ${isActive ? 'bg-indigo-600 text-white' : 'text-slate-300 hover:bg-slate-800'}`}>Cookies</NavLink>
        </nav>
      </div>
    </header>
  )
}
