import { useCallback, useEffect, useRef, useState } from 'react'
import { monitoringService } from '../services/monitoringService'
import { VideoFrameBuffer, videoWarningKey } from '../utils/videoFrameBuffer'
import { drawVideoRisks } from '../utils/drawVideoRisks'

// Fetch ahead in media time; painting never waits for a network round trip.
export function useVideoFrameAnalysis(jobId, videoRef, canvasRef, enabled, autoPlay) {
  const controls = useRef(null)
  const [view, setView] = useState({ position: 0, buffering: true, error: '', warning: null, legacy: false })
  useEffect(() => {
    if (!enabled || !jobId || !videoRef.current) return undefined
    const video = videoRef.current
    const buffer = new VideoFrameBuffer()
    const shown = new Set()
    const abort = new AbortController()
    let disposed = false
    let pending = false
    let nextFetch = 0
    let wanted = autoPlay
    let holding = true
    let ownPauses = 0
    let legacy = false
    let error = ''
    let warning = null
    let frameHandle
    let animationHandle

    const publish = () => {
      const snapshot = { position: video.currentTime, buffering: holding, error, warning, legacy }
      if (!disposed) setView(old => Math.abs(old.position - snapshot.position) < 0.1
        && old.buffering === snapshot.buffering && old.error === error && old.warning === warning && old.legacy === legacy ? old : snapshot)
    }
    const paint = seconds => {
      const sample = buffer.sample(seconds)
      const risks = (sample?.risks || []).filter(r => r.severity === 'WARNING' || r.severity === 'CRITICAL')
      drawVideoRisks(canvasRef.current, video, risks)
      if (warning && !risks.some(risk => videoWarningKey(risk) === videoWarningKey(warning))) warning = null
      const dominant = [...risks].sort((a, b) => (b.severity === 'CRITICAL') - (a.severity === 'CRITICAL'))[0]
      if (!dominant) warning = null
      else if (!shown.has(videoWarningKey(dominant))) {
        shown.add(videoWarningKey(dominant))
        warning = { ...dominant, videoTimestamp: seconds }
      }
      publish()
    }
    const hold = () => {
      holding = true
      if (!video.paused) { ownPauses++; video.pause() }
    }
    const fetchAhead = async () => {
      if (pending || legacy || disposed || Date.now() < nextFetch) return
      pending = true
      const requested = video.currentTime
      try {
        const window = await monitoringService.frameWindow(jobId, requested, abort.signal)
        if (disposed) return
        if (window.legacy) {
          legacy = true
          holding = false
          warning = null
          drawVideoRisks(canvasRef.current, video, [])
        } else buffer.merge(window, video.currentTime)
        error = ''
        nextFetch = Date.now() + (buffer.ahead(video.currentTime) > 1 ? 300 : 120)
      } catch (failure) {
        if (!disposed) {
          error = failure.response?.data?.message || 'Không tải được dữ liệu cảnh báo đồng bộ. Đang thử lại…'
          nextFetch = Date.now() + 1000
        }
      } finally { pending = false; publish() }
    }
    const tick = () => {
      if (disposed || legacy) return
      if (video.seeking) { holding = true; publish(); return }
      const position = video.currentTime
      const ahead = buffer.ahead(position)
      if (ahead < 1.5) void fetchAhead()
      if (!buffer.sample(position) || (wanted && ahead < 0.08)) hold()
      if (holding && ahead >= 0.35 && video.readyState >= 2) {
        holding = false
        if (wanted && video.paused && !video.ended) {
          void video.play().catch(() => { wanted = false; publish() })
        }
      }
      if (!video.requestVideoFrameCallback || video.paused || holding) paint(position)
      publish()
    }
    const onPlay = () => { wanted = true; tick() }
    const onPause = () => {
      if (ownPauses > 0) { ownPauses--; return }
      wanted = false
      publish()
    }
    const onEnded = () => {
      wanted = false; holding = false; warning = null
      drawVideoRisks(canvasRef.current, video, [])
      publish()
    }
    const onSeeking = () => {
      shown.clear()
      warning = null
      nextFetch = 0
      drawVideoRisks(canvasRef.current, video, [])
      tick()
    }
    const onTime = () => { if (legacy) publish() }
    const renderFrame = (_, metadata) => {
      if (disposed) return
      if (!legacy && !video.seeking) {
        if (!buffer.sample(metadata.mediaTime)) hold()
        paint(metadata.mediaTime)
      }
      frameHandle = video.requestVideoFrameCallback(renderFrame)
    }
    if (video.requestVideoFrameCallback) frameHandle = video.requestVideoFrameCallback(renderFrame)
    else {
      const render = () => { if (!disposed) { if (!legacy) paint(video.currentTime); animationHandle = requestAnimationFrame(render) } }
      animationHandle = requestAnimationFrame(render)
    }
    controls.current = { dismiss: () => { warning = null; publish() } }
    const events = { play: onPlay, pause: onPause, ended: onEnded, seeking: onSeeking, seeked: tick, loadeddata: tick, timeupdate: onTime }
    Object.entries(events).forEach(([event, handler]) => video.addEventListener(event, handler))
    const resize = new ResizeObserver(() => { if (!legacy) paint(video.currentTime) })
    resize.observe(video)
    const timer = setInterval(tick, 50)
    void fetchAhead()
    return () => {
      disposed = true
      abort.abort()
      clearInterval(timer)
      resize.disconnect()
      if (frameHandle != null) video.cancelVideoFrameCallback(frameHandle)
      if (animationHandle != null) cancelAnimationFrame(animationHandle)
      Object.entries(events).forEach(([event, handler]) => video.removeEventListener(event, handler))
      video.pause()
      controls.current = null
    }
  }, [jobId, enabled, autoPlay, videoRef, canvasRef])
  const dismissWarning = useCallback(() => controls.current?.dismiss(), [])
  return { ...view, dismissWarning }
}
