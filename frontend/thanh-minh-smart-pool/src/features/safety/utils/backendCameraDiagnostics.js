import { useAuthStore } from '../../auth/store/authStore.js'
import { hasSessionRole } from '../../auth/utils/sessionRoles.js'

// Pure, dependency-injectable predicates so the guard logic is testable without
// a browser, network, or Vite environment.
export function isBackendDiagnosticsEnabled(env = {}) {
  return String(env?.VITE_CAMERA_DIAGNOSTICS ?? '') === 'true' && Boolean(env?.DEV)
}

export function canDownloadBackendDiagnostics(session) {
  return hasSessionRole(session, 'ADMIN')
}

export function backendDiagnosticsFilename(cameraId) {
  return `backend-diagnostics-${cameraId}.json`
}

function assertAllowed(cameraId, env) {
  if (!isBackendDiagnosticsEnabled(env)) throw new Error('Backend camera diagnostics are disabled.')
  if (!canDownloadBackendDiagnostics(useAuthStore.getState().session)) throw new Error('ADMIN role required.')
  if (typeof cameraId !== 'string' || !cameraId.trim()) throw new Error('cameraId is required.')
  if (!/^[0-9a-f-]{36}$/i.test(cameraId)) throw new Error('Invalid cameraId.')
}

// Development-only, ADMIN-only. Reuses the authenticated apiClient so the Bearer
// token is attached by the existing interceptor and is never logged or written
// to the export file. The export contains the diagnostics array only.
export async function downloadBackendCameraDiagnostics(cameraId, env = import.meta.env) {
  assertAllowed(cameraId, env)
  // Lazy import keeps this module importable in plain Node tests (no network stack);
  // in the app the same cached module instance is used by the rest of the frontend.
  const { default: apiClient } = await import('../../../services/apiClient.js')
  const { data } = await apiClient.get(`/api/cameras/${encodeURIComponent(cameraId)}/diagnostics`, {
    params: { limit: 512 },
  })
  const payload = JSON.stringify(data, null, 2)
  const blob = new Blob([payload], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = backendDiagnosticsFilename(cameraId)
  anchor.click()
  URL.revokeObjectURL(url)
  return data
}
