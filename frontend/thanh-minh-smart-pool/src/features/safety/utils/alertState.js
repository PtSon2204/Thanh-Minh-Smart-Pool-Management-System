export function reconcilePendingAlerts(active, pending, dismissed, now) {
  const existing = new Set(active.map(a => a.id))
  return pending.filter(a => a.sourceType === 'LIVE' && a.status?.toUpperCase() === 'PENDING'
    && !dismissed.has(a.id) && (existing.has(a.id) || (Number.isFinite(Date.parse(a.createdAt))
      && now - Date.parse(a.createdAt) >= 0 && now - Date.parse(a.createdAt) <= 120000)))
}

export function sortAlerts(alerts) {
  return [...alerts].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt)
    || a.id.localeCompare(b.id))
}
