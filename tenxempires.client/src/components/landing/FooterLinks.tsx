import { Link } from 'react-router-dom'

export function FooterLinks(): React.JSX.Element {
  return (
    <footer className="mt-16 border-t border-slate-700/60 py-6 text-sm text-slate-400">
      <div className="flex items-center justify-between">
        <div>© {new Date().getFullYear()} TenX Empires</div>
        <div className="flex items-center gap-4">
          <Link to="/privacy" className="hover:text-slate-100">Privacy</Link>
          <Link to="/cookies" className="hover:text-slate-100">Cookies</Link>
        </div>
      </div>
    </footer>
  )
}

