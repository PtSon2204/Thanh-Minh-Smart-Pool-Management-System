import { memo, useCallback, useEffect, useRef, useState } from 'react'
import { Alert, Button, Modal, Spin, Tag } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'
import BrowserCameraPreview from './BrowserCameraPreview'
import { drawVideoRisks } from '../../safety/utils/drawVideoRisks'
import { useCameraPlayout } from '../hooks/useCameraPlayout'
import { useCameraStream } from '../hooks/useCameraStream'

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

  const paintOverlay = useCallback(() => {
    drawVideoRisks(overlayRef.current, videoRef.current, risksRef.current, playoutRef.current?.current?.frame)
  }, [])

  // Áp box mới ngay khi nhận; không cộng thêm thời gian nội suy vào độ trễ AI.
  useEffect(() => {
    if (useSmooth && state === 'playing') return
    risksRef.current = !analysis && state === 'playing' ? risks : []
    paintOverlay()
  }, [risks, equipment.id, state, analysis, useSmooth, paintOverlay])

  useCameraPlayout({ videoRef, playoutRef, risksRef, delayRef, samplerRef, paintOverlay,
    useSmooth, buffered, state, cameraId: equipment.id, setExactFallback, setBufferUnavailable })

  // Canvas theo kích thước container (resize window / đổi cột grid).
  useEffect(() => {
    if (!containerRef.current) return
    const observer = new ResizeObserver(paintOverlay)
    observer.observe(containerRef.current)
    return () => observer.disconnect()
  }, [paintOverlay])

  // Video có metadata mới (kích thước nội tại đổi) thì vẽ lại vùng letterbox.
  useEffect(() => {
    const video = videoRef.current
    if (!video) return
    const onMeta = () => paintOverlay()
    video.addEventListener('loadedmetadata', onMeta)
    return () => video.removeEventListener('loadedmetadata', onMeta)
  }, [paintOverlay])

  useCameraStream({ videoRef, reconnectRef, equipmentId: equipment.id, attempt, analysis,
    useSmooth, buffered, setError, setState })

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
