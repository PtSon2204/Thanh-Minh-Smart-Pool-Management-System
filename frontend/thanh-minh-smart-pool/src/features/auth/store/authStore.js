import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'

export function hasValidAccessToken(session) {
  return Boolean(session?.accessToken) && Date.parse(session.expiresAtUtc) > Date.now()
}

export function hasValidRefreshToken(session) {
  return !session?.mustChangePassword && Boolean(session?.refreshToken) && Date.parse(session.refreshTokenExpiresAtUtc) > Date.now()
}

function normalizeSession(session) {
  if (!session?.mustChangePassword) return session
  return hasValidAccessToken(session)
    ? { ...session, refreshToken: null, refreshTokenExpiresAtUtc: null }
    : null
}

export const useAuthStore = create(
  persist(
    (set) => ({
      session: null,
      setSession: (session) => set({ session: normalizeSession(session) }),
    }),
    {
      name: 'thanh-minh-auth-session',
      storage: createJSONStorage(() => sessionStorage),
      partialize: (state) => ({ session: state.session }),
      merge: (persisted, current) => ({ ...current, session: normalizeSession(persisted?.session ?? null) }),
    },
  ),
)

// sessionStorage hydration is synchronous. Also expire an idle restricted session.
let expiryTimer
function scheduleRestrictedExpiry() {
  clearTimeout(expiryTimer)
  const { session } = useAuthStore.getState()
  if (!session?.mustChangePassword) return
  expiryTimer = setTimeout(() => {
    if (useAuthStore.getState().session === session) {
      useAuthStore.getState().setSession(null)
    }
  }, Math.max(0, Date.parse(session.expiresAtUtc) - Date.now()))
}
useAuthStore.subscribe(scheduleRestrictedExpiry)
scheduleRestrictedExpiry()

export function getAccessToken() {
  const { session } = useAuthStore.getState()
  return hasValidAccessToken(session) ? session.accessToken : null
}
