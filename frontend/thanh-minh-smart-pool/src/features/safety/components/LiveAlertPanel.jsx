import { Alert, Card } from 'antd'
import { useMonitoringEvents } from '../hooks/useMonitoringEvents'
import AlertList from './AlertList'
import '../styles/safetyResponsive.css'

export default function LiveAlertPanel() {
  const connection = useMonitoringEvents()
  return <Card className="safety-live-alerts" title="Cảnh báo camera LIVE">
    {connection !== 'connected' && <Alert type="warning" title="Kết nối realtime chưa sẵn sàng. Danh sách vẫn được tải lại định kỳ." />}
    <AlertList />
  </Card>
}
