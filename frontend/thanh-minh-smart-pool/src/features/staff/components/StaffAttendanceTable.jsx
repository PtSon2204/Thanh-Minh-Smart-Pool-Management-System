import { Button, Table, Tag } from 'antd'
import { statusColor, statusLabel } from '../schedule/scheduleUtils'

const vietnamDateTime = new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'full',
  timeStyle: 'short',
  timeZone: 'Asia/Ho_Chi_Minh',
})

function formatDateTime(value) {
  return value ? vietnamDateTime.format(new Date(value)) : '-'
}

function actionFor(assignment) {
  if (assignment.status === 'Scheduled' && !assignment.checkInTime && !assignment.checkOutTime) return 'check-in'
  if (assignment.status === 'Present' && assignment.checkInTime && !assignment.checkOutTime) return 'check-out'
  return null
}

function sourceLabel(value) {
  if (value === 'QR') return 'QR'
  if (value === 'Manual') return 'Thủ công'
  return 'Không xác định'
}

export default function StaffAttendanceTable({ assignments, actionsDisabled, pendingId, onAction, onCorrect, onHistory, correctionAvailable }) {
  const columns = [
    { title: 'Nhân viên', dataIndex: 'employeeName', key: 'employeeName', render: (value) => value || 'Nhân viên' },
    { title: 'Trạng thái', dataIndex: 'status', key: 'status', render: (value) => <Tag color={statusColor(value)}>{statusLabel(value)}</Tag> },
    { title: 'Vào ca', dataIndex: 'checkInTime', key: 'checkInTime', render: (value, assignment) => <>{formatDateTime(value)}<small className="staff-attendance-source">Nguồn: {sourceLabel(assignment.checkInMethod)}</small></> },
    { title: 'Ra ca', dataIndex: 'checkOutTime', key: 'checkOutTime', render: (value, assignment) => <>{formatDateTime(value)}<small className="staff-attendance-source">Nguồn: {sourceLabel(assignment.checkOutMethod)}</small></> },
    {
      title: 'Thao tác',
      key: 'actions',
      align: 'right',
      render: (_, assignment) => {
        const action = actionFor(assignment)
        return <div className="staff-attendance-row-actions">
          {action && <Button
            className="admin-action-button admin-action-button--positive"
            size="small"
            loading={pendingId === assignment.id}
            disabled={actionsDisabled}
            onClick={() => onAction(assignment, action)}
          >
            {action === 'check-in' ? 'Vào ca' : 'Ra ca'}
          </Button>}
          {assignment.status !== 'Cancelled' && <Button size="small" disabled={actionsDisabled || !correctionAvailable(assignment)} onClick={() => onCorrect(assignment)}>{correctionAvailable(assignment) ? (assignment.checkInTime || assignment.checkOutTime ? 'Điều chỉnh' : 'Bổ sung') : 'Ngoài hạn 7 ngày'}</Button>}
          <Button size="small" type="link" onClick={() => onHistory(assignment)}>Lịch sử</Button>
        </div>
      },
    },
  ]

  return <Table
    rowKey="id"
    columns={columns}
    dataSource={assignments}
    pagination={false}
    scroll={{ x: 920 }}
    locale={{ emptyText: 'Không có nhân viên được phân công cho ca này.' }}
  />
}
