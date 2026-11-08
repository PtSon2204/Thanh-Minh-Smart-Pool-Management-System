import { useQuery } from '@tanstack/react-query'
import { Alert, Button, Modal, Tag } from 'antd'
import { CameraVideoPlayer } from '../../equipments/components/CameraViewModal'
import { monitoringService } from '../services/monitoringService'

// Modal phóng to một camera; risks lấy từ query cache chia sẻ với card cùng cameraId
// (không phát video/đọc AI song song), tự refresh khi có cảnh báo mới.
export default function EnlargedCameraModal({ camera, onClose }) {
  const { risks } = useQuery({
    queryKey: ['safety', 'camera-analysis', camera?.id],
    queryFn: ({ signal }) => monitoringService.analysis(camera.id, signal),
    enabled: !!camera?.id, refetchInterval: camera ? 2000 : false, retry: false,
  })
  return <Modal open={!!camera} destroyOnHidden width={1100} title={camera?.name} footer={null} onCancel={onClose}>
    {camera && camera.status === 'Active' && camera.source === 'RTSP'
      ? <CameraVideoPlayer equipment={camera} risks={risks.data?.risks || []} />
      : <Alert type="info" title="Camera không khả dụng để phóng to." action={<Button onClick={onClose}>Đóng</Button>} />}
    {camera?.source === 'RTSP' && (risks.data?.risks || []).some(r => r.severity === 'CRITICAL') &&
      <Tag color="#EF4444">Đang có cảnh báo khẩn cấp</Tag>}
  </Modal>
}
