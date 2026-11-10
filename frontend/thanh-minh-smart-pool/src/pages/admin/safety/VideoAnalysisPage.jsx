import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Alert, Button, Card, Collapse, Input, Progress, Space, Spin, Typography, Upload, message } from 'antd'
import { monitoringService } from '../../../features/safety/services/monitoringService'
import VideoAnalysisPlayer from '../../../features/safety/components/VideoAnalysisPlayer'
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

export default function VideoAnalysisPage() {
  const [searchParams] = useSearchParams()
  const [file, setFile] = useState(null)
  const [jobId, setJobId] = useState(() => searchParams.get('job') || localStorage.getItem('safety-analysis-job') || '')
  const [seek, setSeek] = useState(() => searchParams.get('seek') != null
    ? { timestamp: Number(searchParams.get('seek')) || 0, fromAlert: true } : null)
  const [resumeId, setResumeId] = useState('')
  const [uploadProgress, setUploadProgress] = useState(0)
  const [autoPlay, setAutoPlay] = useState(false)
  const status = useQuery({ queryKey: ['safety', 'configuration'], queryFn: ({ signal }) => monitoringService.status(signal) })
  const job = useQuery({ queryKey: ['safety', 'video-job', jobId], queryFn: ({ signal }) => monitoringService.job(jobId, signal),
    enabled: !!jobId, retry: false,
    refetchInterval: query => query.state.error || ['COMPLETED', 'FAILED'].includes(query.state.data?.status) ? false : 2000 })
  const upload = useMutation({ mutationFn: () => monitoringService.upload(file, progress => setUploadProgress(Math.round((progress.progress || 0) * 100))),
    onMutate: () => setUploadProgress(0),
    onSuccess: data => { setAutoPlay(true); setJobId(data.id); localStorage.setItem('safety-analysis-job', data.id); setFile(null); setSeek(null) },
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
    <Alert type="info" title="Video tự phát cùng box AI và popup theo thời điểm đang xem. Cảnh báo từ video tải lên được lưu riêng." />
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
      {['QUEUED', 'PROCESSING', 'RENDERING', 'COMPLETED'].includes(job.data.status)
        && <VideoAnalysisPlayer key={jobId} jobId={jobId} seek={seek} status={job.data.status} autoPlay={autoPlay} />}
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
