import { useEffect, useState } from 'react'

// Update the actions while a row/dialog remains open; no HTTP refetch is needed
// to remove buttons at the end of the LIVE response window.
export function useAlertActionClock(enabled = true) {
  const [now, setNow] = useState(Date.now)
  useEffect(() => {
    if (!enabled) return undefined
    const timer = setInterval(() => setNow(Date.now()), 250)
    return () => clearInterval(timer)
  }, [enabled])
  return now
}
