import { memo, useEffect, useRef, useState } from 'react'
import { Alert, Button, Modal, Spin, Tag } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'
import equipmentService from '../services/equipmentService'
import apiClient from '../../../services/apiClient'
import { getAccessToken } from '../../auth/store/authStore'
import BrowserCameraPreview from './BrowserCameraPreview'
import { drawVideoRisks } from '../../safety/utils/drawVideoRisks'
import { CameraPlayoutBuffer } from '../../safety/utils/cameraPlayoutBuffer'
import { cameraCapturePlan } from '../../safety/utils/cameraCapturePlan'

function waitForIce(peer, signal) {
  return new Promise((resolve, reject) => {
    let timer
    function finish(error) {
      clearTimeout(timer)
      peer.removeEventListener('icegatheringstatechange', changed)
      signal.removeEventListener('abort', cancelled)
      if (error) reject(error); else resolve()
    }
    function changed() { if (peer.iceGatheringState === 'complete') finish() }
    function cancelled() { finish(new Error('cancelled')) }
    peer.addEventListener('icegatheringstatechange', changed)
    signal.addEventListener('abort', cancelled, { once: true })
    timer = setTimeout(() => finish(new Error('ICE gathering timeout')), 10000)
    if (signal.aborted) cancelled()
    else changed()
  })
}

function cameraError(status) {
  if (status === 404) return 'Không tìm thấy camera'
  if (status === 503 || status === 410) return 'Camera hiện không khả dụng'
  if (status === 401 || status === 403) return 'Bạn không có quyền xem camera.'
  return 'Không thể kết nối đến camera'
}

// Safety screens prefer source FPS and sample interpolated boxes on the displayed
// RTP clock. Paired analyzed pixels are the fallback when that clock is unavailable.
export const CameraVideoPlayer = memo(function CameraVideoPlayer({ equipment, risks = [], analysis = false, smooth = false, sampleRisks, sampleRisksWithDiagnostics, getPlayoutDelay }) {
  const videoRef = useRef(null)
  const containerRef = useRef(null)
  const overlayRef = useRef(null)
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState('connecting')
  const [error, setError] = useState('')
  const [exactFallback, setExactFallback] = useState(false)
  const [bufferUnavailable, setBufferUnavailable] = useState(false)
  const useSmooth = analysis && smooth && !exactFallback && typeof HTMLVideoElement !== 'undefined'
    && 'requestVideoFrameCallback' in HTMLVideoElement.prototype
  const risksRef = useRef([])
  const playoutRef = useRef(null)
  const delayRef = useRef(getPlayoutDelay)
  const buffered = useSmooth && !bufferUnavailable && typeof VideoFrame === 'function'
  useEffect(() => { delayRef.current = getPlayoutDelay }, [getPlayoutDelay])
  // Prefer the observation-only diagnostics sampler (returns the same risks as
  // sampleRisks and records a bounded diagnostic event when enabled); falling
  // back keeps the plain sampler a single call, never a double sample.
  const activeSampler = sampleRisksWithDiagnostics ?? sampleRisks
  const samplerRef = useRef(activeSampler)
  useEffect(() => { samplerRef.current = activeSampler }, [activeSampler])
  const reconnectRef = useRef(null)
  useEffect(() => {
    if (equipment.connectionStatus === 'Connected') reconnectRef.current?.()
  }, [equipment.connectionStatus])

  function paintOverlay() {
    drawVideoRisks(overlayRef.current, videoRef.current, risksRef.current, playoutRef.current?.current?.frame)
  }

  // Áp box mới ngay khi nhận; không cộng thêm thời gian nội suy vào độ trễ AI.
  useEffect(() => {
    if (useSmooth && state === 'playing') return
    risksRef.current = !analysis && state === 'playing' ? risks : []
    paintOverlay()
  }, [risks, equipment.id, state, analysis, useSmooth])

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
      if (queue.take(now, delayRef.current?.() ?? 300)) shownAt = now
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
          const interval = previousRtp === undefined ? 40 : ((metadata.rtpTimestamp - previousRtp) | 0) / 90
          previousRtp = metadata.rtpTimestamp
          if (interval >= 8 && interval <= 1000) { intervals.push(interval); if (intervals.length > 12) intervals.shift() }
          // A dropped presented frame must not halve the estimated source FPS
          // and unexpectedly discard the lookahead on the next callback.
          const frameMs = intervals.length ? Math.min(...intervals) : 40
          const plan = cameraCapturePlan(video.videoWidth, video.videoHeight, video.clientWidth, video.clientHeight,
            window.devicePixelRatio || 1, frameMs, Math.max(queue.delay, delayRef.current?.() ?? 300))
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
      if (performance.now() - lastFrame > 500) { queue?.clear(); risksRef.current = []; paintOverlay() }
    }, 250)
    return () => {
      disposed = true
      clearInterval(timer)
      video.cancelVideoFrameCallback(handle)
      if (animation !== undefined) cancelAnimationFrame(animation)
      playoutRef.current = null
      queue?.clear()
    }
  }, [useSmooth, buffered, state, equipment.id])

  // Canvas theo kích thước container (resize window / đổi cột grid).
  useEffect(() => {
    if (!containerRef.current) return
    const observer = new ResizeObserver(paintOverlay)
    observer.observe(containerRef.current)
    return () => observer.disconnect()
  }, [])

  // Video có metadata mới (kích thước nội tại đổi) thì vẽ lại vùng letterbox.
  useEffect(() => {
    const video = videoRef.current
    if (!video) return
    const onMeta = () => paintOverlay()
    video.addEventListener('loadedmetadata', onMeta)
    return () => video.removeEventListener('loadedmetadata', onMeta)
  }, [])

  useEffect(() => {
    const video = videoRef.current
    let disposed = false
    let active = null
    let reconnectTimer
    let retries = 0
    let retryDelays = [1000, 2000, 4000]

    function release(connection) {
      if (!connection || connection.closed) return
      connection.closed = true
      connection.abort.abort()
      clearInterval(connection.poll)
      clearTimeout(connection.deadline)
      clearTimeout(connection.stable)
      clearTimeout(connection.stall)
      if (connection.peer) {
        connection.peer.ontrack = null
        connection.peer.onconnectionstatechange = null
        connection.peer.getReceivers().forEach(receiver => receiver.track?.stop())
        connection.peer.close()
      }
      if (connection.viewId) {
        equipmentService.closeCameraView(equipment.id, connection.viewId).catch(() => {})
        connection.viewId = null
      }
    }

    function clearVideo() {
      video.pause()
      video.srcObject?.getTracks().forEach(track => track.stop())
      video.srcObject = null
      video.removeAttribute('src')
      video.load()
    }

    function fail(connection, message, recoverable = true) {
      if (disposed || active !== connection || connection.closed) return
      active = null
      release(connection)
      clearVideo()
      setError(message)
      setState(recoverable ? 'disconnected' : 'error')
      if (recoverable && retries < retryDelays.length) {
        reconnectTimer = setTimeout(() => { retries++; connect() }, retryDelays[retries])
      }
    }

    function playing() {
      const connection = active
      if (!connection || connection.closed || disposed) return
      clearTimeout(connection.deadline)
      clearTimeout(connection.stable)
      clearTimeout(connection.stall)
      connection.hasPlayed = true
      connection.stable = setTimeout(() => { retries = 0 }, 10000)
      setError('')
      setState('playing')
    }

    function waiting() {
      const connection = active
      if (!connection || connection.closed || disposed) return
      clearTimeout(connection.stall)
      if (!connection.hasPlayed) { setState('connecting'); return }
      // Chờ ngắn để jitter buffer bù thiếu frame; tránh chớp đen khi mạng khựng vài ms.
      // Nếu không có hình trở lại sau 700 ms, ẩn frame cũ và hiện trạng thái kết nối.
      connection.stall = setTimeout(() => {
        if (!disposed && active === connection && !connection.closed) setState('connecting')
      }, 700)
    }

    function videoError() { if (active) fail(active, 'Không thể kết nối đến camera') }
    function pageHide() {
      if (!active?.viewId) return
      const url = new URL(`/api/equipments/${equipment.id}/camera/views/${active.viewId}`, apiClient.defaults.baseURL || window.location.origin)
      fetch(url, { method: 'DELETE', keepalive: true, headers: { Authorization: `Bearer ${getAccessToken() || ''}` } }).catch(() => {})
    }

    async function connect() {
      if (disposed) return
      const connection = { abort: new AbortController(), closed: false }
      active = connection
      setState('connecting')
      setError('')
      const current = () => !disposed && active === connection && !connection.closed
      try {
        if (!window.RTCPeerConnection) { fail(connection, 'Trình duyệt này không hỗ trợ WebRTC.', false); return }
        const config = await equipmentService.getCameraViewConfiguration(equipment.id, connection.abort.signal)
        if (!current()) return
        // Simulator đổi RTSP session ở mỗi lượt; retry nhanh, có giới hạn, cho riêng nguồn test.
        retryDelays = config.fastReconnect ? [250, 500, 750, 1000, 1000, 1000, 1000] : [1000, 2000, 4000]
        const peer = new RTCPeerConnection({ iceServers: config.iceServers || [] })
        connection.peer = peer
        const transceiver = peer.addTransceiver('video', { direction: 'recvonly' })
        const h264 = RTCRtpReceiver.getCapabilities?.('video')?.codecs.filter(codec => codec.mimeType.toLowerCase() === 'video/h264'
          && codec.sdpFmtpLine?.toLowerCase().includes('packetization-mode=1')) || []
        if (!h264.length) { fail(connection, 'Trình duyệt này không hỗ trợ video H.264 qua WebRTC.', false); return }
        const baseline = h264.filter(codec => /profile-level-id=42[ec]0/i.test(codec.sdpFmtpLine || ''))
        transceiver.setCodecPreferences?.(baseline.length ? baseline : h264)
        peer.ontrack = event => {
          if (!current()) return
          video.srcObject = event.streams[0] || new MediaStream([event.track])
          // Box đã nằm trong pixel, nên buffer ngắn giữ nguyên đồng bộ hình/box.
          try {
            const delay = buffered ? 60 : useSmooth ? 300 : analysis ? 120 : 60
            if ('jitterBufferTarget' in transceiver.receiver) transceiver.receiver.jitterBufferTarget = delay
            else if ('playoutDelayHint' in transceiver.receiver) transceiver.receiver.playoutDelayHint = delay / 1000
          } catch { /* Browser tự chọn jitter buffer nếu không hỗ trợ thiết lập này. */ }
          video.play().catch(() => { if (current()) fail(connection, 'Trình duyệt không cho phép tự động phát video.', false) })
        }
        peer.onconnectionstatechange = () => {
          if (current() && ['failed', 'disconnected', 'closed'].includes(peer.connectionState))
            fail(connection, 'Không thể kết nối đến camera')
        }
        await peer.setLocalDescription(await peer.createOffer())
        await waitForIce(peer, connection.abort.signal)
        if (!current()) return
        let view
        try {
          view = await equipmentService.openCameraView(equipment.id, { type: 'offer', sdp: peer.localDescription.sdp, analysis, smooth: useSmooth }, connection.abort.signal)
        } catch (failure) {
          if (!analysis || failure.response?.data?.code !== 'NotConfigured' || !current()) throw failure
          // AI chưa cấu hình: vẫn cho xem camera gốc, không vẽ box metadata lên video khác frame.
          view = await equipmentService.openCameraView(equipment.id, { type: 'offer', sdp: peer.localDescription.sdp, analysis: false }, connection.abort.signal)
        }
        if (!current()) { equipmentService.closeCameraView(equipment.id, view.viewId).catch(() => {}); return }
        connection.viewId = view.viewId
        await peer.setRemoteDescription({ type: view.type, sdp: view.sdp })
        connection.deadline = setTimeout(() => fail(connection, 'Không thể kết nối đến camera'), 20000)
        let polling = false
        let lastFrames = 0
        let lastProgress = Date.now()
        connection.poll = setInterval(async () => {
          if (!current() || polling) return
          polling = true
          try {
            const status = await equipmentService.getCameraViewStatus(equipment.id, view.viewId, connection.abort.signal)
            if (!current()) return
            if (status.state === 'Disconnected') { fail(connection, 'Camera hiện không khả dụng'); return }
            const stats = await peer.getStats()
            if (!current()) return
            stats.forEach(stat => {
              if (stat.type === 'inbound-rtp' && (stat.kind === 'video' || stat.mediaType === 'video')) {
                if (stat.framesDecoded > lastFrames) { lastFrames = stat.framesDecoded; lastProgress = Date.now() }
                else if (lastFrames > 0 && Date.now() - lastProgress > 5000) fail(connection, 'Camera hiện không khả dụng')
              }
            })
          } catch (e) {
            if (current()) fail(connection, cameraError(e.response?.status), ![400, 401, 403, 404].includes(e.response?.status))
          } finally { polling = false }
        }, 2000)
      } catch (e) {
        if (current()) fail(connection, cameraError(e.response?.status), ![400, 401, 403, 404].includes(e.response?.status))
      }
    }

    video.addEventListener('playing', playing)
    reconnectRef.current = () => {
      if (!disposed && !active && retries >= retryDelays.length) { retries = 0; clearTimeout(reconnectTimer); connect() }
    }
    video.addEventListener('waiting', waiting)
    video.addEventListener('stalled', waiting)
    video.addEventListener('error', videoError)
    window.addEventListener('pagehide', pageHide)
    connect()
    return () => {
      disposed = true
      reconnectRef.current = null
      clearTimeout(reconnectTimer)
      release(active)
      active = null
      video.removeEventListener('playing', playing)
      video.removeEventListener('waiting', waiting)
      video.removeEventListener('stalled', waiting)
      video.removeEventListener('error', videoError)
      window.removeEventListener('pagehide', pageHide)
      clearVideo()
    }
  }, [equipment.id, attempt, analysis, useSmooth, buffered])

  function retry() { setError(''); setState('connecting'); setAttempt(value => value + 1) }

  return <>
    <div className="camera-view-status" role="status">
      <Tag color={state === 'playing' ? 'success' : ['error', 'disconnected'].includes(state) ? 'error' : 'processing'}>
        {state === 'playing' ? 'Đang phát trực tiếp' : ['error', 'disconnected'].includes(state) ? 'Camera mất kết nối' : 'Đang kết nối camera...'}
      </Tag>
    </div>
    <div className="camera-video-container" ref={containerRef}>
      <video ref={videoRef} autoPlay muted playsInline disablePictureInPicture className={state === 'playing' ? 'camera-video' : 'camera-video camera-video-hidden'} aria-label={`Video camera ${equipment.name}`} />
      {state === 'playing' && (useSmooth || !analysis && risks.length > 0) && <canvas ref={overlayRef} className="camera-analysis-overlay" aria-hidden="true" />}
      {state === 'connecting' && <div className="camera-video-loading"><Spin /><span>Đang kết nối camera...</span></div>}
    </div>
    {error && <Alert type="error" showIcon title={error} action={<Button icon={<ReloadOutlined />} onClick={retry}>Thử lại</Button>} />}
  </>
})

export default function CameraViewModal({ equipment, onClose }) {
  if (!equipment) return null

  return <Modal open title={`Xem Camera — ${equipment.name}`} width={900} onCancel={onClose}
    footer={<Button onClick={onClose}>Đóng</Button>}>
    {equipment.status !== 'Active' ? <Alert type="warning" showIcon title="Thiết bị đang bị khóa hoặc đang bảo trì." /> :
      equipment.cameraSource === 'Browser' ? <BrowserCameraPreview name={equipment.name} /> : <CameraVideoPlayer equipment={equipment} />}
  </Modal>
}
