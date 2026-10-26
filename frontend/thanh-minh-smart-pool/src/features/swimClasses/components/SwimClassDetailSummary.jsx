import { ClockCircleOutlined, TeamOutlined, UserOutlined, WalletOutlined } from '@ant-design/icons'
import { Alert } from 'antd'
import { formatVietnamTime } from '../utils/classSchedule'
import { classAvailability } from '../utils/classAvailability'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })

export default function SwimClassDetailSummary({ swimClass, now }) {
  const remaining = Math.max(0, swimClass.capacity - swimClass.confirmedStudentCount)
  const availability = classAvailability(swimClass, now)
  const alertType = availability.kind === 'open' ? 'success' : availability.kind === 'inactive' || availability.kind === 'full' ? 'warning' : 'info'

  return <>
    <Alert showIcon type={alertType} title={availability.text} />
    <section className="swim-class-detail-panel" aria-label="Thông tin lớp dạy bơi">
      <div className="swim-class-detail-section-title"><TeamOutlined /><h2>Thông tin lớp</h2></div>
      {swimClass.description && <p className="swim-class-detail-description">{swimClass.description}</p>}
      <dl className="swim-class-detail-facts">
        <div><dt><UserOutlined /> Coach mặc định</dt><dd>{swimClass.coachName || swimClass.coachUserId}</dd></div>
        <div><dt><WalletOutlined /> Học phí</dt><dd>{currency.format(swimClass.fee)}</dd></div>
        <div><dt><TeamOutlined /> Học viên đã xác nhận</dt><dd>{swimClass.confirmedStudentCount}/{swimClass.capacity}</dd></div>
        <div><dt><TeamOutlined /> Chỗ còn lại</dt><dd>{remaining}</dd></div>
        <div><dt><ClockCircleOutlined /> Bắt đầu nhận đăng ký</dt><dd>{formatVietnamTime(swimClass.registrationStartAt)}</dd></div>
        <div><dt><ClockCircleOutlined /> Kết thúc nhận đăng ký</dt><dd>{formatVietnamTime(swimClass.registrationEndAt)}</dd></div>
      </dl>
      <p className="swim-class-detail-timezone">Thời gian hiển thị theo giờ Việt Nam (UTC+7).</p>
    </section>
  </>
}
