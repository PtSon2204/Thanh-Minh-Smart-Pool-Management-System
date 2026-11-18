export function criticalAnnouncement(alert) {
  if (alert?.status !== 'ACKNOWLEDGED' || !alert.incidentId || !alert.location?.trim()) return null
  const severity = String(alert.severity || '').toUpperCase()
  const location = alert.location.trim()
  if (severity === 'CRITICAL' || severity === 'EMERGENCY') {
    return { id: alert.incidentId, text: `Sự cố khẩn cấp xảy ra ở khu vực ${location}.` }
  }
  if (severity === 'WARNING') {
    return { id: alert.incidentId, text: `Cảnh báo an toàn tại khu vực ${location}.` }
  }
  return null
}
