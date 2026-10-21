import { Button, Descriptions, Modal, Tag } from 'antd'
import { EditOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { maintenanceStatuses, recurrenceText } from '../utils/maintenanceDisplay'

export default function MaintenanceDetailsModal({ schedule, onClose, onEdit }) {
  const status = maintenanceStatuses[schedule?.status]
  const editable = schedule && ['Pending', 'Paused'].includes(schedule.status)
  return <Modal title="Chi tiết lịch bảo trì" open={Boolean(schedule)} onCancel={onClose} width={640} className="maintenance-modal"
    footer={<><Button onClick={onClose}>Đóng</Button>{editable && <Button type="primary" icon={<EditOutlined />} onClick={() => onEdit(schedule)}>Cập nhật</Button>}</>}>
    {schedule && <Descriptions bordered column={1} size="small" items={[
      { key: 'equipment', label: 'Thiết bị', children: schedule.equipmentName },
      { key: 'location', label: 'Khu vực', children: schedule.location || 'Chưa xác định' },
      { key: 'task', label: 'Tên công việc', children: schedule.taskName },
      { key: 'cycle', label: 'Chu kỳ bảo trì', children: recurrenceText(schedule) },
      { key: 'date', label: 'Ngày thực hiện', children: dayjs(schedule.startDate).format('DD/MM/YYYY') },
      { key: 'assigned', label: 'Người phụ trách', children: schedule.assigneeName },
      { key: 'remind', label: 'Nhắc trước', children: schedule.remindBeforeDays ? `${schedule.remindBeforeDays} ngày` : 'Từ ngày đến hạn' },
      { key: 'status', label: 'Trạng thái', children: <><Tag color={status?.color}>{status?.label || schedule.status}</Tag>{schedule.isOverdue && <Tag color="error">Quá hạn</Tag>}</> },
      { key: 'next', label: 'Lần kế tiếp dự kiến', children: ['Cancelled', 'Paused'].includes(schedule.status) ? 'Đã dừng sinh lịch' : schedule.nextDate ? dayjs(schedule.nextDate).format('DD/MM/YYYY') : 'Không lặp lại' },
    ]} />}
    {schedule?.repeatUnit !== 'None' && <p className="maintenance-detail-note">Lần kế tiếp được tạo sau khi hoàn thành công việc. Bạn có thể tạm dừng hoặc hủy lịch đang chờ để dừng các lần lặp.</p>}
  </Modal>
}
