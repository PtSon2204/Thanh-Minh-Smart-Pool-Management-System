export const MAX_OVERLAY_AGE_MS = 1000

export function cameraAnalysisAge(data, now) {
  if (!data || !Number.isFinite(data.receivedAt)) return Infinity
  const age = now - data.receivedAt + Date.parse(data.serverTime) - Date.parse(data.observedAt)
  return Number.isFinite(age) && age >= 0 ? age : Infinity
}

export function mergeCameraAnalysis(current, incoming, receivedAt) {
  const before = Date.parse(current?.processedAt)
  const next = Date.parse(incoming?.processedAt)
  if (Number.isFinite(before) && Number.isFinite(next) && next < before) return current
  return { ...incoming, receivedAt }
}
