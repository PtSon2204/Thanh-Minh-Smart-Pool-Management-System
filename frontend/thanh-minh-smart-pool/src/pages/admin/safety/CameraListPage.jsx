import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useLocation, useSearchParams } from 'react-router-dom'
import { Alert, Button, Card, Empty, Input, Pagination, Select, Space, Spin, Tag, Typography } from 'antd'
import EnlargedCameraModal from '../../../features/safety/components/EnlargedCameraModal'
import { CameraVideoPlayer } from '../../../features/equipments/components/CameraViewModal'
import { useMonitoringEvents } from '../../../features/safety/hooks/useMonitoringEvents'
import { useCameraList } from '../../../features/safety/hooks/useCameraList'
import { useCameraRisks } from '../../../features/safety/hooks/useCameraRisks'
import { monitoringService } from '../../../features/safety/services/monitoringService'
import { startSignalRConnection } from '../../../services/signalrClient'
import '../../../features/equipments/styles/equipmentManagement.css'
import './Monitoring.css'
function CameraCard({ camera, onEnlarge, highlighted, onRetry }) {
  const element = useRef(null)
  const [visible, setVisible] = useState(false)
  const { ai, fresh, risks, sampleRisks, sampleRisksWithDiagnostics, getPlayoutDelay } = useCameraRisks(camera.id, visible && camera.source === 'RTSP')
  useEffect(() => {
    const observer = new IntersectionObserver(([entry]) => setVisible(entry.isIntersecting), { threshold: 0.1 })
    observer.observe(element.current)
    return () => observer.disconnect()
  }, [])
  // Layout cố định theo vùng (grid-template-areas): mọi camera có cùng vị trí tên/tag/video,
  // khung video luôn căn hàng ngang không bị đẩy xuống theo chiều dài nhãn bên trên.
  return <Card ref={element} id={`camera-${camera.id}`} className="camera-card"
    style={highlighted ? { outline: `3px solid ${highlighted === 'CRITICAL' ? '#EF4444' : '#F97316'}` } : undefined}
    extra={<Button disabled={camera.status !== 'Active' || camera.source !== 'RTSP'} onClick={() => onEnlarge(camera)}>Phóng to</Button>}>
    <div className="camera-card-media">
      <div className="camera-card-title">
        <Typography.Text strong>{camera.name}</Typography.Text>
        <Typography.Text type="secondary" className="camera-card-location">{camera.location || 'Chưa có vị trí'}</Typography.Text>
      </div>
      <div className="camera-card-tags">
        <Space wrap><Tag>{camera.status}</Tag>
          <Tag color={camera.connectionStatus === 'Connected' ? 'green' : 'default'}>{camera.connectionStatus}</Tag>
          <Tag color={fresh ? 'success' : 'default'}>{fresh ? 'Camera AI đang giám sát' : ai.data?.state === 'NotConfigured' ? 'AI chưa cấu hình' : 'Camera AI đang chờ dữ liệu'}</Tag>
          {fresh && (risks.some(x => x.severity === 'CRITICAL') ? <Tag color="#EF4444">Khẩn cấp</Tag> : risks.length > 0 && <Tag color="#F97316">Chú ý</Tag>)}
        </Space>
      </div>
      <div className="camera-card-video">
        {camera.source === 'Browser' ? <Alert type="info" title="Camera trình duyệt chỉ xem được tại máy đã đăng ký." /> :
          camera.status !== 'Active' ? <Alert type="warning" title="Camera hiện không khả dụng" /> :
            <>
              {camera.connectionStatus !== 'Connected' && <Alert type="warning" title="Đang chờ camera kết nối lại" action={<Button onClick={onRetry}>Tải lại trạng thái</Button>} />}
              {visible ? <CameraVideoPlayer equipment={camera} analysis smooth={camera.smoothPlayback} sampleRisks={sampleRisks} sampleRisksWithDiagnostics={sampleRisksWithDiagnostics} getPlayoutDelay={getPlayoutDelay} /> : <div className="monitor-video-placeholder">Camera AI tạm dừng</div>}
            </>}
      </div>
      <div className="camera-card-foot" />
    </div>
  </Card>
}

export default function CameraListPage() {
  const location = useLocation()
  // Mỗi lần mở từ popup khởi tạo đúng camera, kể cả khi vẫn ở route danh sách.
  return <CameraListContent key={location.key} />
}

function CameraListContent() {
  const [params, setParams] = useSearchParams()
  const [filters, setFilters] = useState(() => ({ page: 1, pageSize: 4, cameraId: params.get('cameraId') || undefined }))
  const [search, setSearch] = useState('')
  const [enlarged, setEnlarged] = useState(null)
  const [highlight, setHighlight] = useState(() => params.get('cameraId') ? { cameraId: params.get('cameraId'), severity: params.get('severity') } : null)
  const connection = useMonitoringEvents()
  const cameras = useCameraList(filters)
  const areas = useQuery({ queryKey: ['safety', 'camera-areas'], queryFn: ({ signal }) => monitoringService.areas(signal) })
  const change = patch => setFilters(current => ({ ...current, ...patch, page: 1 }))
  useEffect(() => {
    const timer = setTimeout(() => setFilters(current => ({ ...current, search: search || undefined, page: 1 })), 350)
    return () => clearTimeout(timer)
  }, [search])
  useEffect(() => {
    if (!highlight || !cameras.data?.items.some(x => x.id === highlight.cameraId)) return
    document.getElementById(`camera-${highlight.cameraId}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }, [highlight, cameras.data])
  return <div className="monitoring-page">
    <Typography.Title level={2}>Danh sách camera</Typography.Title>
    {connection !== 'connected' && <Alert type="warning" title="Kết nối cảnh báo realtime chưa sẵn sàng. Danh sách vẫn được tải lại định kỳ." action={<Button onClick={() => void startSignalRConnection()}>Kết nối lại</Button>} />}
    <Space wrap className="monitoring-filters">
      <Input aria-label="Tìm camera" placeholder="Tên hoặc vị trí camera" allowClear value={search} onChange={e => setSearch(e.target.value)} />
      <Select aria-label="Trạng thái thiết bị" placeholder="Trạng thái thiết bị" allowClear value={filters.status}
        options={['Active', 'Maintenance', 'Inactive'].map(value => ({ value, label: value }))} onChange={status => change({ status })} />
      <Select aria-label="Khu vực" placeholder="Khu vực" allowClear loading={areas.isPending} value={filters.area}
        options={(areas.data || []).map(value => ({ value, label: value }))} onChange={area => change({ area })} />
      <Button onClick={() => { setSearch(''); setFilters({ page: 1, pageSize: 4 }); setHighlight(null); setParams({}) }}>Đặt lại</Button>
    </Space>
    {areas.isError && <Alert type="warning" title="Không tải được danh sách khu vực" action={<Button onClick={() => areas.refetch()}>Thử lại</Button>} />}
    {cameras.isPending ? <Spin /> : cameras.isError ? <Alert type="error" title="Không tải được danh sách camera" action={<Button onClick={() => cameras.refetch()}>Thử lại</Button>} /> : <>
      {!cameras.data.items.length && <Empty description={filters.search || filters.status || filters.area || filters.cameraId ? 'Không tìm thấy camera phù hợp' : 'Chưa có camera'} />}
      <div className="camera-grid">{cameras.data.items.map(camera => <CameraCard key={camera.id} camera={camera}
        onEnlarge={setEnlarged} onRetry={() => cameras.refetch()} highlighted={highlight?.cameraId === camera.id ? highlight.severity : null} />)}</div>
      <Pagination current={filters.page} total={cameras.data.totalCount} pageSize={filters.pageSize} showSizeChanger={false}
        onChange={page => setFilters(current => ({ ...current, page }))} />
    </>}
    <EnlargedCameraModal camera={enlarged} onClose={() => setEnlarged(null)} />
  </div>
}
