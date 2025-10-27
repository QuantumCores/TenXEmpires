import { useAuthStatusQuery } from '../../features/auth/useAuthStatusQuery'
import { LandingHero } from '../../components/landing/LandingHero'
import { AuthAwareCTA } from '../../components/landing/AuthAwareCTA'
import { LandingNavbar } from '../../components/landing/LandingNavbar'
import { LandingHighlights } from '../../components/landing/LandingHighlights'
import { FooterLinks } from '../../components/landing/FooterLinks'
import { ConsentBanner } from '../../components/landing/ConsentBanner'
import { Banners } from '../../components/ui/Banners'

export default function Landing() {
  const { data: auth } = useAuthStatusQuery()

  return (
    <main className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8">
      <LandingNavbar />
      <LandingHero cta={<AuthAwareCTA auth={auth} />} />
      <LandingHighlights />
      <FooterLinks />
      <ConsentBanner />
      <Banners />
    </main>
  )
}
