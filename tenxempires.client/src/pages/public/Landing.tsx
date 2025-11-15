import { useAuthStatusQuery } from '../../features/auth/useAuthStatusQuery'
import { PublicLayout } from '../../components/layouts/PublicLayout'
import { LandingIntro } from '../../components/landing/LandingIntro'
import { AuthAwareCTA } from '../../components/landing/AuthAwareCTA'
import { LandingHighlights } from '../../components/landing/LandingHighlights'
import { ConsentBanner } from '../../components/landing/ConsentBanner'
import { Banners } from '../../components/ui/Banners'

export default function Landing(): React.JSX.Element {
  const { data: authResult } = useAuthStatusQuery()
  const auth = authResult?.auth

  return (
    <PublicLayout>
      <LandingIntro cta={<AuthAwareCTA auth={auth} />} />
      <LandingHighlights />
      <ConsentBanner />
      <Banners />
    </PublicLayout>
  )
}
