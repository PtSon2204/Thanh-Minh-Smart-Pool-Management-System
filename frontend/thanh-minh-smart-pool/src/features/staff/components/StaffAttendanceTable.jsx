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

export default function StaffAttendanceTable({ assignments, actionsDisabled, pendingId, onAction }) {
  const columns = [
    { title: 'Nhân viên', dataIndex: 'employeeName', key: 'employeeName', render: (value) => value || 'Nhân viên' },
    { title: 'Trạng thái', dataIndex: 'status', key: 'status', render: (value) => <Tag color={statusColor(value)}>{statusLabel(value)}</Tag> },
    { title: 'Vào ca', dataIndex: 'checkInTime', key: 'checkInTime', render: formatDateTime },
    { title: 'Ra ca', dataIndex: 'checkOutTime', key: 'checkOutTime', render: formatDateTime },
    {
      title: 'Thao tác',
      key: 'actions',
      align: 'right',
      render: (_, assignment) => {
        const action = actionFor(assignment)
        if (!action) return '-'

        const isCheckIn = action === 'check-in'
        return <Button
          className="admin-action-button admin-action-button--positive"
          size="small"
          loading={pendingId === assignment.id}
          disabled={actionsDisabled}
          onClick={() => onAction(assignment, action)}
        >
          {isCheckIn ? 'Vào ca' : 'Ra ca'}
        </Button>
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
