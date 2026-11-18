const LOCAL_ECHO_TTL_MS = 60_000
const localAlertIds = new Map()

function pruneExpired(now) {
  for (const [id, expiresAt] of localAlertIds) {
    if (expiresAt <= now) localAlertIds.delete(id)
  }
}

export function rememberLocalSafetyAlert(clientRequestId, now = Date.now()) {
  if (!clientRequestId) return
  pruneExpired(now)
  localAlertIds.set(String(clientRequestId), now + LOCAL_ECHO_TTL_MS)
}

export function isLocalSafetyAlertEcho(payload, now = Date.now()) {
  const id = payload?.clientRequestId
  if (!id) return false
  pruneExpired(now)
  return localAlertIds.has(String(id))
}

export function clearLocalSafetyAlerts() {
  localAlertIds.clear()
}
