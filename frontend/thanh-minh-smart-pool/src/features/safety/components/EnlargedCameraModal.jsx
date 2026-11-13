import { Alert, Button, Modal, Tag } from 'antd'
import { useCameraRisks } from '../hooks/useCameraRisks'
import { CameraVideoPlayer } from '../../equipments/components/CameraViewModal'

// Phóng to cùng luồng Camera AI đang hiển thị trên danh sách.
export default function EnlargedCameraModal({ camera, onClose }) {
  const { risks } = useCameraRisks(camera?.id, !!camera?.id)
  return <Modal className="safety-modal" open={!!camera} destroyOnHidden width={1100} title={camera ? `Camera AI · ${camera.name}` : 'Camera AI'} footer={null} onCancel={onClose}>
    {camera && camera.status === 'Active' && camera.source === 'RTSP'
      ? <CameraVideoPlayer key={camera.id} equipment={camera} risks={risks} />
      : <Alert type="info" title="Camera không khả dụng để phóng to." action={<Button onClick={onClose}>Đóng</Button>} />}
    {camera?.source === 'RTSP' && risks.some(r => r.severity === 'CRITICAL') &&
      <Tag color="#EF4444">Đang có cảnh báo khẩn cấp</Tag>}
  </Modal>
}
