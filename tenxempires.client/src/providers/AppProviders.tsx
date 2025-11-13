import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useMemo } from 'react'

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
      {children}
    </QueryClientProvider>
  )
}
