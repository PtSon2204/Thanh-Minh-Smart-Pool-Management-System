export const MAX_OVERLAY_AGE_MS = 1000

// Nhãn "đang giám sát" không được nhấp nháy theo nhịp push từng frame: chỉ khi
// không nhận được bất kỳ dữ liệu phân tích nào trong 10s mới xuống "chờ dữ liệu".
export const MONITORING_STALE_MS = 10000

export function cameraAnalysisAge(data, now) {
  if (!data || !Number.isFinite(data.receivedAt)) return Infinity
  const age = now - data.receivedAt + Date.parse(data.serverTime) - Date.parse(data.observedAt)
  return Number.isFinite(age) && age >= 0 ? age : Infinity
}

export function metadataRejectionReason(current, incoming) {
  if (!current || !incoming) return null
  if (current.state === 'Connected' && !Number.isFinite(Date.parse(incoming.observedAt))) return 'empty-response'
  if (current.trackingSession && incoming.trackingSession && current.trackingSession !== incoming.trackingSession) {
    const before = Date.parse(current.observedAt), next = Date.parse(incoming.observedAt)
    if (Number.isFinite(before) && Number.isFinite(next) && next < before) return 'older-session'
    if (Number.isFinite(before) && Number.isFinite(next) && next === before) {
      const currentProcessed = Date.parse(current.processedAt), incomingProcessed = Date.parse(incoming.processedAt)
      if (!(Number.isFinite(incomingProcessed) && Number.isFinite(currentProcessed) && incomingProcessed > currentProcessed)) return 'older-session'
    }
  }
  if (Number.isInteger(current.rtpTimestamp) && Number.isInteger(incoming.rtpTimestamp)
    && ((incoming.rtpTimestamp - current.rtpTimestamp) | 0) <= 0) return 'older-rtp'
  const before = Date.parse(current.processedAt), next = Date.parse(incoming.processedAt)
  return Number.isFinite(before) && Number.isFinite(next) && next < before ? 'older-processedAt' : null
}

export function mergeCameraAnalysis(current, incoming, receivedAt) {
  if (!current) return { ...incoming, receivedAt }
  if (metadataRejectionReason(current, incoming)) return current
  return { ...incoming, receivedAt }
}
