import { Alert, Button, Modal, Tag } from 'antd'
import { CameraVideoPlayer } from '../../equipments/components/CameraViewModal'
import { useCameraRisks } from '../hooks/useCameraRisks'
import PairedCameraPlayer from './PairedCameraPlayer'

// Modal phóng to một camera; risks lấy từ query cache chia sẻ với card cùng cameraId
// (không phát video/đọc AI song song), tự refresh khi có cảnh báo mới.
export default function EnlargedCameraModal({ camera, onClose }) {
  const { risks } = useCameraRisks(camera?.id, !!camera?.id)
  return <Modal open={!!camera} destroyOnHidden width={1100} title={camera?.name} footer={null} onCancel={onClose}>
    {camera && camera.status === 'Active' && camera.source === 'RTSP'
      ? camera.analysisMode === 'ai' ? <PairedCameraPlayer key={camera.id} camera={camera} /> : <CameraVideoPlayer equipment={camera} />
      : <Alert type="info" title="Camera không khả dụng để phóng to." action={<Button onClick={onClose}>Đóng</Button>} />}
    {camera?.source === 'RTSP' && risks.some(r => r.severity === 'CRITICAL') &&
      <Tag color="#EF4444">Đang có cảnh báo khẩn cấp</Tag>}
  </Modal>
}
