import { lazy, Suspense } from 'react'
import {
  createBrowserRouter,
  RouterProvider,
  Navigate,
} from 'react-router-dom'
import { GameCurrentGuardRoute } from '../pages/game/GameCurrentGuardRoute'
import { GameMapPage } from '../pages/game/GameMapPage'
import { GameAuthGuard } from '../pages/game/GameAuthGuard'

// Public pages (lightweight stubs for now)
const Landing = lazy(() => import('../pages/public/Landing'))
const About = lazy(() => import('../pages/public/About'))
const Gallery = lazy(() => import('../pages/public/Gallery'))
const Privacy = lazy(() => import('../pages/public/Privacy'))
const Cookies = lazy(() => import('../pages/public/Cookies'))
const Login = lazy(() => import('../pages/public/Login'))
const Register = lazy(() => import('../pages/public/Register'))
const Unsupported = lazy(() => import('../pages/public/Unsupported'))

const router = createBrowserRouter([
  // Legacy redirect
  { path: '/hub', element: <Navigate to="/game/current" replace /> },

  // Public routes
  { path: '/', element: <Suspense fallback={null}><Landing /></Suspense> },
  { path: '/about', element: <Suspense fallback={null}><About /></Suspense> },
  { path: '/gallery', element: <Suspense fallback={null}><Gallery /></Suspense> },
  { path: '/privacy', element: <Suspense fallback={null}><Privacy /></Suspense> },
  { path: '/cookies', element: <Suspense fallback={null}><Cookies /></Suspense> },
  { path: '/login', element: <Suspense fallback={null}><Login /></Suspense> },
  { path: '/register', element: <Suspense fallback={null}><Register /></Suspense> },
  { path: '/unsupported', element: <Suspense fallback={null}><Unsupported /></Suspense> },

  // Guarded route for current game
  { path: '/game/current', element: <GameCurrentGuardRoute /> },

  // Game map page
  { path: '/game/:id', element: (
    <GameAuthGuard>
      <GameMapPage />
    </GameAuthGuard>
  ) },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}
