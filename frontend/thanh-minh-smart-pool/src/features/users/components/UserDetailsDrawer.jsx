import { Alert, Avatar, Button, Descriptions, Drawer, Space, Spin, Tag } from 'antd'
import { USER_ROLES, getUserError } from '../userManagement'
import { useUserDetail } from '../hooks/useUsers'

function formatDate(value) {
  return value ? new Date(value).toLocaleDateString('vi-VN') : '—'
}

function formatDateTime(value) {
  return value ? new Date(value).toLocaleString('vi-VN') : '—'
}

export default function UserDetailsDrawer({ userId, onClose }) {
  const detail = useUserDetail(userId)
  const user = detail.data
  const role = USER_ROLES.find((item) => item.value === user?.role)?.label || user?.role || '—'
  const status = user?.status === 'ACTIVE' ? 'Hoạt động' : user?.status === 'LOCKED' ? 'Đã khóa' : user?.status || '—'

  return <Drawer title="Chi tiết người dùng" open={Boolean(userId)} onClose={onClose} width={520}>
    {detail.isPending && <div className="um-detail-state"><Spin size="large" /></div>}
    {detail.isError && <Alert
      type="error"
      showIcon
      message="Không thể tải chi tiết người dùng"
      description={getUserError(detail.error)}
      action={<Button size="small" onClick={() => detail.refetch()}>Thử lại</Button>}
    />}
    {user && !detail.isError && <>
      <div className="um-detail-heading">
        <Space size={12}>
          <Avatar size={48} src={user.avatarUrl}>{(user.fullName || user.username || '?').charAt(0)}</Avatar>
          <span className="um-person"><strong>{user.fullName || 'Chưa có họ tên'}</strong><small>@{user.username}</small></span>
        </Space>
      </div>
      <Descriptions bordered column={1} size="small">
        <Descriptions.Item label="Mã người dùng">{user.id}</Descriptions.Item>
        <Descriptions.Item label="Tên đăng nhập">{user.username || '—'}</Descriptions.Item>
        <Descriptions.Item label="Họ và tên">{user.fullName || '—'}</Descriptions.Item>
        <Descriptions.Item label="Email">{user.email || '—'}</Descriptions.Item>
        <Descriptions.Item label="Số điện thoại">{user.phone || '—'}</Descriptions.Item>
        <Descriptions.Item label="Ngày sinh">{formatDate(user.dateOfBirth)}</Descriptions.Item>
        <Descriptions.Item label="Địa chỉ">{user.address || '—'}</Descriptions.Item>
        <Descriptions.Item label="Vai trò"><Tag color={user.role === 'ADMIN' ? 'purple' : user.role === 'STAFF' ? 'blue' : 'cyan'}>{role}</Tag></Descriptions.Item>
        <Descriptions.Item label="Trạng thái"><Tag color={user.status === 'ACTIVE' ? 'success' : user.status === 'LOCKED' ? 'default' : 'warning'}>{status}</Tag></Descriptions.Item>
        <Descriptions.Item label="Ngày tạo">{formatDateTime(user.createdAt)}</Descriptions.Item>
        <Descriptions.Item label="Cập nhật lần cuối">{formatDateTime(user.updatedAt)}</Descriptions.Item>
      </Descriptions>
    </>}
  </Drawer>
}
