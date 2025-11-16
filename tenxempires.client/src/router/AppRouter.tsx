import {
  createBrowserRouter,
  RouterProvider,
  Navigate,
} from 'react-router-dom'
import { GameCurrentGuardRoute } from '../pages/game/GameCurrentGuardRoute'
import { GameMapPage } from '../pages/game/GameMapPage'
import { GameAuthGuard } from '../pages/game/GameAuthGuard'

const router = createBrowserRouter([
  // Legacy redirect
  { path: '/hub', element: <Navigate to="/game/current" replace /> },

  // Public routes with lazy loading
  { 
    path: '/', 
    lazy: async () => {
      const { default: Landing } = await import('../pages/public/Landing')
      return { Component: Landing }
    }
  },
  { 
    path: '/about', 
    lazy: async () => {
      const { default: About } = await import('../pages/public/About')
      return { Component: About }
    }
  },
  { 
    path: '/gallery', 
    lazy: async () => {
      const { default: Gallery } = await import('../pages/public/Gallery')
      return { Component: Gallery }
    }
  },
  { 
    path: '/privacy', 
    lazy: async () => {
      const { default: Privacy } = await import('../pages/public/Privacy')
      return { Component: Privacy }
    }
  },
  { 
    path: '/cookies', 
    lazy: async () => {
      const { default: Cookies } = await import('../pages/public/Cookies')
      return { Component: Cookies }
    }
  },
  { 
    path: '/login', 
    lazy: async () => {
      const { default: Login } = await import('../pages/public/Login')
      return { Component: Login }
    }
  },
  { 
    path: '/register', 
    lazy: async () => {
      const { default: Register } = await import('../pages/public/Register')
      return { Component: Register }
    }
  },
  { 
    path: '/verify-email',
    lazy: async () => {
      const { default: VerifyEmail } = await import('../pages/public/VerifyEmail')
      return { Component: VerifyEmail }
    }
  },
  {
    path: '/reset-password',
    lazy: async () => {
      const { default: ResetPassword } = await import('../pages/public/ResetPassword')
      return { Component: ResetPassword }
    }
  },
  { 
    path: '/unsupported', 
    lazy: async () => {
      const { default: Unsupported } = await import('../pages/public/Unsupported')
      return { Component: Unsupported }
    }
  },

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
