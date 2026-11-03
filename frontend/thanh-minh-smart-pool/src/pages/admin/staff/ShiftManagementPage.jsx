import { useState } from 'react'
import { EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Empty, Modal, Spin, Table, Tag, message } from 'antd'
import { useAuthStore } from '../../../features/auth/store/authStore'
import { useShifts, useUpdateShift } from '../../../features/staff/hooks/useShifts'
import ShiftCreateDrawer from '../../../features/staff/components/ShiftCreateDrawer'
import { formatTime, isOvernight } from '../../../features/staff/schedule/scheduleUtils'
import { ServiceListHeader } from '../../../features/services/components/ServiceListChrome'
import '../../../features/services/styles/serviceManagement.css'
import '../../../features/staff/styles/staffSchedule.css'

export default function ShiftManagementPage() {
  const isAdmin = useAuthStore((state) => state.session?.role?.toUpperCase() === 'ADMIN')
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingShift, setEditingShift] = useState(null)
  const query = useShifts({ pageIndex, pageSize }, isAdmin)
  const update = useUpdateShift()

  const changeStatus = (shift) => {
    if (update.isPending) return
    Modal.confirm({
      title: shift.isActive ? 'Ngừng hoạt động ca làm việc?' : 'Kích hoạt ca làm việc?',
      content: shift.isActive ? `${shift.name}: lịch đã phân vẫn được giữ. Ca sẽ không nhận phân công mới.` : `${shift.name}: ca có thể nhận phân công mới.`,
      okText: shift.isActive ? 'Ngừng hoạt động' : 'Kích hoạt',
      cancelText: 'Đóng',
      autoFocusButton: 'cancel',
      onOk: async () => {
        try {
          await update.mutateAsync({ id: shift.id, data: { name: shift.name, startTime: shift.startTime, endTime: shift.endTime, isActive: !shift.isActive } })
          message.success(shift.isActive ? 'Đã ngừng hoạt động ca.' : 'Đã kích hoạt ca.')
        } catch (error) {
          message.error(error?.response?.data?.detail || error?.response?.data?.message || 'Không thể đổi trạng thái ca. Hãy kiểm tra danh sách trước khi thử lại.')
        }
      },
    })
  }

  const columns = [
    { title: 'Tên ca', dataIndex: 'name', key: 'name', render: (name) => <strong>{name}</strong> },
    { title: 'Bắt đầu', dataIndex: 'startTime', key: 'startTime', render: formatTime },
    { title: 'Kết thúc', dataIndex: 'endTime', key: 'endTime', render: (value, shift) => <>{formatTime(value)}{isOvernight(shift) && <span className="staff-schedule-overnight">Ngày hôm sau</span>}</> },
    { title: 'Trạng thái', dataIndex: 'isActive', key: 'isActive', render: (active) => <Tag color={active ? 'success' : 'default'}>{active ? 'Đang hoạt động' : 'Ngừng hoạt động'}</Tag> },
    { title: 'Thao tác', key: 'actions', render: (_, shift) => <div className="staff-schedule-header-actions"><Button type="link" icon={<EditOutlined />} disabled={update.isPending} onClick={() => { setEditingShift(shift); setDrawerOpen(true) }}>Sửa</Button><Button type="link" disabled={update.isPending} loading={update.isPending && update.variables?.id === shift.id} onClick={() => changeStatus(shift)}>{shift.isActive ? 'Ngừng hoạt động' : 'Kích hoạt'}</Button></div> },
  ]

  if (!isAdmin) return <div className="service-list-page staff-schedule-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ quản trị viên có thể quản lý ca làm việc." /></div>

  return <div className="service-list-page staff-schedule-page">
    <ServiceListHeader title="Ca làm việc" action={<div className="staff-schedule-header-actions"><Button icon={<ReloadOutlined />} loading={query.isFetching} onClick={() => query.refetch()}>Làm mới</Button><Button type="primary" icon={<PlusOutlined />} disabled={update.isPending} onClick={() => { setEditingShift(null); setDrawerOpen(true) }}>Thêm ca</Button></div>} />
    {update.isError && <Alert className="staff-schedule-alert" type="error" showIcon title="Không thể đổi trạng thái ca" description={update.error?.response?.data?.detail || update.error?.response?.data?.message || 'Chưa xác định được kết quả. Hãy làm mới danh sách trước khi thử lại.'} />}
    <section className="service-list-table staff-schedule-table" aria-label="Danh sách ca làm việc">
      {query.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
      {query.isError && <Empty description="Không thể tải danh sách ca làm việc."><Button onClick={() => query.refetch()}>Thử lại</Button></Empty>}
      {!query.isLoading && !query.isError && <Table rowKey="id" columns={columns} dataSource={query.data?.items || []} locale={{ emptyText: 'Chưa có ca làm việc. Chọn Thêm ca để tạo ca mới.' }} pagination={{ current: pageIndex, pageSize, total: query.data?.totalCount || 0, showSizeChanger: true, pageSizeOptions: ['10', '20', '50'], onChange: (page, size) => { setPageIndex(page); setPageSize(size) } }} scroll={{ x: 760 }} />}
    </section>
    <ShiftCreateDrawer open={drawerOpen} shift={editingShift} onClose={() => setDrawerOpen(false)} />
  </div>
}
