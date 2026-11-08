import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { Alert, Button, Card, Empty, Input, Modal, Pagination, Segmented, Select, Space, Spin, Tag, Typography } from 'antd'
import AlertList from '../../../features/safety/components/AlertList'
import { useMonitoringEvents } from '../../../features/safety/hooks/useMonitoringEvents'
import { useCameraList } from '../../../features/safety/hooks/useCameraList'
import { monitoringService } from '../../../features/safety/services/monitoringService'
import { CameraVideoPlayer } from '../../../features/equipments/components/CameraViewModal'
import '../../../features/equipments/styles/equipmentManagement.css'
import './Monitoring.css'

function CameraCard({ camera, enlarged, onEnlarge, highlighted, onRetry }) {
  const element = useRef(null)
  const [visible, setVisible] = useState(false)
  const [mode, setMode] = useState('live')
  const [now, setNow] = useState(Date.now)
  const ai = useQuery({ queryKey: ['safety', 'camera-analysis', camera.id], queryFn: async ({ signal }) => ({
    ...await monitoringService.analysis(camera.id, signal), receivedAt: Date.now(),
  }), enabled: visible && camera.source === 'RTSP', refetchInterval: 2000, retry: false })
  const aiFresh = !ai.isError && ai.data?.state === 'Connected' && now - ai.data.receivedAt +
    Date.parse(ai.data.serverTime) - Date.parse(ai.data.observedAt) < 6000
  useEffect(() => {
    if (!visible) return
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [visible])
  useEffect(() => {
    const observer = new IntersectionObserver(([entry]) => setVisible(entry.isIntersecting), { threshold: 0.1 })
    observer.observe(element.current)
    return () => observer.disconnect()
  }, [])
  return <Card ref={element} id={`camera-${camera.id}`} title={camera.name}
    style={highlighted ? { outline: `3px solid ${highlighted === 'CRITICAL' ? '#EF4444' : '#F97316'}` } : undefined}
    extra={<Button disabled={camera.status !== 'Active' || camera.source !== 'RTSP'} onClick={() => onEnlarge({ ...camera, analysis: mode === 'ai' })}>Phóng to</Button>}>
    <Space wrap><span>{camera.location || 'Chưa có vị trí'}</span><Tag>{camera.status}</Tag>
      <Tag color={camera.connectionStatus === 'Connected' ? 'green' : 'default'}>{camera.connectionStatus}</Tag>
      <Tag color={aiFresh ? 'processing' : 'default'}>{aiFresh ? 'AI đang giám sát' : ai.data?.state === 'NotConfigured' ? 'AI chưa cấu hình' : 'AI chưa có dữ liệu'}</Tag>
      {aiFresh && (ai.data.risks.some(x => x.severity === 'CRITICAL') ? <Tag color="#EF4444">Khẩn cấp</Tag> : ai.data.risks.length > 0 && <Tag color="#F97316">Chú ý</Tag>)}
      <Segmented value={mode} onChange={setMode} options={[{ label: 'Video trực tiếp', value: 'live' }, { label: 'Video AI', value: 'ai' }]} /></Space>
    {camera.source === 'Browser' ? <Alert type="info" title="Camera trình duyệt chỉ xem được tại máy đã đăng ký." /> :
      camera.status !== 'Active' ? <Alert type="warning" title="Camera hiện không khả dụng" /> :
        camera.connectionStatus !== 'Connected' ? <Alert type="warning" title="Camera hiện không khả dụng" action={<Button onClick={onRetry}>Thử lại</Button>} /> :
          mode === 'ai' && !aiFresh ? <Alert type="warning" title="AI hiện không khả dụng" action={<Button onClick={() => ai.refetch()}>Thử lại</Button>} /> :
            visible && !enlarged ? <CameraVideoPlayer equipment={camera} analysis={mode === 'ai'} /> : <div className="monitor-video-placeholder">Video tạm dừng</div>}
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
    <Modal open={!!enlarged} destroyOnHidden width={1100} title={enlarged?.name} footer={null} onCancel={() => setEnlarged(null)}>
      {enlarged && enlarged.status === 'Active' && enlarged.source === 'RTSP' && <CameraVideoPlayer equipment={enlarged} analysis={enlarged.analysis} />}
    </Modal>
  </div>
}
