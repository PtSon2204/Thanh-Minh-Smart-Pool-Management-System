import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Alert, Button, Card, Collapse, Input, Progress, Slider, Space, Spin, Typography, Upload, message } from 'antd'
import { monitoringService } from '../../../features/safety/services/monitoringService'
import AlertList from '../../../features/safety/components/AlertList'
import './Monitoring.css'

const jobLabels = {
  UPLOADING: 'Đang tải lên', QUEUED: 'Đang chờ phân tích', PROCESSING: 'Đang phân tích',
  RENDERING: 'Đang tạo video kết quả', COMPLETED: 'Đã hoàn tất', FAILED: 'Phân tích thất bại',
}

function uploadError(error) {
  if (error.code === 'ECONNABORTED') return 'Tải video lên quá lâu. Vui lòng kiểm tra kết nối và thử lại.'
  return error.response?.data?.message || 'Không tải lên được video. Vui lòng thử lại.'
}

function resultVideoError(error) {
  if (error.response?.status === 401) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để xem video.'
  if (error.response?.status === 403) return 'Bạn chưa được cấp quyền xem video kết quả.'
  if (error.response?.status === 404) return 'Không tìm thấy file video kết quả trên máy chủ.'
  if (error.code === 'ECONNABORTED') return 'Tải video quá lâu. Vui lòng kiểm tra kết nối và thử lại.'
  return 'Không tải được video kết quả. Vui lòng kiểm tra kết nối và thử lại.'
}

function resultVideoErrorDetails(error) {
  const status = error.response?.status
  return [status ? `HTTP ${status}` : null, error.code || error.name, error.message].filter(Boolean).join(' · ')
}

function ResultVideo({ jobId, seek }) {
  const video = useRef(null)
  const [source, setSource] = useState('')
  const [duration, setDuration] = useState(0)
  const [position, setPosition] = useState(0)
  const [error, setError] = useState('')
  const [errorDetails, setErrorDetails] = useState('')
  const [attempt, setAttempt] = useState(0)
  const alerts = useQuery({ queryKey: ['safety', 'video-timeline', jobId],
    queryFn: ({ signal }) => monitoringService.alerts({ sourceType: 'UPLOAD', jobId, pageSize: 100 }, signal) })
  const marks = {}
  for (const alert of alerts.data?.items || []) {
    if (alert.videoTimestamp != null && alert.videoTimestamp <= duration) marks[alert.videoTimestamp] = {
      label: <span title={`${alert.severity} · ${alert.videoTimestamp.toFixed(1)}s`} style={{ color: alert.severity === 'CRITICAL' ? '#EF4444' : '#F97316' }}>●</span>,
    }
  }
  useEffect(() => {
    const abort = new AbortController()
    monitoringService.playback(jobId, abort.signal).then(playback => {
      if (abort.signal.aborted) return
      setSource(playback.url); setError(''); setErrorDetails('')
    }).catch(error => {
      if (!abort.signal.aborted) {
        setError(resultVideoError(error)); setErrorDetails(resultVideoErrorDetails(error))
      }
    })
    return () => abort.abort()
  }, [jobId, attempt])
  useEffect(() => { if (video.current && seek) video.current.currentTime = seek.timestamp }, [seek])
  return <Space orientation="vertical" style={{ width: '100%' }}>
    {error ? <Alert type="error" title={error} description={errorDetails ? <Typography.Text type="secondary">Chi tiết lỗi: {errorDetails}</Typography.Text> : undefined}
      action={<Button onClick={() => { setError(''); setErrorDetails(''); setAttempt(x => x + 1) }}>Thử lại</Button>} /> :
      source ? <><video ref={video} src={source} controls playsInline preload="metadata" onTimeUpdate={() => setPosition(video.current.currentTime)}
        onError={event => {
          const code = event.currentTarget.error?.code
          setError(code === 3 || code === 4 ? 'Trình duyệt không đọc được định dạng video kết quả.' : 'Không phát được video. Bấm “Thử lại” để tạo lại liên kết phát.')
          setErrorDetails(code ? `MEDIA_ERR_${code}` : 'Không tải được luồng video')
        }}
        onLoadedMetadata={() => { setDuration(video.current.duration); if (seek) video.current.currentTime = seek.timestamp }} />
        <Typography.Text>Timeline cảnh báo · Cam: Chú ý · Đỏ: Khẩn cấp</Typography.Text>
        <Slider aria-label="Timeline cảnh báo video" min={0} max={duration || 1} step={0.1} value={position} marks={marks}
          onChange={timestamp => { video.current.currentTime = timestamp; setPosition(timestamp) }} />
        {alerts.isError && <Alert type="warning" title="Không tải được timeline cảnh báo" action={<Button onClick={() => alerts.refetch()}>Thử lại</Button>} />}
        {alerts.data?.totalCount > 100 && <Typography.Text>Timeline hiển thị 100 cảnh báo mới nhất. Danh sách bên dưới có đầy đủ theo trang.</Typography.Text>}
        <a href={`${source}&download=true`} download={`analysis-${jobId}.mp4`}>Tải video kết quả</a></> : <Spin />}
  </Space>
}

export default function VideoAnalysisPage() {
  const [file, setFile] = useState(null)
  const [jobId, setJobId] = useState(() => localStorage.getItem('safety-analysis-job') || '')
  const [resumeId, setResumeId] = useState('')
  const [uploadProgress, setUploadProgress] = useState(0)
  const [seek, setSeek] = useState(null)
  const status = useQuery({ queryKey: ['safety', 'configuration'], queryFn: ({ signal }) => monitoringService.status(signal) })
  const job = useQuery({ queryKey: ['safety', 'video-job', jobId], queryFn: ({ signal }) => monitoringService.job(jobId, signal),
    enabled: !!jobId, retry: false,
    refetchInterval: query => query.state.error || ['COMPLETED', 'FAILED'].includes(query.state.data?.status) ? false : 2000 })
  const upload = useMutation({ mutationFn: () => monitoringService.upload(file, progress => setUploadProgress(Math.round((progress.progress || 0) * 100))),
    onMutate: () => setUploadProgress(0),
    onSuccess: data => { setJobId(data.id); localStorage.setItem('safety-analysis-job', data.id); setFile(null); setSeek(null) },
    onError: error => message.error(uploadError(error)) })
  function openPrevious() {
    const id = resumeId.trim()
    if (/^[a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12}$/i.test(id)) {
      setJobId(id); setSeek(null); localStorage.setItem('safety-analysis-job', id)
      if (id === jobId) job.refetch()
    } else message.error('Mã lần phân tích không hợp lệ.')
  }
  return <div className="monitoring-page">
    <Typography.Title level={2}>Phân tích Video</Typography.Title>
    <Alert type="info" title="Kết quả từ video tải lên được lưu riêng và không phát cảnh báo cứu hộ trực tiếp." />
    {status.isError && <Alert type="error" title="Không kiểm tra được dịch vụ AI" action={<Button onClick={() => status.refetch()}>Thử lại</Button>} />}
    {status.data && !status.data.uploadConfigured && <Alert type="warning" title="Dịch vụ phân tích video chưa sẵn sàng" />}
    <Card title="Tải video lên">
      <Upload.Dragger accept=".mp4,.mov,.avi,.mkv" maxCount={1} fileList={file ? [file] : []} disabled={upload.isPending}
        beforeUpload={selected => {
          if (selected.size > (status.data?.maxVideoMegabytes || 200) * 1024 * 1024) message.error('Video vượt quá giới hạn dung lượng.')
          else setFile(selected)
          return false
        }} onRemove={() => setFile(null)}>
        <p>Chọn hoặc kéo thả video MP4, MOV, AVI, MKV</p>
        <p>Tối đa {status.data?.maxVideoMegabytes || 200} MB · {status.data?.maxVideoSeconds || 600} giây</p>
      </Upload.Dragger>
      {file && <p>{file.name} · {(file.size / 1024 / 1024).toFixed(2)} MB</p>}
      {upload.isPending && <Progress percent={uploadProgress} />}
      {upload.isError && <Alert type="error" showIcon title={uploadError(upload.error)} />}
      <Button type="primary" disabled={!file || !status.data?.uploadConfigured} loading={upload.isPending} onClick={() => upload.mutate()}>Bắt đầu phân tích</Button>
    </Card>
    {jobId && job.isPending && <Spin tip="Đang tải kết quả phân tích..." />}
    {job.isError && <Alert type="error" title="Không tìm thấy lần phân tích này hoặc bạn không có quyền truy cập" action={<Button onClick={() => job.refetch()}>Thử lại</Button>} />}
    {job.data && <Card title={job.data.originalName}>
      <p>Trạng thái: {jobLabels[job.data.status] || job.data.status}</p><Progress percent={Math.round(job.data.progress * 100)} status={job.data.status === 'FAILED' ? 'exception' : undefined} />
      {job.data.status === 'QUEUED' && <p>Video đã tải lên thành công và đang chờ đến lượt xử lý.</p>}
      {job.data.status === 'PROCESSING' && <p>AI đang phân tích video. Kết quả sẽ tự cập nhật tại đây.</p>}
      {job.data.error && <Alert type="error" title={job.data.error} />}
      {job.data.status === 'FAILED' && <p>Chọn lại video ở phần tải lên rồi bấm “Bắt đầu phân tích” để thử lại.</p>}
      <div className="analysis-reference"><Typography.Text type="secondary">Mã lần phân tích: </Typography.Text>
        <Typography.Text copyable={{ tooltips: ['Sao chép mã để xem lại', 'Đã sao chép'] }}>{jobId}</Typography.Text></div>
      {job.data.status === 'COMPLETED' && <ResultVideo key={jobId} jobId={jobId} seek={seek} />}
    </Card>}
    {job.data && <Card title="Cảnh báo trong video"><AlertList key={jobId} sourceType="UPLOAD" jobId={jobId}
      onDetail={alert => setSeek({ timestamp: alert.videoTimestamp || 0, id: alert.id })} /></Card>}
    <Collapse items={[{ key: 'previous', label: 'Xem lại kết quả bằng mã', children: <Space orientation="vertical" style={{ width: '100%' }}>
      <Typography.Text type="secondary">Dán mã của lần phân tích đã tải lên để xem tiến độ hoặc kết quả. Khi tải video mới, kết quả sẽ tự mở.</Typography.Text>
      <Space.Compact block><Input aria-label="Mã lần phân tích" placeholder="Dán mã lần phân tích trước" value={resumeId}
        onChange={e => setResumeId(e.target.value)} onPressEnter={openPrevious} />
        <Button disabled={!resumeId.trim()} onClick={openPrevious}>Xem kết quả</Button></Space.Compact>
    </Space> }]} />
  </div>
}
