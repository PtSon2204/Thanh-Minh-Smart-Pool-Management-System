import { ALERT_POPUP_LIFETIME_MS } from './popupLifetime.js'

export function canHandleAlert(alert, now = Date.now()) {
  if (alert?.status?.toUpperCase() !== 'PENDING') return false
  const observed = Date.parse(alert.observedAt || alert.createdAt)
  const age = now - observed
  return Number.isFinite(age) && age >= 0 && age < ALERT_POPUP_LIFETIME_MS
}

// An alert currently open in the live popup/modal is still actionable regardless
// of its age: the popup only stays open while the camera risk is live (or the
// short fallback window). Terminal statuses still win, and the backend re-checks.
export function canHandleOpenAlert(alert) {
  return alert?.status?.toUpperCase() === 'PENDING'
}
