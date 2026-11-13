import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Alert, Button, Slider, Space, Spin, Typography, message } from 'antd'
import { monitoringService } from '../services/monitoringService'
import { useVideoFrameAnalysis } from '../hooks/useVideoFrameAnalysis'

export default function VideoAnalysisPlayer({ jobId, seek, status, autoPlay = false }) {
  const video = useRef(null)
  const canvas = useRef(null)
  const restore = useRef(null)
  const [source, setSource] = useState('')
  const [duration, setDuration] = useState(0)
  const [error, setError] = useState('')
  const [attempt, setAttempt] = useState(0)
  const [downloading, setDownloading] = useState(false)
  const sync = useVideoFrameAnalysis(jobId, video, canvas, !!source && !error, autoPlay)
  const alerts = useQuery({ queryKey: ['safety', 'video-timeline', jobId],
    queryFn: ({ signal }) => monitoringService.alerts({ sourceType: 'UPLOAD', jobId, pageSize: 100 }, signal),
    refetchInterval: status === 'COMPLETED' ? false : 1000 })
  useEffect(() => {
    const abort = new AbortController()
    let timer
    monitoringService.playback(jobId, abort.signal, { original: !sync.legacy }).then(playback => {
      if (abort.signal.aborted) return
      if (video.current?.readyState > 0) restore.current = { time: video.current.currentTime, playing: !video.current.paused }
      setSource(playback.url)
      setError('')
      timer = setTimeout(() => setAttempt(value => value + 1), Math.max(1000, Date.parse(playback.expiresAt) - Date.now() - 60000))
    }).catch(failure => {
      if (!abort.signal.aborted) setError(failure.response?.data?.message || 'Không tải được video. Vui lòng thử lại.')
    })
    return () => { abort.abort(); clearTimeout(timer) }
  }, [jobId, attempt, sync.legacy])
  useEffect(() => {
    if (video.current && seek) video.current.currentTime = Math.max(0, seek.timestamp)
  }, [seek])

  async function downloadResult() {
    setDownloading(true)
    try {
      const playback = await monitoringService.playback(jobId)
      const anchor = document.createElement('a')
      anchor.href = `${playback.url}&download=true`
      anchor.download = `analysis-${jobId}.mp4`
      anchor.click()
    } catch { message.error('Không tải được video kết quả. Vui lòng thử lại.') }
    finally { setDownloading(false) }
  }
  const marks = {}
  for (const alert of alerts.data?.items || []) {
    if (alert.videoTimestamp != null && alert.videoTimestamp <= duration) marks[alert.videoTimestamp] = {
      label: <span title={`${alert.severity} · ${alert.videoTimestamp.toFixed(1)}s`}
        style={{ color: alert.severity === 'CRITICAL' ? '#EF4444' : '#F97316' }}>●</span>,
    }
  }
  return <Space orientation="vertical" style={{ width: '100%' }}>
    {error && <Alert type="error" title={error} action={<Button onClick={() => setAttempt(value => value + 1)}>Thử lại</Button>} />}
    {sync.error && <Alert type="warning" title={sync.error} />}
    {sync.legacy && <Alert type="info" title="Đang phát kết quả đã vẽ khung của lần phân tích cũ. Tải lại video để sử dụng popup đồng bộ theo từng frame." />}
    <div className="safety-video-stage">
      <video ref={video} src={source || undefined} controls muted playsInline preload="auto"
        aria-label="Video phân tích đồng bộ cảnh báo"
        style={{ display: 'block', width: '100%', aspectRatio: '16 / 9', objectFit: 'contain' }}
        onLoadedMetadata={() => {
          const element = video.current
          setDuration(element.duration)
          element.currentTime = restore.current?.time ?? seek?.timestamp ?? 0
          if (restore.current?.playing || (sync.legacy && autoPlay)) void element.play().catch(() => {})
          restore.current = null
        }}
        onError={event => {
          if (event.currentTarget.error) setError('Không phát được video. Bấm “Thử lại” để tạo lại liên kết phát.')
        }} />
      <canvas ref={canvas} aria-hidden="true" style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', pointerEvents: 'none' }} />
      {(!source || (sync.buffering && !sync.legacy)) && !error && <div role="status" aria-live="polite"
        style={{ position: 'absolute', inset: '0 0 40px', minHeight: 100, display: 'grid', placeContent: 'center', gap: 12,
          background: 'rgba(0,0,0,.55)', color: '#fff', pointerEvents: 'none', textAlign: 'center' }}>
        <Spin /><span>Đang đồng bộ hình ảnh và cảnh báo AI…</span>
      </div>}
    </div>
    <Typography.Text>Box và popup theo thời điểm đang phát · Cam: Chú ý · Đỏ: Khẩn cấp</Typography.Text>
    <Slider aria-label="Timeline cảnh báo video" min={0} max={duration || 1} step={0.01} value={sync.position} marks={marks}
      disabled={!duration} onChange={timestamp => { if (video.current) video.current.currentTime = timestamp }} />
    {alerts.isError && <Alert type="warning" title="Không tải được timeline cảnh báo" action={<Button onClick={() => alerts.refetch()}>Thử lại</Button>} />}
    {alerts.data?.totalCount > 100 && <Typography.Text>Timeline hiển thị 100 cảnh báo mới nhất; danh sách bên dưới có đầy đủ theo trang.</Typography.Text>}
    {status === 'COMPLETED' && <Button loading={downloading} onClick={downloadResult}>Tải video kết quả</Button>}
    {sync.warnings.length > 0 && <div className="safety-video-popup" role="alertdialog" aria-label="Cảnh báo trong video">
      {sync.warnings.map(warning => <Alert key={warning.popupId} type={warning.severity === 'CRITICAL' ? 'error' : 'warning'} showIcon closable onClose={() => sync.dismissWarning(warning.popupId)}
        title="Cảnh báo trong video" description={<Space orientation="vertical">
          <strong>{warning.severity === 'CRITICAL' ? 'Nguy cơ khẩn cấp' : 'Phát hiện dấu hiệu nguy cơ'}</strong>
          <span>Thời điểm: {warning.videoTimestamp.toFixed(2)} giây · Đối tượng {warning.trackId}</span>
        </Space>} />)}
    </div>}
  </Space>
}
