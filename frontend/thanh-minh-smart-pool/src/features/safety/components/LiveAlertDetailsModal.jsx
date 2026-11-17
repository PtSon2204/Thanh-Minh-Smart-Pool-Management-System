import { Alert, Button, Collapse, Descriptions, Image, Modal, Space, Spin, Tag } from 'antd'
import { useQuery } from '@tanstack/react-query'
import { CameraVideoPlayer } from '../../equipments/components/CameraViewModal'
import { useCameraList } from '../hooks/useCameraList'
import { useCameraRisks } from '../hooks/useCameraRisks'
import { useMonitoringEvents } from '../hooks/useMonitoringEvents'
import { monitoringService } from '../services/monitoringService'
import AlertActionButtons from './AlertActionButtons'
import '../../equipments/styles/equipmentManagement.css'

export default function LiveAlertDetailsModal({ alert, onClose }) {
  useMonitoringEvents()
  const details = useQuery({ queryKey: ['safety', 'alert', alert.id],
    queryFn: ({ signal }) => monitoringService.alert(alert.id, signal), initialData: alert,
    refetchInterval: 2000, retry: false })
  const current = details.data || alert
  const cameras = useCameraList({ cameraId: current.cameraId, page: 1, pageSize: 1 })
  const camera = cameras.data?.items?.find(item => item.id === current.cameraId)
  const canPlay = camera?.status === 'Active' && camera?.source === 'RTSP'
  const { fresh, sampleRisks, sampleRisksWithDiagnostics, getPlayoutDelay } = useCameraRisks(camera?.id, canPlay)
  const critical = ['CRITICAL', 'EMERGENCY'].includes(current.severity)
  const status = { PENDING: 'Chờ xác nhận', ACKNOWLEDGED: 'Đã xác nhận', CANCELLED: 'Đã hủy' }[current.status] || current.status

  return <Modal open destroyOnHidden className="safety-modal safety-live-detail" width={1000} zIndex={1200}
    title={`Camera LIVE · ${current.cameraName || camera?.name || 'Cảnh báo an toàn'}`}
    onCancel={onClose} footer={<Space wrap>
      {!details.isError && <AlertActionButtons alert={current} onHandled={onClose} />}
      <Button onClick={onClose}>Đóng</Button>
    </Space>}>
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap>
        <Tag color={critical ? '#EF4444' : '#F97316'}>{critical ? 'Khẩn cấp' : 'Chú ý'}</Tag>
        <Tag color="blue">LIVE</Tag>
        <Tag>{status}</Tag>
        {canPlay && <Tag color={fresh ? 'success' : 'default'}>{fresh ? 'AI đang giám sát' : 'AI đang chờ dữ liệu'}</Tag>}
      </Space>
      {details.isError && <Alert type="warning" title="Chưa cập nhật được trạng thái cảnh báo"
        action={<Button onClick={() => details.refetch()}>Thử lại</Button>} />}
      {cameras.isPending ? <Spin tip="Đang tải camera…"><div style={{ minHeight: 120 }} /></Spin>
        : cameras.isError ? <Alert type="error" title="Không tải được camera"
          action={<Button onClick={() => cameras.refetch()}>Thử lại</Button>} />
          : canPlay ? <div className="safety-live-detail-video">
            <CameraVideoPlayer key={camera.id} equipment={camera} analysis smooth={camera.smoothPlayback} sampleRisks={sampleRisks} sampleRisksWithDiagnostics={sampleRisksWithDiagnostics} getPlayoutDelay={getPlayoutDelay} />
          </div> : <Alert type="warning" title="Camera hiện không khả dụng để phát LIVE. Có thể xem ảnh cảnh báo bên dưới." />}
      <Descriptions title="Thông tin cảnh báo" size="small" column={1} items={[
        { key: 'camera', label: 'Camera', children: current.cameraName || camera?.name || 'Camera' },
        { key: 'location', label: 'Khu vực', children: current.location || camera?.location || 'Chưa có vị trí' },
        { key: 'track', label: 'Đối tượng', children: current.trackId || 'Chưa có thông tin' },
        { key: 'time', label: 'Thời điểm phát hiện', children: new Date(current.observedAt || current.createdAt).toLocaleString('vi-VN') },
      ]} />
      {current.imageUrl && <Collapse size="small" items={[{ key: 'snapshot', label: 'Ảnh tại thời điểm phát hiện',
        children: <Image src={current.imageUrl} alt="Ảnh camera tại thời điểm cảnh báo" width="100%" /> }]} />}
    </Space>
  </Modal>
}
