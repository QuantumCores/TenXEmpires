import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useMemo } from 'react'
import { CsrfProvider } from './CsrfProvider'

export function AppProviders({ children }: { children: React.ReactNode }) {
  const client = useMemo(() => new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 60_000,
        retry: 0,
        refetchOnWindowFocus: false,
      },
      mutations: {
        retry: 0,
      },
    },
  }), [])

  return (
    <QueryClientProvider client={client}>
      <CsrfProvider>
        {children}
      </CsrfProvider>
    </QueryClientProvider>
  )
}
