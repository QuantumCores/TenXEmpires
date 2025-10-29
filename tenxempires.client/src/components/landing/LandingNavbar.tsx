import { Link, NavLink, useLocation } from 'react-router-dom'

export function LandingNavbar(): React.JSX.Element {
  const { pathname } = useLocation()
  return (
    <header className="sticky top-0 z-30 py-4">
      <div className="flex min-h-[3.5rem] items-center justify-between rounded-xl border border-slate-700/60 bg-slate-900/70 px-4 py-2 shadow-lg backdrop-blur supports-[backdrop-filter]:bg-slate-900/50">
        <Link
          to="/"
          className="shrink-0 text-base font-semibold tracking-tight text-white"
          aria-current={pathname === '/' ? 'page' : undefined}
          aria-label="Home: TenX Empires"
        >
          <span className="bg-gradient-to-r from-indigo-400 to-violet-400 bg-clip-text text-transparent">TenX</span> Empires
        </Link>
        <nav aria-label="Primary navigation" className="flex shrink-0 items-center gap-1 text-sm">
          <NavLink 
            to="/about" 
            className={({ isActive }): string => `whitespace-nowrap rounded-md px-3 py-1.5 ${isActive ? 'bg-indigo-600 text-white' : 'text-slate-300 hover:bg-slate-800'}`}
          >
            About
          </NavLink>
          <NavLink 
            to="/gallery" 
            className={({ isActive }): string => `whitespace-nowrap rounded-md px-3 py-1.5 ${isActive ? 'bg-indigo-600 text-white' : 'text-slate-300 hover:bg-slate-800'}`}
          >
            Gallery
          </NavLink>
          <NavLink 
            to="/privacy" 
            className={({ isActive }): string => `whitespace-nowrap rounded-md px-3 py-1.5 ${isActive ? 'bg-indigo-600 text-white' : 'text-slate-300 hover:bg-slate-800'}`}
          >
            Privacy
          </NavLink>
          <NavLink 
            to="/cookies" 
            className={({ isActive }): string => `whitespace-nowrap rounded-md px-3 py-1.5 ${isActive ? 'bg-indigo-600 text-white' : 'text-slate-300 hover:bg-slate-800'}`}
          >
            Cookies
          </NavLink>
        </nav>
      </div>
    </header>
  )
}
