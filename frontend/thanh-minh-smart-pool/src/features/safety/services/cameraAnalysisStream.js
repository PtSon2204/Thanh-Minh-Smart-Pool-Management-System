import { getSignalRConnection, onSignalRStateChange, startSignalRConnection } from '../../../services/signalrClient'
import { cameraDiagnostics } from '../utils/cameraDiagnostics.js'

// A card and its enlarged modal can overlap during mounting. Keep one server group
// subscription per camera so closing either does not unsubscribe the other viewer.
const subscribers = new Map()
let initialized = false

async function join(cameraId) {
  if (await startSignalRConnection() && subscribers.has(cameraId)) {
    const at = Date.now()
    try {
      await getSignalRConnection().invoke('SubscribeCameraAnalysis', cameraId)
      cameraDiagnostics.record(cameraId, { source: 'signalr-subscribe', at, ok: true })
    } catch (error) {
      cameraDiagnostics.record(cameraId, { source: 'signalr-subscribe', at, ok: false,
        error: error?.message || error?.name || 'unknown-error' })
      /* HTTP fallback stays active. */
    }
  }
}

export function subscribeCameraAnalysis(cameraId, listener) {
  const id = cameraId.toLowerCase()
  if (!initialized) {
    initialized = true
    getSignalRConnection().on('camera_analysis', payload => {
      const cameraId = payload?.cameraId?.toLowerCase()
      const listeners = subscribers.get(cameraId)
      cameraDiagnostics.record(cameraId, { source: 'signalr-event', at: Date.now(),
        frameId: payload?.frameId ?? null, rtpTimestamp: payload?.rtpTimestamp ?? null,
        trackingSession: payload?.trackingSession ?? null, accepted: Boolean(listeners?.size) })
      listeners?.forEach(callback => callback(payload))
    })
    onSignalRStateChange(state => {
      cameraDiagnostics.record('signalr', { source: 'signalr-state', at: Date.now(), state })
      if (state === 'connected') for (const key of subscribers.keys()) void join(key)
    })
  }
  const listeners = subscribers.get(id) || new Set()
  const first = listeners.size === 0
  listeners.add(listener)
  subscribers.set(id, listeners)
  if (first) void join(id)
  return () => {
    listeners.delete(listener)
    if (listeners.size === 0 && subscribers.get(id) === listeners) {
      subscribers.delete(id)
      if (getSignalRConnection().state === 'Connected') {
        void getSignalRConnection().invoke('UnsubscribeCameraAnalysis', id).catch(() => {})
      }
    }
  }
}
