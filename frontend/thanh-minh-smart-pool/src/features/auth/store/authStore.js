import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'

export function hasValidAccessToken(session) {
  return Boolean(session?.accessToken) && Date.parse(session.expiresAtUtc) > Date.now()
}

export function hasValidRefreshToken(session) {
  return Boolean(session?.refreshToken) && Date.parse(session.refreshTokenExpiresAtUtc) > Date.now()
}

export const useAuthStore = create(
  persist(
    (set) => ({
      session: null,
      setSession: (session) => set({ session }),
    }),
    {
      name: 'thanh-minh-auth-session',
      storage: createJSONStorage(() => sessionStorage),
      partialize: (state) => ({ session: state.session }),
    },
  ),
)

export function getAccessToken() {
  const { session } = useAuthStore.getState()
  return hasValidAccessToken(session) ? session.accessToken : null
}
