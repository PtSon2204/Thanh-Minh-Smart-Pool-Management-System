const waitingStatuses = new Set(['PENDING', 'PARTIAL'])
const finalStatuses = new Set(['ISSUED', 'EXPIRED', 'CANCELLED'])

export function isRentalPaymentWaiting(status) {
  return waitingStatuses.has(status)
}

export function isRentalPaymentFinal(status) {
  return finalStatuses.has(status)
}

export function createRentalPaymentClock(payment, receivedAtMs = Date.now()) {
  const serverTimeMs = Date.parse(payment?.serverTimeUtc)
  const expiresAtMs = Date.parse(payment?.expiresAtUtc)
  if (!Number.isFinite(serverTimeMs) || !Number.isFinite(expiresAtMs)) return null
  return { paymentId: payment.id, receivedAtMs, serverTimeMs, expiresAtMs }
}

export function getRentalPaymentSecondsLeft(clock, nowMs = Date.now()) {
  if (!clock) return 0
  const serverNowMs = clock.serverTimeMs + Math.max(0, nowMs - clock.receivedAtMs)
  return Math.max(0, Math.ceil((clock.expiresAtMs - serverNowMs) / 1000))
}

export function requiresRentalPaymentReconciliation(payment) {
  if (!payment) return false
  if (payment.requiresReconciliation) return true
  return ['EXPIRED', 'CANCELLED'].includes(payment.status)
    && (Number(payment.paidAmount) > 0 || Number(payment.excessAmount) > 0)
}

export function rentalPaymentIntentStorageKey(staffId) {
  return `smartpool-rental-payment:${staffId}`
}

export function readRentalPaymentIntent(staffId, storage = sessionStorage) {
  if (!staffId) return null
  try {
    const value = JSON.parse(storage.getItem(rentalPaymentIntentStorageKey(staffId)) || 'null')
    return value?.requestId || value?.paymentId ? value : null
  } catch {
    return null
  }
}

export function saveRentalPaymentIntent(staffId, intent, storage = sessionStorage) {
  if (!staffId) return
  try {
    const key = rentalPaymentIntentStorageKey(staffId)
    if (intent?.requestId || intent?.paymentId) {
      const previous = readRentalPaymentIntent(staffId, storage)
      const requestPayload = Object.hasOwn(intent, 'requestPayload') ? intent.requestPayload : previous?.requestPayload || null
      storage.setItem(key, JSON.stringify({ requestId: intent.requestId || null, paymentId: intent.paymentId || null, requestPayload }))
    }
    else storage.removeItem(key)
  } catch {}
}
