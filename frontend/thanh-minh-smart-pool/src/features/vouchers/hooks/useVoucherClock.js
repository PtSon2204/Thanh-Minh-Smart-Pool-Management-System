import { useEffect, useState } from 'react'

// Re-evaluate cached vouchers while a page stays open, including after a background tab resumes.
export function useVoucherClock(enabled = true) {
  const [now, setNow] = useState(Date.now)
  useEffect(() => {
    if (!enabled) return
    const refresh = () => setNow(Date.now())
    const initialRefresh = window.setTimeout(refresh, 0)
    const timer = window.setInterval(refresh, 1000)
    window.addEventListener('focus', refresh)
    return () => {
      window.clearTimeout(initialRefresh)
      window.clearInterval(timer)
      window.removeEventListener('focus', refresh)
    }
  }, [enabled])
  return now
}
