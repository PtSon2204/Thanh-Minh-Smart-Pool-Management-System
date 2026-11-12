import { useEffect, useState } from 'react'
import { Alert, Spin } from 'antd'
import { monitoringService } from '../services/monitoringService'

// Hiển thị pixel đã được AI vẽ khung, không ghép metadata lên WebRTC ở thời điểm khác.
export default function PairedCameraPlayer({ camera }) {
  const [frame, setFrame] = useState(null)
  const [unavailable, setUnavailable] = useState(false)
  useEffect(() => {
    let cancelled = false
    let timer
    let currentUrl
    let lastFrameId
    let expiresAt = 0
    const abort = new AbortController()
    const clear = () => {
      if (currentUrl) URL.revokeObjectURL(currentUrl)
      currentUrl = null
      lastFrameId = null
      setFrame(null)
    }
    const tick = async () => {
      try {
        const metadata = await monitoringService.analysis(camera.id, abort.signal)
        const age = Date.parse(metadata.serverTime) - Date.parse(metadata.observedAt)
        if (metadata.state !== 'Connected' || !Number.isFinite(age) || age >= 6000) {
          if (!cancelled) { clear(); setUnavailable(true) }
        } else if (metadata.frameId !== lastFrameId) {
          const started = Date.now()
          const blob = await monitoringService.analysisFrame(camera.id, metadata.frameId, abort.signal)
          if (!cancelled && age + Date.now() - started < 6000) {
            const nextUrl = URL.createObjectURL(blob)
            const previous = currentUrl
            currentUrl = nextUrl
            lastFrameId = metadata.frameId
            expiresAt = started + 6000 - age
            setFrame({ cameraId: camera.id, url: nextUrl })
            setUnavailable(false)
            if (previous) URL.revokeObjectURL(previous)
          }
        }
      } catch {
        if (!cancelled && Date.now() >= expiresAt) { clear(); setUnavailable(true) }
      }
      if (!cancelled) {
        if (currentUrl && Date.now() >= expiresAt) { clear(); setUnavailable(true) }
        timer = setTimeout(tick, 500)
      }
    }
    void tick()
    return () => { cancelled = true; abort.abort(); clearTimeout(timer); if (currentUrl) URL.revokeObjectURL(currentUrl) }
  }, [camera.id])
  if (frame?.cameraId === camera.id) return <img src={frame.url} alt={`Khung hình AI: ${camera.name}`} style={{ display: 'block', width: '100%', background: '#000' }} />
  return unavailable ? <Alert type="warning" title="AI chưa có khung hình mới. Đang kết nối lại…" /> : <Spin tip="Đang tải khung hình AI…" />
}
