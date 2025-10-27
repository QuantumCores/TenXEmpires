import { useEffect, useLayoutEffect, useRef } from 'react'
import { createPortal } from 'react-dom'

export interface ModalContainerProps {
  titleId?: string
  onRequestClose: () => void
  initialFocusRef?: React.RefObject<HTMLElement>
  closeOnBackdrop?: boolean
  children: React.ReactNode
}

function getFocusable(container: HTMLElement): HTMLElement[] {
  const selectors = [
    'a[href]',
    'area[href]',
    'input:not([disabled]):not([type="hidden"])',
    'select:not([disabled])',
    'textarea:not([disabled])',
    'button:not([disabled])',
    'iframe',
    'object',
    'embed',
    '[tabindex]:not([tabindex="-1"])',
    '[contenteditable="true"]',
  ]
  const nodes = Array.from(container.querySelectorAll<HTMLElement>(selectors.join(',')))
  return nodes.filter((el) => !el.hasAttribute('disabled') && el.tabIndex !== -1 && isVisible(el))
}

function isVisible(el: HTMLElement) {
  const style = window.getComputedStyle(el)
  return style.visibility !== 'hidden' && style.display !== 'none'
}

export function ModalContainer({ titleId, onRequestClose, initialFocusRef, closeOnBackdrop = true, children }: ModalContainerProps) {
  const overlayRef = useRef<HTMLDivElement | null>(null)
  const dialogRef = useRef<HTMLDivElement | null>(null)
  const prevActiveEl = useRef<HTMLElement | null>(null)

  // Scroll lock and background aria-hidden
  useLayoutEffect(() => {
    prevActiveEl.current = document.activeElement as HTMLElement | null
    const root = document.getElementById('root')
    const prevOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    if (root) root.setAttribute('aria-hidden', 'true')

    return () => {
      document.body.style.overflow = prevOverflow
      if (root) root.removeAttribute('aria-hidden')
      // Restore focus
      try { prevActiveEl.current?.focus({ preventScroll: true }) } catch {}
    }
  }, [])

  // Focus trap & initial focus
  useEffect(() => {
    const node = dialogRef.current
    if (!node) return
    const focusFirst = () => {
      if (initialFocusRef?.current) {
        initialFocusRef.current.focus()
        return
      }
      const focusables = getFocusable(node)
      ;(focusables[0] ?? node).focus()
    }
    // Slight delay to ensure portal content is in DOM
    const id = window.setTimeout(focusFirst, 0)
    return () => window.clearTimeout(id)
  }, [initialFocusRef])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault()
        onRequestClose()
        return
      }
      if (e.key === 'Tab') {
        const node = dialogRef.current
        if (!node) return
        const focusables = getFocusable(node)
        if (focusables.length === 0) {
          e.preventDefault()
          node.focus()
          return
        }
        const current = document.activeElement as HTMLElement | null
        const idx = Math.max(0, focusables.indexOf(current ?? focusables[0]))
        const dir = e.shiftKey ? -1 : 1
        const next = (idx + dir + focusables.length) % focusables.length
        e.preventDefault()
        focusables[next].focus()
      }
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onRequestClose])

  const onBackdropClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!closeOnBackdrop) return
    if (e.target === overlayRef.current) onRequestClose()
  }

  const content = (
    <div ref={overlayRef} className="fixed inset-0 z-50 flex items-center justify-center" onMouseDown={onBackdropClick}>
      <div className="absolute inset-0 bg-black/50" />
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className="relative z-10 max-h-[85vh] w-[min(92vw,42rem)] overflow-auto rounded-md bg-white p-4 shadow-xl outline-none"
      >
        {children}
      </div>
    </div>
  )

  return createPortal(content, document.body)
}

