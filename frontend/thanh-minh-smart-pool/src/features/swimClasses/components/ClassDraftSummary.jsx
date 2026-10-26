import { Card, Tag, Typography } from 'antd'
import { coachName } from '../utils/classDisplay'
import { formatVietnamTime } from '../utils/classSchedule'

const { Text } = Typography

export default function ClassDraftSummary({ draft, coaches }) {
  return <Card className="swim-class-summary-card" title="Lớp đang tạo" extra={<Tag color="blue">Draft</Tag>}>
    <h3>{draft.name}</h3>
    <div className="swim-class-summary-row"><span>Coach mặc định</span><strong>{coachName(coaches, draft.coachUserId)}</strong></div>
    <div className="swim-class-summary-row"><span>Sức chứa</span><strong>{draft.capacity} học viên</strong></div>
    <div className="swim-class-summary-row"><span>Học phí</span><strong>{Number(draft.fee).toLocaleString('vi-VN')} VNĐ</strong></div>
    <div className="swim-class-summary-row"><span>Nhận đăng ký</span><strong>{formatVietnamTime(draft.registrationStartAt)}<br />đến {formatVietnamTime(draft.registrationEndAt)}</strong></div>
    <div className="swim-class-summary-row"><span>Buổi đã lưu</span><strong>{draft.sessions.length}</strong></div>
    <Text type="secondary" className="swim-class-draft-id">Mã lớp: {draft.id}</Text>
  </Card>
}
