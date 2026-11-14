import { useNavigate } from 'react-router-dom'
import { Alert, Button, Image, Space, Tag } from 'antd'
import { useAuthStore } from '../../auth/store/authStore'
import AlertActionButtons from './AlertActionButtons'

// Popup cảnh báo toàn cục: hiển thị trên mọi trang khi người dùng có quyền nhận
// cảnh báo an toàn. Chi tiết LIVE mở video trong dialog, không đổi trang.
export default function GlobalAlertPopup({ alert, isCritical, onDismiss, onViewLive }) {
  const navigate = useNavigate()
  const isAdmin = useAuthStore(state => state.session?.role?.toUpperCase() === 'ADMIN')

  const detail = () => {
    if (alert.sourceType === 'UPLOAD') {
      navigate(`/admin/safety/video-analysis?job=${encodeURIComponent(alert.videoJobId)}&seek=${alert.videoTimestamp ?? 0}`)
    } else {
      onViewLive?.(alert)
      return
    }
    onDismiss(alert.id)
  }

  const color = isCritical(alert) ? '#EF4444' : '#F97316'
  const sourceName = alert.sourceType === 'UPLOAD' ? 'Video tải lên' : (alert.cameraName || 'Camera')

  return (
    <Alert
      type={isCritical(alert) ? 'error' : 'warning'}
      showIcon
      style={{ borderLeft: `4px solid ${color}`, borderRadius: 10 }}
      message={
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Space wrap>
            <Tag color={color}>{isCritical(alert) ? 'Khẩn cấp' : 'Chú ý'}</Tag>
            <strong>{sourceName}</strong>
            {alert.location && <span>· {alert.location}</span>}
            {alert.sourceType === 'LIVE' && <span>· {new Date(alert.observedAt || alert.createdAt).toLocaleString('vi-VN')}</span>}
            {alert.sourceType === 'UPLOAD' && alert.videoTimestamp != null && <span>· {Number(alert.videoTimestamp).toFixed(1)}s</span>}
          </Space>
          {alert.imageUrl
            ? <Image src={alert.imageUrl} width="100%" alt="Ảnh sự cố" style={{ borderRadius: 8, maxHeight: '35dvh', objectFit: 'contain' }} preview={false} />
            : <Tag>Ảnh sự cố đang được xử lý</Tag>}
          <Space wrap>
            {isAdmin && <Button type="primary" size="small" onClick={detail}>Xem chi tiết</Button>}
            <AlertActionButtons alert={alert} onHandled={() => onDismiss(alert.id)} />
            <Button type="text" size="small" onClick={() => onDismiss(alert.id)}>Đóng</Button>
          </Space>
        </Space>
      }
    />
  )
}
