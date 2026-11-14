export function isRecentLiveAlert(alert, now) {
  const observed = Date.parse(alert.observedAt || alert.createdAt)
  return alert.sourceType === 'LIVE' && Number.isFinite(observed) && now - observed >= 0 && now - observed <= 120000
}

// Closing a warning must not dismiss a later escalation of the same DB row.
export const popupIdentity = alert => `${alert.id}:${alert.severity?.toUpperCase()}`

// The same row can arrive through SignalR and overlapping HTTP snapshots.
// A delayed WARNING must not replace/close its newer CRITICAL escalation.
export function mergeAlertUpdate(current, incoming) {
  if (!current) return incoming
  const rank = alert => ['CRITICAL', 'EMERGENCY'].includes(alert.severity?.toUpperCase()) ? 2 : 1
  const time = alert => Date.parse(alert.observedAt || alert.createdAt)
  if (rank(incoming) < rank(current) || time(incoming) < time(current)) return current
  return incoming
}

export function reconcilePendingAlerts(active, pending, dismissed, now) {
  const existing = new Map(active.map(a => [a.id, a]))
  const merged = new Map(existing)
  for (const alert of pending) merged.set(alert.id, mergeAlertUpdate(existing.get(alert.id), alert))
  // Absence from a list snapshot is not a terminal status. Keep visible popups
  // on their original timer; callers verify missing rows using the detail API.
  return [...merged.values()].filter(a => a.sourceType === 'LIVE' && a.status?.toUpperCase() === 'PENDING'
    && !dismissed.has(popupIdentity(a)) && (existing.has(a.id) || isRecentLiveAlert(a, now)))
}

export function sortAlerts(alerts) {
  return [...alerts].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt)
    || a.id.localeCompare(b.id))
}

// Classify a live camera_analysis frame for a given LIVE alert so the global
// popup follows the risk on that camera. Tracker track ids are re-issued across
// session resets, so the alert's original track id may not survive even while
// the same risk is still visible: close only when the camera reports no active
// risk at all.
//   'present' — the camera still flags a risk: keep the popup open.
//   'cleared' — the camera reports no active risk: the alert is over, close.
//   'unknown' — another camera, or a frame that is not Connected: let the
//               fallback timer decide; never force a close on uncertain data.
export function cameraRiskState(payload, alert) {
  if (!payload?.cameraId || !alert?.cameraId
    || String(payload.cameraId).toLowerCase() !== String(alert.cameraId).toLowerCase()) return 'unknown'
  if (payload.state !== 'Connected') return 'unknown'
  const active = payload.activeRiskTrackIds || []
  const current = (payload.displayRisks ?? payload.risks ?? []).filter(risk => risk && risk.trackId)
  return active.length === 0 && current.length === 0 ? 'cleared' : 'present'
}
