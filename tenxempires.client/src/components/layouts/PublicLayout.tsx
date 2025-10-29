import type { ReactNode } from 'react'
import { LandingNavbar } from '../landing/LandingNavbar'
import { FooterLinks } from '../landing/FooterLinks'

interface PublicLayoutProps {
  children: ReactNode
  maxWidth?: 'narrow' | 'medium' | 'wide' | 'full'
}

// Centralized background image path
const BACKGROUND_IMAGE = '/images/TenXEmpiresLandingBG.png'

// Centralized dark mode gradient overlay
const DARK_GRADIENT = 'bg-gradient-to-b from-slate-900/85 via-slate-900/75 to-slate-950/85'

export function PublicLayout({ children, maxWidth = 'full' }: PublicLayoutProps): React.JSX.Element {
  const maxWidthClass = {
    narrow: 'max-w-3xl',
    medium: 'max-w-5xl',
    wide: 'max-w-6xl',
    full: 'max-w-7xl'
  }[maxWidth]

  return (
    <main className="relative min-h-screen overflow-x-hidden">
      {/* Background image */}
      <div 
        className="fixed inset-0 -z-10 bg-cover bg-center bg-no-repeat"
        style={{ backgroundImage: `url(${BACKGROUND_IMAGE})` }}
      />
      
      {/* Dark mode gradient overlay */}
      <div className={`fixed inset-0 -z-10 ${DARK_GRADIENT}`} />
      
      <div className={`mx-auto ${maxWidthClass} px-4 sm:px-6 lg:px-8`}>
        <LandingNavbar />
        {children}
        <FooterLinks />
      </div>
    </main>
  )
}

