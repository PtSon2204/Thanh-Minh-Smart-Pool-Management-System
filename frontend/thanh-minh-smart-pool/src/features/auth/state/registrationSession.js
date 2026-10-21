import { registerPendingResponseSchema } from '../types/authSchemas'

const STORAGE_KEY = 'smartpool.pendingRegistration'

export function savePendingRegistration(pending) {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify({
      registrationId: pending.registrationId,
      otpExpiresAtUtc: pending.otpExpiresAtUtc,
      registrationExpiresAtUtc: pending.registrationExpiresAtUtc,
      resendAvailableAtUtc: pending.resendAvailableAtUtc,
      email: pending.email,
      username: pending.username,
    }))
  } catch {
    // Navigation state still lets the user verify in the current tab.
  }
}

export function readPendingRegistration() {
  try {
    const value = JSON.parse(sessionStorage.getItem(STORAGE_KEY) || 'null')
    const parsed = registerPendingResponseSchema.safeParse(value)
    if (!parsed.success || typeof value.email !== 'string' || typeof value.username !== 'string' ||
      Date.parse(parsed.data.registrationExpiresAtUtc) <= Date.now()) {
      clearPendingRegistration()
      return null
    }
    return { ...parsed.data, email: value.email, username: value.username }
  } catch {
    clearPendingRegistration()
    return null
  }
}

export function clearPendingRegistration() {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // A blocked storage API does not affect the server-side registration state.
  }
}
