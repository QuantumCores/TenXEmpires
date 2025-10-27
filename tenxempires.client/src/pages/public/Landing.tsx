import { Link } from 'react-router-dom'

export default function Landing() {
  return (
    <main className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold">TenX Empires</h1>
      <p className="mt-4 text-slate-600">Welcome. Start or resume your game.</p>
      <div className="mt-6 flex gap-3">
        <Link className="rounded bg-indigo-600 px-4 py-2 text-white" to="/game/current">Play</Link>
        <Link className="rounded border px-4 py-2" to="/about">About</Link>
      </div>
    </main>
  )
}

