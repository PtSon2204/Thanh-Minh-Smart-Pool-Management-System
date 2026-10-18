// The reset token never enters router state, a URL or browser storage.
let grant = null

export function savePasswordResetGrant(nextGrant) {
  grant = nextGrant
}

export function readPasswordResetGrant() {
  if (!grant || Date.parse(grant.resetTokenExpiresAtUtc) <= Date.now()) {
    grant = null
    return null
  }
  return grant
}

export function clearPasswordResetGrant() {
  grant = null
}
