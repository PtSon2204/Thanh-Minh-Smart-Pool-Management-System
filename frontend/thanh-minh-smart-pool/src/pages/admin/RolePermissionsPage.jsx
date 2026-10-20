import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, Select, Space, Switch, Table, Tag, Typography, message } from 'antd'
import { ReloadOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import rolePermissionService from '../../features/permissions/services/rolePermissionService'
import { getUserError, USER_ROLES, USER_ROLE_COLORS } from '../../features/users/userManagement'
import '../../features/users/styles/userManagement.css'
import '../../features/permissions/styles/rolePermissions.css'

const { Title, Text } = Typography

const moduleLabels = {
  profiles: 'Hồ sơ cá nhân',
  staff: 'Hồ sơ nhân viên',
  shifts: 'Ca làm việc',
  staff_schedule: 'Lịch làm việc',
  attendance: 'Chấm công',
  ticket_lookup: 'Tra cứu vé',
  pool_entry: 'Vào cổng hồ bơi',
  ticket_sales: 'Bán vé',
  customer_tickets: 'Vé khách hàng',
  ticket_types: 'Loại vé',
  services: 'Dịch vụ',
  inventory: 'Tồn kho',
  rentals: 'Cho thuê',
  vouchers: 'Mã giảm giá',
  payments: 'Thanh toán',
  reports: 'Báo cáo',
  notifications: 'Thông báo',
}

const actionLabels = { read: 'Xem', create: 'Tạo', update: 'Sửa', delete: 'Xóa' }

export default function RolePermissionsPage() {
  const [messageApi, contextHolder] = message.useMessage()
  const [selectedName, setSelectedName] = useState('LIFEGUARD')
  const queryClient = useQueryClient()
  const catalogQuery = useQuery({ queryKey: ['permissionCatalog'], queryFn: rolePermissionService.getCatalog })
  const rolesQuery = useQuery({ queryKey: ['roles'], queryFn: rolePermissionService.getRoles })
  const roles = (rolesQuery.data || []).filter((role) => USER_ROLES.some((item) => item.value === role.name?.toUpperCase()))
  const selectedRole = roles.find((role) => role.name?.toUpperCase() === selectedName) || roles[0]
  const roleId = selectedRole?.id
  const permissionsQuery = useQuery({
    queryKey: ['rolePermissions', roleId],
    queryFn: () => rolePermissionService.getPermissions(roleId),
    enabled: Boolean(roleId),
  })
  const permissionMutation = useMutation({
    mutationFn: ({ id, module, action, enabled }) => rolePermissionService.changePermission(id, module, action, enabled),
    onSuccess: (updated, variables) => {
      queryClient.setQueryData(['rolePermissions', variables.id], updated)
      queryClient.invalidateQueries({ queryKey: ['roles'] })
      messageApi.success('Đã cập nhật quyền.')
    },
    onError: (error) => messageApi.error(getUserError(error)),
  })
  const catalog = catalogQuery.data || {}
  const permissions = permissionsQuery.data?.permissions
  const isAdmin = selectedRole?.name?.toUpperCase() === 'ADMIN'
  const loading = catalogQuery.isLoading || rolesQuery.isLoading || permissionsQuery.isLoading
  const error = catalogQuery.error || rolesQuery.error || permissionsQuery.error
  const modules = Object.entries(catalog)
  const actionKeys = ['read', 'create', 'update', 'delete']

  const columns = [
    {
      title: 'Chức năng',
      dataIndex: 'module',
      key: 'module',
      fixed: 'left',
      width: 220,
      render: (module) => <span className="rp-module-name">{moduleLabels[module] || module}</span>,
    },
    ...actionKeys.map((action) => ({
      title: actionLabels[action],
      key: action,
      align: 'center',
      width: 110,
      render: (_, row) => row.actions.includes(action) ? <Switch
        size="small"
        aria-label={`${actionLabels[action]} ${moduleLabels[row.module] || row.module}`}
        checked={Boolean(permissions?.[row.module]?.includes(action))}
        disabled={isAdmin || !permissions || permissionMutation.isPending}
        loading={permissionMutation.isPending && permissionMutation.variables?.module === row.module && permissionMutation.variables?.action === action}
        onChange={(enabled) => permissionMutation.mutate({ id: roleId, module: row.module, action, enabled })}
      /> : <Text type="secondary">—</Text>,
    })),
  ]

  return <div className="um-page rp-page">
    {contextHolder}
    <header className="um-hero">
      <div>
        <span className="um-eyebrow">QUẢN TRỊ HỆ THỐNG</span>
        <Title level={2}>Phân quyền vai trò</Title>
        <p>Quản lý quyền truy cập các chức năng theo từng vai trò.</p>
      </div>
      <div className="um-hero-icon"><SafetyCertificateOutlined /></div>
    </header>
    <Card className="um-main-card">
      <div className="um-toolbar">
        <div>
          <h2>Danh sách quyền</h2>
          <Text type="secondary">Chọn vai trò, sau đó bật hoặc tắt quyền tương ứng.</Text>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => {
          catalogQuery.refetch()
          rolesQuery.refetch()
          if (roleId) permissionsQuery.refetch()
        }}>Tải lại</Button>
      </div>
      <div className="rp-role-picker">
        <Text strong>Vai trò</Text>
        <Select
          aria-label="Chọn vai trò phân quyền"
          value={selectedRole?.name?.toUpperCase()}
          loading={rolesQuery.isLoading}
          placeholder="Chọn vai trò"
          onChange={setSelectedName}
          options={roles.map((role) => ({
            value: role.name.toUpperCase(),
            label: USER_ROLES.find((item) => item.value === role.name.toUpperCase())?.label || role.name,
          }))}
        />
        {selectedRole && <Tag color={USER_ROLE_COLORS[selectedRole.name?.toUpperCase()]}>
          {USER_ROLES.find((item) => item.value === selectedRole.name?.toUpperCase())?.label || selectedRole.name}
        </Tag>}
      </div>
      {isAdmin && <Alert type="info" showIcon className="rp-notice" message="Quyền của quản trị viên được cố định ở backend và không thể chỉnh sửa." />}
      {error && <Alert
        type="error"
        showIcon
        className="rp-notice"
        message="Không thể tải dữ liệu phân quyền"
        description={getUserError(error)}
        action={<Button size="small" onClick={() => {
          catalogQuery.refetch()
          rolesQuery.refetch()
          if (roleId) permissionsQuery.refetch()
        }}>Thử lại</Button>}
      />}
      {!loading && !error && selectedRole && permissions === null && <Alert type="error" showIcon className="rp-notice" message="Dữ liệu quyền của vai trò không hợp lệ." />}
      <Table
        rowKey="module"
        columns={columns}
        dataSource={modules.map(([module, actions]) => ({ module, actions }))}
        loading={loading}
        pagination={false}
        scroll={{ x: 660 }}
        locale={{ emptyText: roles.length ? 'Chưa có chức năng phân quyền.' : 'Chưa có vai trò khả dụng.' }}
      />
      <Space className="rp-footnote"><Text type="secondary">Các quyền thay đổi có hiệu lực sau khi lưu thành công.</Text></Space>
    </Card>
  </div>
}
