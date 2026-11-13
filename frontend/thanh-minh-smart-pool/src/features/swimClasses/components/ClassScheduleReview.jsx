import { CheckCircleOutlined, DeleteOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Empty, Popconfirm, Tag } from 'antd'
import { coachName } from '../utils/classDisplay'
import { formatVietnamTime } from '../utils/classSchedule'
import { canActivateClass } from '../utils/classAvailability'

export default function ClassScheduleReview({ draft, coaches, activationError, activating, savingSessions, onActivate, onRemoveSession }) {
  return <>
    <Card className="swim-class-saved-card" title={`Buổi trong bản nháp (${draft.sessions.length})`}>
      {draft.sessions.length === 0 ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có buổi học. Hãy thêm buổi vào bản nháp trước khi kích hoạt lớp." /> : <div className="swim-class-saved-list">
        {draft.sessions.map((item, index) => <div className="swim-class-saved-row" key={item.id}>
          <span className="swim-class-saved-number">{String(index + 1).padStart(2, '0')}</span>
          <div><strong>{formatVietnamTime(item.startsAt)} – {formatVietnamTime(item.endsAt).slice(-5)}</strong><span>Coach: {coachName(coaches, item.coachUserId)}</span></div>
          <Tag color="blue">{item.isLocal ? 'Bản nháp' : 'Đã xếp lịch'}</Tag>
          {item.isLocal && <Popconfirm title="Xóa buổi học khỏi bản nháp?" okText="Xóa buổi" cancelText="Giữ lại"
            okButtonProps={{ danger: true }} onConfirm={() => onRemoveSession(item.id)}>
            <Button danger type="text" icon={<DeleteOutlined />} disabled={activating || savingSessions}
              aria-label={`Xóa buổi học ${index + 1}`}>Xóa</Button>
          </Popconfirm>}
        </div>)}
      </div>}
    </Card>
    <Card className="swim-class-activate-card">
      <div><strong>Sẵn sàng mở đăng ký?</strong><p>Khi kích hoạt, lịch trong bản nháp sẽ được lưu lên hệ thống. Cần ít nhất một buổi sắp tới và hạn đăng ký kết thúc không muộn hơn buổi đầu tiên.</p></div>
      {activationError && <Alert type="error" showIcon title={activationError} className="swim-class-form-alert" />}
      <Button type="primary" icon={<CheckCircleOutlined />} size="large" disabled={!canActivateClass(draft) || savingSessions} loading={activating} onClick={onActivate}>Kích hoạt lớp</Button>
    </Card>
  </>
}
