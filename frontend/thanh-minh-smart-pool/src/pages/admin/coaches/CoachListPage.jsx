import { useState } from 'react'
import { Alert, Avatar, Button, Card, Input, Modal, Select, Space, Table, Tag, Typography, message } from 'antd'
import { EyeOutlined, ReloadOutlined, SearchOutlined, TeamOutlined } from '@ant-design/icons'
import { useUsers } from '../../../features/users/hooks/useUsers'
import { useRevokeCoach } from '../../../features/users/hooks/useUserMutations'
import UserDetailsDrawer from '../../../features/users/components/UserDetailsDrawer'
import { getUserError } from '../../../features/users/utils/userManagement'
import '../../../features/users/styles/userManagement.css'

const { Title, Text } = Typography

export default function CoachListPage() {
  const [messageApi, contextHolder] = message.useMessage()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState(undefined)
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [selectedId, setSelectedId] = useState(null)
  const [revokeTarget, setRevokeTarget] = useState(null)
  const [revokeReason, setRevokeReason] = useState('')
  const coaches = useUsers({ role: 'COACH', searchTerm: search.trim() || undefined, status, pageIndex, pageSize })
  const revokeCoach = useRevokeCoach()

  async function submitRevoke() {
    if (!revokeTarget || !revokeReason.trim()) {
      messageApi.error('Vui lòng nhập lý do thu hồi quyền Coach.')
      return
    }
    try {
      await revokeCoach.mutateAsync({ id: revokeTarget.id, reason: revokeReason.trim() })
      messageApi.success('Đã thu hồi quyền Coach.')
      setRevokeTarget(null)
      setRevokeReason('')
      setSelectedId(null)
    } catch (error) {
      messageApi.error(getUserError(error))
    }
  }

  const columns = [
    { title: 'Huấn luyện viên', key: 'name', render: (_, user) => <Space size={12}>
      <Avatar src={user.avatarUrl}>{(user.fullName || user.username || '?').charAt(0)}</Avatar>
      <span className="um-person"><strong>{user.fullName || user.username || 'Chưa có tên'}</strong><small>@{user.username}</small></span>
    </Space> },
    { title: 'Liên hệ', key: 'contact', render: (_, user) => <span className="um-person"><span>{user.email || '—'}</span><small>{user.phone || '—'}</small></span> },
    { title: 'Quyền Coach', key: 'coachRole', render: (_, user) => <Tag color="gold">{user.role === 'COACH' ? 'Vai trò chính' : 'Đã cấp quyền'}</Tag> },
    { title: 'Tài khoản', dataIndex: 'status', key: 'status', render: value => <Tag color={value === 'ACTIVE' ? 'success' : 'default'}>{value === 'ACTIVE' ? 'Hoạt động' : value === 'LOCKED' ? 'Đã khóa' : value || 'Không rõ'}</Tag> },
    { title: 'Thao tác', key: 'actions', render: (_, user) => <Button icon={<EyeOutlined />} onClick={() => setSelectedId(user.id)}>Xem hồ sơ</Button> },
  ]

  return <div className="um-page">
    {contextHolder}
    <header className="um-hero"><div><span className="um-eyebrow">QUẢN LÝ HUẤN LUYỆN VIÊN</span>
      <Title level={2}>Danh sách huấn luyện viên</Title>
      <p>Xem tài khoản đã có quyền Coach và thông tin liên hệ của từng người.</p></div>
      <div className="um-hero-icon"><TeamOutlined /></div>
    </header>
    <Card className="um-main-card">
      <div className="um-toolbar"><div><h2>Huấn luyện viên</h2><Text type="secondary">{coaches.data?.totalCount ?? 0} người phù hợp</Text></div>
        <Button icon={<ReloadOutlined />} loading={coaches.isFetching} onClick={() => coaches.refetch()}>Tải lại</Button></div>
      <div className="um-filters">
        <Input allowClear prefix={<SearchOutlined />} aria-label="Tìm huấn luyện viên"
          placeholder="Tìm tên, email hoặc số điện thoại" maxLength={100} value={search}
          onChange={event => { setSearch(event.target.value); setPageIndex(1) }} />
        <Select allowClear placeholder="Tất cả trạng thái" aria-label="Lọc trạng thái tài khoản"
          value={status} onChange={value => { setStatus(value); setPageIndex(1) }}
          options={[{ value: 'ACTIVE', label: 'Hoạt động' }, { value: 'LOCKED', label: 'Đã khóa' }]} />
      </div>
      {coaches.isError && <Alert type="error" showIcon message="Không thể tải danh sách huấn luyện viên"
        description={getUserError(coaches.error)} action={<Button onClick={() => coaches.refetch()}>Thử lại</Button>}
        style={{ marginBottom: 16 }} />}
      <Table rowKey="id" columns={columns} dataSource={coaches.data?.items || []}
        loading={coaches.isLoading || coaches.isFetching} scroll={{ x: 720 }}
        locale={{ emptyText: 'Chưa có huấn luyện viên phù hợp.' }}
        pagination={{ current: pageIndex, pageSize, total: coaches.data?.totalCount || 0,
          showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100],
          onChange: (page, size) => { setPageIndex(size === pageSize ? page : 1); setPageSize(size) } }} />
    </Card>
    <UserDetailsDrawer userId={selectedId} onClose={() => setSelectedId(null)}
      onRevokeCoach={user => { setRevokeTarget(user); setRevokeReason('') }} />
    <Modal title={`Thu hồi quyền Coach của ${revokeTarget?.fullName || revokeTarget?.username || 'người dùng'}?`}
      open={Boolean(revokeTarget)} okText="Thu hồi quyền" cancelText="Hủy"
      okButtonProps={{ danger: true, loading: revokeCoach.isPending }}
      onCancel={() => { if (!revokeCoach.isPending) setRevokeTarget(null) }} onOk={submitRevoke}>
      <p>Người dùng vẫn giữ quyền Customer. Cần ngừng hoặc chuyển giao lớp và buổi học đang phụ trách trước khi thu hồi.</p>
      <Input.TextArea aria-label="Lý do thu hồi quyền Coach" placeholder="Nhập lý do thu hồi (bắt buộc)"
        value={revokeReason} onChange={event => setRevokeReason(event.target.value)}
        maxLength={1000} showCount rows={4} />
    </Modal>
  </div>
}
