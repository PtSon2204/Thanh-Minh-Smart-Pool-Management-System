import { useState } from 'react'
import { Alert, Button, Card, Col, Input, Modal, Row, Select, Space, Statistic, Typography, message } from 'antd'
import { PlusOutlined, ReloadOutlined, SearchOutlined, TeamOutlined, UserOutlined } from '@ant-design/icons'
import UserFormModal from '../../features/users/components/UserFormModal'
import UserDetailsDrawer from '../../features/users/components/UserDetailsDrawer'
import UserTable from '../../features/users/components/UserTable'
import { useUsers } from '../../features/users/hooks/useUsers'
import { useChangeUserStatus, useSaveUser } from '../../features/users/hooks/useUserMutations'
import userService from '../../features/users/services/userService'
import { getUserError, USER_ROLES } from '../../features/users/userManagement'
import '../../features/users/styles/userManagement.css'

const { Title, Text } = Typography

export default function UserManagementPage() {
  const [messageApi, contextHolder] = message.useMessage()
  const [search, setSearch] = useState('')
  const [role, setRole] = useState()
  const [status, setStatus] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [editor, setEditor] = useState(null)
  const [detailUserId, setDetailUserId] = useState(null)
  const [editingId, setEditingId] = useState(null)

  const usersQuery = useUsers({ pageIndex, pageSize, searchTerm: search.trim() || undefined, role, status })
  const saveUser = useSaveUser()
  const changeStatus = useChangeUserStatus()
  const users = usersQuery.data?.items || []

  const changeFilter = (setter) => (value) => {
    setter(value)
    setPageIndex(1)
  }

  const openEdit = async (user) => {
    setEditingId(user.id)
    try {
      const record = await userService.getById(user.id)
      setEditor({ record })
    } catch (error) {
      messageApi.error(getUserError(error))
    } finally {
      setEditingId(null)
    }
  }

  const save = (values) => {
    saveUser.mutate({ values, record: editor.record }, {
      onSuccess: () => {
        messageApi.success(editor.record ? 'Đã cập nhật tài khoản.' : 'Đã tạo tài khoản.')
        setEditor(null)
      },
      onError: (error) => {
        messageApi.error(error.message?.startsWith('Đã lưu thông tin tài khoản') ? error.message : getUserError(error))
      },
    })
  }

  const toggleStatus = (user) => {
    const nextStatus = user.status === 'ACTIVE' ? 'LOCKED' : 'ACTIVE'
    Modal.confirm({
      title: nextStatus === 'LOCKED' ? 'Khóa tài khoản?' : 'Mở khóa tài khoản?',
      content: `${user.fullName || user.username} sẽ ${nextStatus === 'LOCKED' ? 'không thể sử dụng tài khoản' : 'có thể sử dụng lại tài khoản'}.`,
      okText: nextStatus === 'LOCKED' ? 'Khóa tài khoản' : 'Mở khóa',
      cancelText: 'Hủy',
      okButtonProps: { danger: nextStatus === 'LOCKED' },
      onOk: async () => {
        try {
          await changeStatus.mutateAsync({ id: user.id, status: nextStatus })
          messageApi.success('Đã cập nhật trạng thái tài khoản.')
        } catch (error) {
          messageApi.error(getUserError(error))
          throw error
        }
      },
    })
  }

  return <div className="um-page">
    {contextHolder}
    <header className="um-hero">
      <div>
        <span className="um-eyebrow">QUẢN TRỊ HỆ THỐNG</span>
        <Title level={2}>Quản lý người dùng</Title>
        <p>Theo dõi tài khoản và phân quyền tại hồ bơi Thanh Minh.</p>
      </div>
      <div className="um-hero-icon"><TeamOutlined /></div>
    </header>
    <Row gutter={[16, 16]} className="um-stats">
      <Col xs={12} md={8}><Card><Statistic title="Tài khoản phù hợp" value={usersQuery.data?.totalCount ?? 0} prefix={<TeamOutlined />} /></Card></Col>
      <Col xs={12} md={8}><Card><Statistic title="Hiển thị trên trang" value={users.length} prefix={<UserOutlined />} /></Card></Col>
      <Col xs={12} md={8}><Card><Statistic title="Vai trò hệ thống" value={USER_ROLES.length} prefix={<TeamOutlined />} /></Card></Col>
    </Row>
    <Card className="um-main-card">
      <div className="um-toolbar">
        <div>
          <h2>Danh sách tài khoản</h2>
          <Text type="secondary">Tìm kiếm, cập nhật và phân quyền người dùng.</Text>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => usersQuery.refetch()}>Tải lại</Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditor({ record: null })}>Thêm tài khoản</Button>
        </Space>
      </div>
      <div className="um-filters">
        <Input
          allowClear
          prefix={<SearchOutlined />}
          aria-label="Tìm kiếm tài khoản"
          placeholder="Tìm tên, username, email, số điện thoại..."
          maxLength={100}
          value={search}
          onChange={(event) => changeFilter(setSearch)(event.target.value)}
        />
        <Select allowClear placeholder="Tất cả vai trò" aria-label="Lọc theo vai trò" value={role} onChange={changeFilter(setRole)} options={USER_ROLES} />
        <Select allowClear placeholder="Tất cả trạng thái" aria-label="Lọc theo trạng thái" value={status} onChange={changeFilter(setStatus)} options={[{ value: 'ACTIVE', label: 'Hoạt động' }, { value: 'LOCKED', label: 'Đã khóa' }]} />
      </div>
      {usersQuery.isError && <Alert
        type="error"
        showIcon
        message="Không thể tải danh sách tài khoản"
        description={getUserError(usersQuery.error)}
        action={<Button onClick={() => usersQuery.refetch()}>Thử lại</Button>}
        style={{ marginBottom: 16 }}
      />}
      <UserTable
        users={users}
        loading={usersQuery.isLoading || usersQuery.isFetching}
        total={usersQuery.data?.totalCount || 0}
        pageIndex={pageIndex}
        pageSize={pageSize}
        editingId={editingId}
        changingStatusId={changeStatus.isPending ? changeStatus.variables?.id : null}
        onView={(user) => setDetailUserId(user.id)}
        onEdit={openEdit}
        onToggleStatus={toggleStatus}
        onPageChange={(page, size) => {
          setPageIndex(size === pageSize ? page : 1)
          setPageSize(size)
        }}
      />
    </Card>
    <UserDetailsDrawer userId={detailUserId} onClose={() => setDetailUserId(null)} />
    <UserFormModal
      open={Boolean(editor)}
      record={editor?.record}
      saving={saveUser.isPending}
      onClose={() => { if (!saveUser.isPending) setEditor(null) }}
      onSave={save}
    />
  </div>
}
