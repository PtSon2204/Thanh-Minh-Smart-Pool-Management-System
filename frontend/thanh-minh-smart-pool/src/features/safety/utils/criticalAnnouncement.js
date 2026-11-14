export function criticalAnnouncement(alert) {
  if (alert?.sourceType !== 'LIVE' || alert.status !== 'ACKNOWLEDGED' || !alert.incidentId ||
      !['CRITICAL', 'EMERGENCY'].includes(alert.severity) || !alert.location?.trim()) return null
  return { id: alert.incidentId, text: `Sự cố khẩn cấp xảy ra ở khu vực ${alert.location.trim()}.` }
}
