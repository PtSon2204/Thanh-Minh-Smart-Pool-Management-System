import { Avatar, Button, Space, Table, Tag, Tooltip } from 'antd'
import { EditOutlined, EyeOutlined, LockOutlined, UnlockOutlined } from '@ant-design/icons'
import { USER_ROLES } from '../userManagement'

const roleColors = { ADMIN: 'purple', STAFF: 'blue', CUSTOMER: 'cyan' }

function userName(user) {
  return user.fullName || user.username
}

export default function UserTable({ users, loading, total, pageIndex, pageSize, editingId, changingStatusId, onView, onEdit, onToggleStatus, onPageChange }) {
  const columns = [
    {
      title: 'Người dùng',
      key: 'name',
      render: (_, user) => <Space size={12}>
        <Avatar className="um-avatar" src={user.avatarUrl}>{(userName(user) || '?').charAt(0)}</Avatar>
        <span className="um-person">
          <strong>{user.fullName || 'Chưa có họ tên'}</strong>
          <small>@{user.username}</small>
        </span>
      </Space>,
    },
    {
      title: 'Liên hệ',
      key: 'contact',
      render: (_, user) => <span className="um-person">
        <span>{user.email}</span>
        <small>{user.phone}</small>
      </span>,
    },
    {
      title: 'Vai trò',
      dataIndex: 'role',
      key: 'role',
      render: (value) => <Tag color={roleColors[value]}>
        {USER_ROLES.find((item) => item.value === value)?.label || value || 'Chưa phân vai trò'}
      </Tag>,
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      render: (value) => <Tag color={value === 'ACTIVE' ? 'success' : value === 'LOCKED' ? 'default' : 'warning'}>
        {value === 'ACTIVE' ? 'Hoạt động' : value === 'LOCKED' ? 'Đã khóa' : value || 'Không rõ'}
      </Tag>,
    },
    {
      title: 'Ngày tạo',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (value) => value ? new Date(value).toLocaleDateString('vi-VN') : '—',
    },
    {
      title: 'Thao tác',
      key: 'actions',
      align: 'right',
      render: (_, user) => <Space size={2}>
        <Tooltip title="Xem chi tiết">
          <Button type="text" aria-label={`Xem chi tiết tài khoản ${userName(user)}`} icon={<EyeOutlined />} onClick={() => onView(user)} />
        </Tooltip>
        <Tooltip title="Chỉnh sửa">
          <Button
            type="text"
            aria-label={`Sửa tài khoản ${userName(user)}`}
            icon={<EditOutlined />}
            loading={editingId === user.id}
            onClick={() => onEdit(user)}
          />
        </Tooltip>
        {(user.status === 'ACTIVE' || user.status === 'LOCKED') && <Tooltip title={user.status === 'ACTIVE' ? 'Khóa tài khoản' : 'Mở khóa tài khoản'}>
          <Button
            type="text"
            aria-label={`${user.status === 'ACTIVE' ? 'Khóa' : 'Mở khóa'} tài khoản ${userName(user)}`}
            icon={user.status === 'ACTIVE' ? <LockOutlined /> : <UnlockOutlined />}
            loading={changingStatusId === user.id}
            onClick={() => onToggleStatus(user)}
          />
        </Tooltip>}
      </Space>,
    },
  ]

  return <Table
    rowKey="id"
    columns={columns}
    dataSource={users}
    loading={loading}
    scroll={{ x: 850 }}
    locale={{ emptyText: 'Không tìm thấy tài khoản phù hợp.' }}
    pagination={{
      current: pageIndex,
      pageSize,
      total,
      showSizeChanger: true,
      pageSizeOptions: [10, 20, 50, 100],
      onChange: onPageChange,
    }}
  />
}
