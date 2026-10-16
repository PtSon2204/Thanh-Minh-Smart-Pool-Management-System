import { Alert, Button } from 'antd'
import { Link } from 'react-router-dom'
import { ServiceListHeader } from '../../features/services/components/ServiceListChrome'
import '../../features/services/styles/serviceManagement.css'
import '../../features/staff/styles/staffSchedule.css'

export default function StaffQrAttendancePage() {
  return <div className="service-list-page staff-schedule-page">
    <ServiceListHeader title="Chấm công QR" />
    <section className="service-list-table staff-schedule-table" aria-label="Chấm công QR">
      <Alert
        type="info"
        showIcon
        message="Chấm công bằng QR chưa được triển khai. Vui lòng sử dụng chấm công thủ công."
        action={<Link to="/admin/staff/attendance"><Button type="primary">Chấm công thủ công</Button></Link>}
      />
    </section>
  </div>
}
