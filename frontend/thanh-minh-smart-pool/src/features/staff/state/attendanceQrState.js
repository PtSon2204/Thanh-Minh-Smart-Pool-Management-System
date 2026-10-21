export const initialAttendanceQrState = { phase: 'idle', session: null, stopBoundary: null }

export function claimAttendanceQrStart(startingRef) {
  if (startingRef.current) return false
  startingRef.current = true
  return true
}

export function reduceAttendanceQrSession(state, event) {
  if (event.type === 'startRequested') return { phase: 'starting', session: null, stopBoundary: null }
  if (event.type === 'started') return { phase: 'active', session: event.session, stopBoundary: null }
  if (event.type === 'rotated') {
    if (state.phase !== 'active' || state.session?.sessionId !== event.session.sessionId || event.session.generation <= state.session.generation) return state
    return { ...state, session: event.session }
  }
  if (event.type === 'rotationFailed' || event.type === 'expired') return { phase: 'error', session: null, stopBoundary: null }
  if (event.type === 'stopRequested') return { phase: 'stopping', session: null, stopBoundary: state.session?.sessionExpiresAtUtc ?? state.stopBoundary }
  if (event.type === 'stopped') return initialAttendanceQrState
  if (event.type === 'stopFailed') return { phase: 'stopUncertain', session: null, stopBoundary: event.stopBoundary }
  return state
}
