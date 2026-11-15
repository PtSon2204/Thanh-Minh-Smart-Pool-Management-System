import { Alert, Card } from 'antd'
import { useNavigate } from 'react-router-dom'
import { useAuthStore } from '../../auth/store/authStore'
import { useMonitoringEvents } from '../hooks/useMonitoringEvents'
import AlertList from './AlertList'
import '../styles/safetyResponsive.css'

export default function LiveAlertPanel() {
  const connection = useMonitoringEvents()
  const navigate = useNavigate()
  const isAdmin = useAuthStore(state => state.session?.role?.toUpperCase() === 'ADMIN')
  return <Card className="safety-live-alerts" title="Cảnh báo camera LIVE">
    {connection !== 'connected' && <Alert type="warning" title="Kết nối realtime chưa sẵn sàng. Danh sách vẫn được tải lại định kỳ." />}
    <AlertList onDetail={isAdmin ? row => navigate(`/admin/safety/list-camera?cameraId=${encodeURIComponent(row.cameraId)}&severity=${encodeURIComponent(row.severity)}`) : undefined} />
  </Card>
}
