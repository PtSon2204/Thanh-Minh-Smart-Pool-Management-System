import test from 'node:test'
import assert from 'node:assert/strict'
import {
  isBackendDiagnosticsEnabled,
  canDownloadBackendDiagnostics,
  backendDiagnosticsFilename,
  downloadBackendCameraDiagnostics,
} from './backendCameraDiagnostics.js'
import { useAuthStore } from '../../auth/store/authStore.js'

const GUID = '3fa85f64-5717-4565-b152-246e539dab8c'
const enabledEnv = { VITE_CAMERA_DIAGNOSTICS: 'true', DEV: true }

test('enabled only in a dev build with the flag set', () => {
  assert.equal(isBackendDiagnosticsEnabled(enabledEnv), true)
  assert.equal(isBackendDiagnosticsEnabled({ VITE_CAMERA_DIAGNOSTICS: 'true' }), false, 'a production build must stay off')
  assert.equal(isBackendDiagnosticsEnabled({ DEV: true }), false, 'unset flag must stay off')
  assert.equal(isBackendDiagnosticsEnabled(undefined), false)
})

test('ADMIN role gate is case-insensitive and covers the roles array', () => {
  assert.equal(canDownloadBackendDiagnostics({ role: 'ADMIN' }), true)
  assert.equal(canDownloadBackendDiagnostics({ role: 'admin' }), true)
  assert.equal(canDownloadBackendDiagnostics({ roles: ['LIFEGUARD', 'Admin'] }), true)
  assert.equal(canDownloadBackendDiagnostics({ role: 'LIFEGUARD' }), false)
  assert.equal(canDownloadBackendDiagnostics(null), false)
})

test('the export file name follows backend-diagnostics-{cameraId}.json', () => {
  assert.equal(backendDiagnosticsFilename(GUID), `backend-diagnostics-${GUID}.json`)
})

test('download refuses when diagnostics are disabled, even for an ADMIN', async () => {
  useAuthStore.getState().setSession({ role: 'ADMIN' })
  await assert.rejects(() => downloadBackendCameraDiagnostics(GUID, { DEV: true }), /disabled/)
  useAuthStore.getState().setSession(null)
  await assert.rejects(() => downloadBackendCameraDiagnostics(GUID, enabledEnv), /ADMIN role required/, 'no signed-in session')
})

test('download validates the camera id before any request is made', async () => {
  useAuthStore.getState().setSession({ role: 'ADMIN' })
  try {
    await assert.rejects(() => downloadBackendCameraDiagnostics('not-a-guid', enabledEnv), /Invalid cameraId/)
    await assert.rejects(() => downloadBackendCameraDiagnostics('', enabledEnv), /cameraId is required/)
  } finally {
    useAuthStore.getState().setSession(null)
  }
})
