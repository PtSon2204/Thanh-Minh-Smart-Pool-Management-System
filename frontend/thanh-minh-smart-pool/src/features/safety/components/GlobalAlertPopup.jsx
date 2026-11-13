import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Alert, Button, Image, Space, Tag } from 'antd'
import { useAuthStore } from '../../auth/store/authStore'
import { monitoringService } from '../services/monitoringService'

// Popup cảnh báo toàn cục: hiển thị trên mọi trang khi người dùng có quyền nhận
// cảnh báo an toàn. Không tự điều hướng; "Xem chi tiết" mới chuyển màn hình.
export default function GlobalAlertPopup({ alert, isCritical, onDismiss }) {
  const navigate = useNavigate()
  const isAdmin = useAuthStore(state => state.session?.role?.toUpperCase() === 'ADMIN')
  const [confirming, setConfirming] = useState(false)

  const detail = () => {
    if (alert.sourceType === 'UPLOAD') {
      navigate(`/admin/safety/video-analysis?job=${encodeURIComponent(alert.videoJobId)}&seek=${alert.videoTimestamp ?? 0}`)
    } else {
      navigate(`/admin/safety/list-camera?cameraId=${encodeURIComponent(alert.cameraId)}&severity=${encodeURIComponent(alert.severity)}`)
    }
    onDismiss(alert.id)
  }

  const confirm = async () => {
    setConfirming(true)
    try {
      await monitoringService.handleAlert(alert.id, 'acknowledge')
      onDismiss(alert.id)
    } catch {
      setConfirming(false)
    }
  }

  const cancel = async () => {
    setConfirming(true)
    try {
      await monitoringService.handleAlert(alert.id, 'cancel')
      onDismiss(alert.id)
    } catch {
      setConfirming(false)
    }
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
            {alert.sourceType === 'UPLOAD' && alert.videoTimestamp != null && <span>· {Number(alert.videoTimestamp).toFixed(1)}s</span>}
          </Space>
          {alert.imageUrl
            ? <Image src={alert.imageUrl} width="100%" alt="Ảnh sự cố" style={{ borderRadius: 8, maxHeight: '35dvh', objectFit: 'contain' }} preview={false} />
            : <Tag>Ảnh sự cố đang được xử lý</Tag>}
          <Space wrap>
            {isAdmin && <Button type="primary" size="small" onClick={detail}>Xem chi tiết</Button>}
            <Button size="small" loading={confirming} onClick={confirm}>Xác nhận</Button>
            <Button size="small" danger onClick={cancel}>Hủy</Button>
            <Button type="text" size="small" onClick={() => onDismiss(alert.id)}>Đóng</Button>
          </Space>
        </Space>
      }
    />
  )
}
