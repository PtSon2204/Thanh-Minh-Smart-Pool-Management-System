import { useEffect } from 'react'
import { CameraPlayoutBuffer } from '../../safety/utils/cameraPlayoutBuffer'
import { cameraCapturePlan } from '../../safety/utils/cameraCapturePlan'

const DEFAULT_PLAYOUT_DELAY_MS = 300
const DEFAULT_SOURCE_FRAME_MS = 40
const FRAME_STALL_TIMEOUT_MS = 500
const FRAME_HEALTH_INTERVAL_MS = 250
const FRAME_INTERVAL_SAMPLE_LIMIT = 12

export function useCameraPlayout({ videoRef, playoutRef, risksRef, delayRef, samplerRef, paintOverlay,
  useSmooth, buffered, state, cameraId, setExactFallback, setBufferUnavailable }) {
  // Sample the AI timeline using the RTP timestamp of the frame actually displayed.
  // Canvas updates do not trigger React renders or wait for the inference loop.
  useEffect(() => {
    const video = videoRef.current
    if (!useSmooth || state !== 'playing' || !video) return undefined
    let handle, animation, previousRtp, disposed = false, lastFrame = performance.now(), shownAt = 0
    const queue = buffered ? new CameraPlayoutBuffer() : null
    const surface = queue ? document.createElement('canvas') : null
    const capture = surface?.getContext('2d', { alpha: false })
    const intervals = []
    playoutRef.current = queue
    // Box glides on the display refresh clock: between two presented frames the
    // sample point moves from the held frame's RTP toward the next queued one,
    // so the overlay is repainted at 60 Hz instead of stepping at source FPS.
    const present = now => {
      if (disposed) return
      if (queue.take(now, delayRef.current?.() ?? DEFAULT_PLAYOUT_DELAY_MS)) shownAt = now
      const current = queue.currentRtp()
      if (current !== undefined && samplerRef.current) {
        let timestamp = current
        const next = queue.peekNextRtp()
        if (next !== undefined) {
          const spanMs = ((next - current) | 0) / 90
          if (spanMs > 0) timestamp = (current + Math.round(((next - current) | 0) * Math.min(1, Math.max(0, (now - shownAt) / spanMs)))) | 0
        }
        risksRef.current = samplerRef.current?.(timestamp) || []
        paintOverlay()
      }
      animation = requestAnimationFrame(present)
    }
    const draw = (_, metadata) => {
      if (disposed) return
      if (!Number.isInteger(metadata.rtpTimestamp)) { setExactFallback(true); return }
      lastFrame = performance.now()
      if (queue) {
        try {
          const interval = previousRtp === undefined ? DEFAULT_SOURCE_FRAME_MS : ((metadata.rtpTimestamp - previousRtp) | 0) / 90
          previousRtp = metadata.rtpTimestamp
          if (interval >= 8 && interval <= 1000) { intervals.push(interval); if (intervals.length > FRAME_INTERVAL_SAMPLE_LIMIT) intervals.shift() }
          // A dropped presented frame must not halve the estimated source FPS
          // and unexpectedly discard the lookahead on the next callback.
          const frameMs = intervals.length ? Math.min(...intervals) : DEFAULT_SOURCE_FRAME_MS
          const plan = cameraCapturePlan(video.videoWidth, video.videoHeight, video.clientWidth, video.clientHeight,
            window.devicePixelRatio || 1, frameMs, Math.max(queue.delay, delayRef.current?.() ?? DEFAULT_PLAYOUT_DELAY_MS))
          if (!plan || !capture) throw new Error('Camera capture unavailable')
          if (surface.width !== plan.width || surface.height !== plan.height) { surface.width = plan.width; surface.height = plan.height }
          capture.drawImage(video, 0, 0, plan.width, plan.height)
          const frame = new VideoFrame(surface, { timestamp: Math.round(metadata.mediaTime * 1e6) })
          queue.capacity = plan.capacity
          queue.add(frame, metadata.rtpTimestamp, performance.now())
        } catch { setBufferUnavailable(true); return }
      } else {
        risksRef.current = samplerRef.current?.(metadata.rtpTimestamp) || []
        paintOverlay()
      }
      handle = video.requestVideoFrameCallback(draw)
    }
    handle = video.requestVideoFrameCallback(draw)
    if (queue) animation = requestAnimationFrame(present)
    const timer = setInterval(() => {
      if (performance.now() - lastFrame > FRAME_STALL_TIMEOUT_MS) { queue?.clear(); risksRef.current = []; paintOverlay() }
    }, FRAME_HEALTH_INTERVAL_MS)
    return () => {
      disposed = true
      clearInterval(timer)
      video.cancelVideoFrameCallback(handle)
      if (animation !== undefined) cancelAnimationFrame(animation)
      playoutRef.current = null
      queue?.clear()
    }
  }, [useSmooth, buffered, state, cameraId, videoRef, playoutRef, risksRef, delayRef, samplerRef,
    paintOverlay, setExactFallback, setBufferUnavailable])

}
