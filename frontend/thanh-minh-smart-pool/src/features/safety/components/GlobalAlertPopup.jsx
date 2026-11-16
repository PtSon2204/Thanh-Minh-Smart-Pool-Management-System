import { Alert, Button, Image, Space, Tag } from 'antd'
import { useAuthStore } from '../../auth/store/authStore'
import AlertActionButtons from './AlertActionButtons'

// Popup cảnh báo toàn cục: hiển thị trên mọi trang khi người dùng có quyền nhận
// cảnh báo an toàn. Chi tiết LIVE mở camera trong dialog, không đổi trang.
export default function GlobalAlertPopup({ alert, isCritical, onDismiss, onViewLive }) {
  // Chỉ operator an toàn (ADMIN / LIFEGUARD) được mở chi tiết LIVE — khớp host GlobalSafetyAlertsHost.
  const canViewDetail = useAuthStore(state => {
    const role = state.session?.role?.toUpperCase()
    return role === 'ADMIN' || role === 'LIFEGUARD'
  })

  const detail = () => {
    onViewLive?.(alert)
    onDismiss(alert.id)
  }

  const color = isCritical(alert) ? '#EF4444' : '#F97316'
  const sourceName = alert.cameraName || 'Camera'

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
            {<span>· {new Date(alert.observedAt || alert.createdAt).toLocaleString('vi-VN')}</span>}
          </Space>
          {alert.imageUrl
            ? <Image src={alert.imageUrl} width="100%" alt="Ảnh sự cố" style={{ borderRadius: 8, maxHeight: '35dvh', objectFit: 'contain' }} preview={false} />
            : <Tag>Ảnh sự cố đang được xử lý</Tag>}
          <Space wrap>
            {canViewDetail && <Button type="primary" size="small" onClick={detail}>Xem chi tiết</Button>}
            <AlertActionButtons alert={alert} onHandled={() => onDismiss(alert.id)} />
            <Button type="text" size="small" onClick={() => onDismiss(alert.id)}>Đóng</Button>
          </Space>
        </Space>
      }
    />
  )
}
