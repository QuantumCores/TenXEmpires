import { useState } from 'react'
import reactLogo from './assets/react.svg'
import viteLogo from '/vite.svg'
import './App.css'

function App() {
  const [count, setCount] = useState(0)
  const [showAnim, setShowAnim] = useState(true)

  return (
    <>
      <div>
        <a href="https://vite.dev" target="_blank" rel="noreferrer">
          <img src={viteLogo} className="logo" alt="Vite logo" />
        </a>
        <a href="https://react.dev" target="_blank" rel="noreferrer">
          <img src={reactLogo} className="logo react" alt="React logo" />
        </a>
      </div>
      <h1 className="mt-6 text-4xl font-extrabold tracking-tight">Vite + React</h1>
      <div className="card">
        <button
          onClick={() => setCount((c) => c + 1)}
          className="inline-flex items-center rounded-lg bg-indigo-600 px-4 py-2 font-medium text-white shadow hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2"
        >
          Count is {count}
        </button>
        <p>
          Edit <code className="font-mono">src/App.tsx</code> and save to test HMR
        </p>
      </div>

      {/* Animation demo: element fades/slides in on mount */}
      <div className="mt-6">
        <div className="mb-3">
          <button
            onClick={() => {
              // Re-run the animation by unmounting and remounting the element
              setShowAnim(false)
              setTimeout(() => setShowAnim(true), 50)
            }}
            className="rounded-md bg-emerald-600 px-3 py-1 text-white hover:bg-emerald-700 focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            Re-run animation
          </button>
        </div>
        {showAnim && (
          <div className="animate-in fade-in slide-in-from-top-8 duration-[1500ms] ease-out">
            <span className="inline-block rounded-md bg-emerald-600 px-3 py-1 text-white">Hello animation</span>
          </div>
        )}
      </div>
      <p className="read-the-docs">
        Click on the Vite and React logos to learn more
      </p>

      {/* Tailwind plugin demos */}
      <section className="mt-8 space-y-8 text-left max-w-lg mx-auto">
        {/* @tailwindcss/forms demo */}
        <div>
          <label htmlFor="name" className="block text-sm font-medium text-slate-700 dark:text-slate-200">
            Your name
          </label>
          <input
            id="name"
            type="text"
            placeholder="Ada Lovelace"
            className="mt-2 block w-full rounded-md border-slate-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
          />
        </div>

        {/* @tailwindcss/typography demo */}
        <article className="prose dark:prose-invert max-w-none">
          <h2>Typography demo</h2>
          <p>
            This paragraph and list are wrapped in <code>prose</code> to showcase the
            typography plugin. Combine it with utilities as needed.
          </p>
          <ul>
            <li>Semantic defaults for text elements</li>
            <li>Nice spacing and readable line lengths</li>
          </ul>
        </article>
      </section>
    </>
  )
}

export default App
