import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { Alert, Button, Card, Empty, Input, Pagination, Segmented, Select, Space, Spin, Tag, Typography } from 'antd'
import AlertList from '../../../features/safety/components/AlertList'
import EnlargedCameraModal from '../../../features/safety/components/EnlargedCameraModal'
import PairedCameraPlayer from '../../../features/safety/components/PairedCameraPlayer'
import { useMonitoringEvents } from '../../../features/safety/hooks/useMonitoringEvents'
import { useCameraList } from '../../../features/safety/hooks/useCameraList'
import { useCameraRisks } from '../../../features/safety/hooks/useCameraRisks'
import { monitoringService } from '../../../features/safety/services/monitoringService'
import { CameraVideoPlayer } from '../../../features/equipments/components/CameraViewModal'
import '../../../features/equipments/styles/equipmentManagement.css'
import './Monitoring.css'
function CameraCard({ camera, enlarged, onEnlarge, highlighted, onRetry }) {
  const element = useRef(null)
  const [visible, setVisible] = useState(false)
  const [mode, setMode] = useState('live')
  const { ai, fresh, risks } = useCameraRisks(camera.id, visible && camera.source === 'RTSP')
  useEffect(() => {
    const observer = new IntersectionObserver(([entry]) => setVisible(entry.isIntersecting), { threshold: 0.1 })
    observer.observe(element.current)
    return () => observer.disconnect()
  }, [])
  return <Card ref={element} id={`camera-${camera.id}`} title={camera.name}
    style={highlighted ? { outline: `3px solid ${highlighted === 'CRITICAL' ? '#EF4444' : '#F97316'}` } : undefined}
    extra={<Button disabled={camera.status !== 'Active' || camera.source !== 'RTSP'} onClick={() => onEnlarge({ ...camera, analysisMode: mode })}>Phóng to</Button>}>
    <Space wrap><span>{camera.location || 'Chưa có vị trí'}</span><Tag>{camera.status}</Tag>
      <Tag color={camera.connectionStatus === 'Connected' ? 'green' : 'default'}>{camera.connectionStatus}</Tag>
      <Tag color={fresh ? 'processing' : 'default'}>{fresh ? 'AI đang giám sát' : ai.data?.state === 'NotConfigured' ? 'AI chưa cấu hình' : 'AI chưa có dữ liệu'}</Tag>
      {fresh && (risks.some(x => x.severity === 'CRITICAL') ? <Tag color="#EF4444">Khẩn cấp</Tag> : risks.length > 0 && <Tag color="#F97316">Chú ý</Tag>)}
      <Segmented value={mode} onChange={setMode} options={[{ label: 'Video trực tiếp', value: 'live' }, { label: 'Video AI', value: 'ai' }]} /></Space>
    {camera.source === 'Browser' ? <Alert type="info" title="Camera trình duyệt chỉ xem được tại máy đã đăng ký." /> :
      camera.status !== 'Active' ? <Alert type="warning" title="Camera hiện không khả dụng" /> :
        camera.connectionStatus !== 'Connected' ? <Alert type="warning" title="Camera hiện không khả dụng" action={<Button onClick={onRetry}>Thử lại</Button>} /> :
          mode === 'ai' && !fresh ? <Alert type="warning" title="AI hiện không khả dụng" action={<Button onClick={() => ai.refetch()}>Thử lại</Button>} /> :
            visible && !enlarged ? mode === 'ai' ? <PairedCameraPlayer camera={camera} /> : <CameraVideoPlayer equipment={camera} /> : <div className="monitor-video-placeholder">Video tạm dừng</div>}
  </Card>
}

export default function CameraListPage() {
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
    {connection !== 'connected' && <Alert type="warning" title="Kết nối cảnh báo realtime chưa sẵn sàng. Danh sách vẫn được tải lại định kỳ." action={<Button onClick={() => window.location.reload()}>Kết nối lại</Button>} />}
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
        enlarged={enlarged?.id === camera.id} onEnlarge={setEnlarged} onRetry={() => cameras.refetch()} highlighted={highlight?.cameraId === camera.id ? highlight.severity : null} />)}</div>
      <Pagination current={filters.page} total={cameras.data.totalCount} pageSize={filters.pageSize} showSizeChanger={false}
        onChange={page => setFilters(current => ({ ...current, page }))} />
    </>}
    <Card title="Cảnh báo camera LIVE"><AlertList onDetail={row => {
      setSearch(''); setFilters({ page: 1, pageSize: 4, cameraId: row.cameraId }); setHighlight(row)
    }} /></Card>
    <EnlargedCameraModal camera={enlarged} onClose={() => setEnlarged(null)} />
  </div>
}
