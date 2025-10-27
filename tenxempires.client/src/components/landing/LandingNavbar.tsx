import { Link, NavLink, useLocation } from 'react-router-dom'

interface Props {
  compact?: boolean
}

export function LandingNavbar({ compact }: Props) {
  const { pathname } = useLocation()
  return (
    <header className={`flex items-center justify-between ${compact ? 'py-2' : 'py-4'}`}>
      <Link
        to="/"
        className="text-lg font-semibold tracking-tight text-slate-900"
        aria-current={pathname === '/' ? 'page' : undefined}
        aria-label="Home: TenX Empires"
      >
        TenX Empires
      </Link>
      <nav aria-label="Primary navigation" className="flex items-center gap-4 text-sm">
        <NavLink to="/about" className={({ isActive }) => isActive ? 'text-slate-900' : 'text-slate-600 hover:text-slate-900'}>About</NavLink>
        <NavLink to="/gallery" className={({ isActive }) => isActive ? 'text-slate-900' : 'text-slate-600 hover:text-slate-900'}>Gallery</NavLink>
        <NavLink to="/privacy" className={({ isActive }) => isActive ? 'text-slate-900' : 'text-slate-600 hover:text-slate-900'}>Privacy</NavLink>
        <NavLink to="/cookies" className={({ isActive }) => isActive ? 'text-slate-900' : 'text-slate-600 hover:text-slate-900'}>Cookies</NavLink>
      </nav>
    </header>
  )
}
