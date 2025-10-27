import { Link } from 'react-router-dom'

export function FooterLinks() {
  return (
    <footer className="mx-auto mt-16 max-w-6xl border-t border-slate-200 px-4 py-6 text-sm text-slate-600">
      <div className="flex items-center justify-between">
        <div>© {new Date().getFullYear()} TenX Empires</div>
        <div className="flex items-center gap-4">
          <Link to="/privacy" className="hover:text-slate-900">Privacy</Link>
          <Link to="/cookies" className="hover:text-slate-900">Cookies</Link>
        </div>
      </div>
    </footer>
  )
}

