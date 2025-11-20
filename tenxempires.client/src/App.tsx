import './App.css'
import { AppRouter } from './router/AppRouter'
import { AppProviders } from './providers/AppProviders'

function App(): React.JSX.Element {
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '(not set)'
  const showApiBanner = import.meta.env.VITE_SHOW_API_BASE_URL === 'false'

  return (
    <AppProviders>
      {showApiBanner && (
        <div
          style={{
            padding: '0.25rem 0.75rem',
            backgroundColor: '#fef3c7',
            color: '#92400e',
            fontSize: '0.85rem',
            textAlign: 'center',
          }}
        >
          API base URL: {apiBaseUrl}
        </div>
      )}
      <AppRouter />
    </AppProviders>
  )
}

export default App
