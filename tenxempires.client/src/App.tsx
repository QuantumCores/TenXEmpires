import './App.css'
import { AppRouter } from './router/AppRouter'
import { AppProviders } from './providers/AppProviders'

function App() {
  return (
    <AppProviders>
      <AppRouter />
    </AppProviders>
  )
}

export default App
