// Optional, bounded camera pipeline diagnostics. Disabled by default: the ring
// buffer only records when VITE_CAMERA_DIAGNOSTICS is set. No frame pixels,
// RTSP URL, credential or personal data ever enter a diagnostic event.
// `import.meta.env` only exists in a Vite build; the module is importable in
// plain Node (tests) where it must simply stay disabled.
const RAW_FLAG = import.meta.env?.VITE_CAMERA_DIAGNOSTICS
const ENABLED = String(RAW_FLAG ?? '') === 'true'
const DEV = Boolean(import.meta.env?.DEV)

const CAMERA_BUFFER_LIMIT = 512
const CAMERA_MIN_INTERVAL_MS = 500

class DiagnosticRingBuffer {
  cameras = new Map()
  lastFrameAt = new Map()

  record(cameraId, event) {
    if (!ENABLED || !cameraId) return
    const buffer = this.cameras.get(cameraId) || []
    buffer.push(event)
    while (buffer.length > CAMERA_BUFFER_LIMIT) buffer.shift()
    this.cameras.set(cameraId, buffer)
  }

  shouldSampleFrame(cameraId, now) {
    if (!ENABLED || !cameraId) return false
    const previous = this.lastFrameAt.get(cameraId)
    if (previous !== undefined && now - previous < CAMERA_MIN_INTERVAL_MS) return false
    this.lastFrameAt.set(cameraId, now)
    return true
  }

  snapshot(cameraId) {
    if (!cameraId) return []
    return this.cameras.get(cameraId) ?? []
  }

  snapshotAll() {
    return Object.fromEntries([...this.cameras.entries()].map(([cameraId, events]) => [cameraId, [...events]]))
  }

  clear(cameraId) {
    this.cameras.delete(cameraId)
    this.lastFrameAt.delete(cameraId)
  }
}

export const cameraDiagnostics = new DiagnosticRingBuffer()
export const cameraDiagnosticsEnabled = ENABLED

export const CAMERA_DIAGNOSTICS_GROUP = 'SmartPool camera diagnostics'

// DevTools-only, dev+enabled-build-only helpers. Exposed only when the Vite env
// flag is set in a development build, so they never ship to production.
//   window.__cameraDiagnostics.get('<cameraId>')  -> bounded browser snapshot
//   window.__cameraDiagnostics.downloadBackend('<cameraId>') -> downloads the
//     backend ring buffer to backend-diagnostics-<cameraId>.json (ADMIN only).
if (ENABLED && DEV && typeof globalThis !== 'undefined' && globalThis.window) {
  globalThis.window.__cameraDiagnostics = {
    get: (cameraId) => cameraId ? cameraDiagnostics.snapshot(cameraId) : cameraDiagnostics.snapshotAll(),
    clear: (cameraId) => cameraDiagnostics.clear(cameraId),
    downloadBackend: async (cameraId) => {
      const { downloadBackendCameraDiagnostics } = await import('./backendCameraDiagnostics.js')
      return downloadBackendCameraDiagnostics(cameraId, import.meta.env)
    },
  }
}

