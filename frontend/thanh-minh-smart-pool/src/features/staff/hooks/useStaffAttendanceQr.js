import { useCallback, useEffect, useRef, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { attendanceQrService } from '../services/attendanceQrService.js'
import { claimAttendanceQrStart, initialAttendanceQrState, reduceAttendanceQrSession } from './attendanceQrState.js'

function isCurrentSession(session, candidate) {
  return session?.sessionId === candidate.sessionId && session.generation === candidate.generation
}

export function useStaffAttendanceQr() {
  const [state, setState] = useState(initialAttendanceQrState)
  const [error, setError] = useState(null)
  const [now, setNow] = useState(() => Date.now())
  const sessionRef = useRef(null)
  const requestIdRef = useRef(0)
  const startPendingRef = useRef(false)
  const isStoppingRef = useRef(false)
  const disposedRef = useRef(false)
  const mountedRef = useRef(false)
  const stopRef = useRef(null)
  const { mutateAsync: createSession, isPending: isCreating } = useMutation({ mutationFn: attendanceQrService.create, retry: false })
  const { mutateAsync: rotateSession, isPending: isRotating } = useMutation({ mutationFn: ({ sessionId, generation }) => attendanceQrService.rotate(sessionId, generation), retry: false })
  const { mutateAsync: stopSession, isPending: isStopping } = useMutation({ mutationFn: attendanceQrService.stop, retry: false })

  const transition = useCallback((event) => {
    setState((current) => {
      const next = reduceAttendanceQrSession(current, event)
      sessionRef.current = next.session
      return next
    })
  }, [])

  const stop = useCallback(async () => {
    const session = sessionRef.current
    if (!session || isStoppingRef.current) return

    isStoppingRef.current = true
    requestIdRef.current += 1
    sessionRef.current = null
    transition({ type: 'stopRequested' })
    try {
      await stopSession(session.sessionId)
      setError(null)
      transition({ type: 'stopped' })
    } catch (stopError) {
      setError({ operation: 'stop', error: stopError })
      transition({ type: 'stopFailed', stopBoundary: session.sessionExpiresAtUtc })
    } finally {
      isStoppingRef.current = false
    }
  }, [stopSession, transition])

  const discardIssuedSession = useCallback(async (sessionId) => {
    try {
      await attendanceQrService.stop(sessionId)
    } catch {
      // The page is gone or the request was invalidated. Do not retry this uncertain cleanup.
    }
  }, [])

  const start = useCallback(async (input) => {
    if (sessionRef.current || isStoppingRef.current || !claimAttendanceQrStart(startPendingRef)) return

    const requestId = requestIdRef.current + 1
    requestIdRef.current = requestId
    setError(null)
    transition({ type: 'startRequested' })
    try {
      const session = await createSession(input)
      if (disposedRef.current || requestIdRef.current !== requestId) {
        await discardIssuedSession(session.sessionId)
        return
      }
      sessionRef.current = session
      transition({ type: 'started', session })
    } catch (createError) {
      if (requestIdRef.current === requestId) {
        setError({ operation: 'start', error: createError })
        transition({ type: 'rotationFailed' })
      }
    } finally {
      startPendingRef.current = false
    }
  }, [createSession, discardIssuedSession, transition])

  const rotate = useCallback(async (session) => {
    if (isStoppingRef.current || !isCurrentSession(sessionRef.current, session)) return

    const requestId = requestIdRef.current + 1
    requestIdRef.current = requestId
    try {
      const nextSession = await rotateSession({ sessionId: session.sessionId, generation: session.generation })
      if (isStoppingRef.current || requestIdRef.current !== requestId || !isCurrentSession(sessionRef.current, session)) return
      sessionRef.current = nextSession
      setError(null)
      transition({ type: 'rotated', session: nextSession })
    } catch (rotateError) {
      if (requestIdRef.current === requestId && isCurrentSession(sessionRef.current, session)) {
        setError({ operation: 'rotate', error: rotateError })
        transition({ type: 'rotationFailed' })
      }
    }
  }, [rotateSession, transition])

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1_000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    if (state.phase !== 'active' || !state.session) return undefined
    const session = state.session
    const tokenExpiry = Date.parse(session.tokenExpiresAtUtc)
    const sessionExpiry = Date.parse(session.sessionExpiresAtUtc)
    const timer = window.setTimeout(() => {
      if (isStoppingRef.current || !isCurrentSession(sessionRef.current, session)) return
      if (sessionExpiry <= tokenExpiry) {
        requestIdRef.current += 1
        setError({ operation: 'expired', error: null })
        transition({ type: 'expired' })
        return
      }
      void rotate(session)
    }, Math.max(0, tokenExpiry - Date.now()))
    return () => window.clearTimeout(timer)
  }, [rotate, state.phase, state.session, transition])

  useEffect(() => {
    stopRef.current = stop
  }, [stop])
  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      queueMicrotask(() => {
        if (mountedRef.current) return
        disposedRef.current = true
        void stopRef.current?.()
      })
    }
  }, [])

  const session = state.session
  const canDisplay = state.phase === 'active' && Boolean(session) && Date.parse(session.tokenExpiresAtUtc) > now && Date.parse(session.sessionExpiresAtUtc) > now

  return {
    state,
    error,
    now,
    canDisplay,
    isBusy: isCreating || isRotating || isStopping || state.phase === 'starting' || state.phase === 'stopping',
    start,
    stop,
  }
}
