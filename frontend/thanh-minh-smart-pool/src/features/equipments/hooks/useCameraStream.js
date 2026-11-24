import { useEffect } from 'react'
import equipmentService from '../services/equipmentService'
import apiClient from '../../../services/apiClient'
import { getAccessToken } from '../../auth/store/authStore'

const ICE_GATHERING_TIMEOUT_MS = 10000
const CONNECTION_DEADLINE_MS = 20000
const PLAYBACK_STABLE_INTERVAL_MS = 10000
const PLAYBACK_STALL_GRACE_MS = 700
const DECODE_PROGRESS_TIMEOUT_MS = 5000
const VIEW_STATUS_POLL_INTERVAL_MS = 2000

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
    timer = setTimeout(() => finish(new Error('ICE gathering timeout')), ICE_GATHERING_TIMEOUT_MS)
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

// Each effect instance owns its peer, backend view, retries, listeners and timers.
export function useCameraStream({ videoRef, reconnectRef, equipmentId, attempt, analysis,
  useSmooth, buffered, setError, setState }) {
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
        equipmentService.closeCameraView(equipmentId, connection.viewId).catch(() => {})
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
      connection.stable = setTimeout(() => { retries = 0 }, PLAYBACK_STABLE_INTERVAL_MS)
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
      }, PLAYBACK_STALL_GRACE_MS)
    }

    function videoError() { if (active) fail(active, 'Không thể kết nối đến camera') }
    function pageHide() {
      if (!active?.viewId) return
      const url = new URL(`/api/equipments/${equipmentId}/camera/views/${active.viewId}`, apiClient.defaults.baseURL || window.location.origin)
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
        const config = await equipmentService.getCameraViewConfiguration(equipmentId, connection.abort.signal)
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
          view = await equipmentService.openCameraView(equipmentId, { type: 'offer', sdp: peer.localDescription.sdp, analysis, smooth: useSmooth }, connection.abort.signal)
        } catch (failure) {
          if (!analysis || failure.response?.data?.code !== 'NotConfigured' || !current()) throw failure
          // AI chưa cấu hình: vẫn cho xem camera gốc, không vẽ box metadata lên video khác frame.
          view = await equipmentService.openCameraView(equipmentId, { type: 'offer', sdp: peer.localDescription.sdp, analysis: false }, connection.abort.signal)
        }
        if (!current()) { equipmentService.closeCameraView(equipmentId, view.viewId).catch(() => {}); return }
        connection.viewId = view.viewId
        await peer.setRemoteDescription({ type: view.type, sdp: view.sdp })
        connection.deadline = setTimeout(() => fail(connection, 'Không thể kết nối đến camera'), CONNECTION_DEADLINE_MS)
        let polling = false
        let lastFrames = 0
        let lastProgress = Date.now()
        connection.poll = setInterval(async () => {
          if (!current() || polling) return
          polling = true
          try {
            const status = await equipmentService.getCameraViewStatus(equipmentId, view.viewId, connection.abort.signal)
            if (!current()) return
            if (status.state === 'Disconnected') { fail(connection, 'Camera hiện không khả dụng'); return }
            const stats = await peer.getStats()
            if (!current()) return
            stats.forEach(stat => {
              if (stat.type === 'inbound-rtp' && (stat.kind === 'video' || stat.mediaType === 'video')) {
                if (stat.framesDecoded > lastFrames) { lastFrames = stat.framesDecoded; lastProgress = Date.now() }
                else if (lastFrames > 0 && Date.now() - lastProgress > DECODE_PROGRESS_TIMEOUT_MS) fail(connection, 'Camera hiện không khả dụng')
              }
            })
          } catch (e) {
            if (current()) fail(connection, cameraError(e.response?.status), ![400, 401, 403, 404].includes(e.response?.status))
          } finally { polling = false }
        }, VIEW_STATUS_POLL_INTERVAL_MS)
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
  }, [equipmentId, attempt, analysis, useSmooth, buffered, videoRef, reconnectRef, setError, setState])

}
