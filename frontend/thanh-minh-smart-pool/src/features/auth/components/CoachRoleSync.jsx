import { useEffect, useRef } from 'react'
import { hasValidRefreshToken, useAuthStore } from '../store/authStore'
import { hasSessionRole } from '../utils/sessionRoles'
import { refreshSession } from '../../../services/apiClient'

export default function CoachRoleSync() {
  const session = useAuthStore((state) => state.session)
  const lastCheck = useRef(0)

  useEffect(() => {
    if (!hasSessionRole(session, 'COACH')) return undefined
    const check = () => {
      const current = useAuthStore.getState().session
      if (!hasValidRefreshToken(current) || Date.now() - lastCheck.current < 60_000) return
      lastCheck.current = Date.now()
      void refreshSession().catch(() => {})
    }
    const onVisibility = () => { if (document.visibilityState === 'visible') check() }
    check()
    window.addEventListener('focus', check)
    document.addEventListener('visibilitychange', onVisibility)
    const timer = window.setInterval(check, 120_000)
    return () => {
      window.removeEventListener('focus', check)
      document.removeEventListener('visibilitychange', onVisibility)
      window.clearInterval(timer)
    }
  }, [session])

  return null
}
