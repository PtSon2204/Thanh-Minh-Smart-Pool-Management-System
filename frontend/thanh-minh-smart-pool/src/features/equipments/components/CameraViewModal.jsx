import { useEffect, useRef, useState } from 'react'
import { Alert, Button, Modal, Spin, Tag } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'
import equipmentService from '../services/equipmentService'
import apiClient from '../../../services/apiClient'
import { getAccessToken } from '../../auth/store/authStore'
import BrowserCameraPreview from './BrowserCameraPreview'

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

function CameraVideoPlayer({ equipment }) {
  const videoRef = useRef(null)
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState('connecting')
  const [error, setError] = useState('')

  useEffect(() => {
    const video = videoRef.current
    let disposed = false
    let active = null
    let reconnectTimer
    let retries = 0

    function release(connection) {
      if (!connection || connection.closed) return
      connection.closed = true
      connection.abort.abort()
      clearInterval(connection.poll)
      clearTimeout(connection.deadline)
      clearTimeout(connection.stable)
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
      if (recoverable && retries < 3) {
        reconnectTimer = setTimeout(() => { retries++; connect() }, [1000, 2000, 4000][retries])
      }
    }

    function playing() {
      const connection = active
      if (!connection || connection.closed || disposed) return
      clearTimeout(connection.deadline)
      clearTimeout(connection.stable)
      connection.stable = setTimeout(() => { retries = 0 }, 10000)
      setError('')
      setState('playing')
    }

    function waiting() {
      const connection = active
      if (!connection || connection.closed || disposed) return
      setState('connecting') // Hide the last image as soon as playback stalls.
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
          if (typeof transceiver.receiver.playoutDelayHint !== 'undefined') transceiver.receiver.playoutDelayHint = 0
          video.play().catch(() => { if (current()) fail(connection, 'Trình duyệt không cho phép tự động phát video.', false) })
        }
        peer.onconnectionstatechange = () => {
          if (current() && ['failed', 'disconnected', 'closed'].includes(peer.connectionState))
            fail(connection, 'Không thể kết nối đến camera')
        }
        await peer.setLocalDescription(await peer.createOffer())
        await waitForIce(peer, connection.abort.signal)
        if (!current()) return
        const view = await equipmentService.openCameraView(equipment.id, { type: 'offer', sdp: peer.localDescription.sdp }, connection.abort.signal)
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
    video.addEventListener('waiting', waiting)
    video.addEventListener('stalled', waiting)
    video.addEventListener('error', videoError)
    window.addEventListener('pagehide', pageHide)
    connect()
    return () => {
      disposed = true
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
  }, [equipment.id, attempt])

  function retry() { setError(''); setState('connecting'); setAttempt(value => value + 1) }

  return <>
    <div className="camera-view-status" role="status">
      <Tag color={state === 'playing' ? 'success' : ['error', 'disconnected'].includes(state) ? 'error' : 'processing'}>
        {state === 'playing' ? 'Đang phát trực tiếp' : ['error', 'disconnected'].includes(state) ? 'Camera mất kết nối' : 'Đang kết nối camera...'}
      </Tag>
    </div>
    <div className="camera-video-container">
      <video ref={videoRef} autoPlay muted playsInline disablePictureInPicture className={state === 'playing' ? 'camera-video' : 'camera-video camera-video-hidden'} aria-label={`Video camera ${equipment.name}`} />
      {state === 'connecting' && <div className="camera-video-loading"><Spin /><span>Đang kết nối camera...</span></div>}
    </div>
    {error && <Alert type="error" showIcon title={error} action={<Button icon={<ReloadOutlined />} onClick={retry}>Thử lại</Button>} />}
  </>
}

export default function CameraViewModal({ equipment, onClose }) {
  return <Modal open title={`Xem Camera — ${equipment.name}`} width={900} onCancel={onClose}
    footer={<Button onClick={onClose}>Đóng</Button>}>
    {equipment.status !== 'Active' ? <Alert type="warning" showIcon title="Thiết bị đang bị khóa hoặc đang bảo trì." /> :
      equipment.cameraSource === 'Browser' ? <BrowserCameraPreview name={equipment.name} /> : <CameraVideoPlayer equipment={equipment} />}
  </Modal>
}
