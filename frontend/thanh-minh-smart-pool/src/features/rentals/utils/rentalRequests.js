export function isRentalRequestOutcomeUncertain(error) {
  const status = error?.response?.status
  return !status || status >= 500
}

export function createRentalRequestId() {
  return crypto.randomUUID()
}
