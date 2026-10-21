import { useState } from 'react'
import { EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Empty, message, Pagination, Popconfirm, Select, Spin, Table, Tag } from 'antd'
import { useAuthStore } from "../../../features/auth/store/authStore"
import ShiftCreateDrawer from "../../../features/staff/components/ShiftCreateDrawer"
import { useShifts, useUpdateShift } from "../../../features/staff/hooks/useShifts"
import { ServiceListHeader } from "../../../features/services/components/ServiceListChrome"
import "../../../features/services/styles/serviceManagement.css"
import "../../../components/shared/styles/adminActions.css"
import "../../../features/staff/styles/staffSchedule.css"

const activeOptions = [{ label: 'Đang hoạt động', value: 'true' }, { label: 'Không hoạt động', value: 'false' }]

function formatTime(value) {
  return value ? value.slice(0, 5) : '-'
}

function isOvernight(shift) {
  return Boolean(shift.startTime && shift.endTime && shift.endTime < shift.startTime)
}

export default function ShiftManagementPage() {
  const role = useAuthStore((state) => state.session?.role)
  const isAdmin = role?.toUpperCase() === 'ADMIN'
  const [isActive, setIsActive] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [isDrawerOpen, setDrawerOpen] = useState(false)
  const [selectedShift, setSelectedShift] = useState(null)
  const updateShift = useUpdateShift()
  const query = useShifts({ pageIndex, pageSize, isActive }, isAdmin)
  const openShiftDrawer = (shift = null) => {
    setSelectedShift(shift)
    setDrawerOpen(true)
  }
  const handleStatusChange = async (shift) => {
    if (updateShift.isPending) return
    updateShift.reset()
    try {
      await updateShift.mutateAsync({
        id: shift.id,
        data: {
          name: shift.name,
          startTime: shift.startTime,
          endTime: shift.endTime,
          isActive: !shift.isActive,
        },
      })
      message.success(`Đã ${shift.isActive ? 'ngừng kích hoạt' : 'kích hoạt lại'} ca “${shift.name}”.`)
    } catch {
      // The mutation error is shown in the page alert; keep the server's row status unchanged.
    }
  }
  const columns = [
    { title: 'Tên ca', dataIndex: 'name', key: 'name' },
    { title: 'Bắt đầu', dataIndex: 'startTime', key: 'startTime', render: formatTime },
    { title: 'Kết thúc', dataIndex: 'endTime', key: 'endTime', render: formatTime },
    { title: 'Qua ngày', key: 'overnight', render: (_, shift) => isOvernight(shift) ? <Tag color="cyan">Kết thúc ngày sau</Tag> : '-' },
    { title: 'Trạng thái', dataIndex: 'isActive', key: 'isActive', render: (value) => <Tag color={value ? 'success' : 'default'}>{value ? 'Đang hoạt động' : 'Không hoạt động'}</Tag> },
    ...(isAdmin ? [{
      title: 'Thao tác',
      key: 'actions',
      render: (_, shift) => <div className="admin-action-group">
        <Button className="admin-action-button" icon={<EditOutlined />} disabled={updateShift.isPending} onClick={() => openShiftDrawer(shift)}>Sửa</Button>
        <Popconfirm
          title={`${shift.isActive ? 'Ngừng kích hoạt' : 'Kích hoạt lại'} ca “${shift.name}”?`}
          description={shift.isActive ? 'Ca đã lưu vẫn được giữ nguyên; ca này sẽ không còn được phân công mới.' : 'Ca này sẽ được phép phân công mới.'}
          okText={shift.isActive ? 'Ngừng kích hoạt' : 'Kích hoạt lại'}
          cancelText="Hủy"
          onConfirm={() => handleStatusChange(shift)}
          okButtonProps={{ danger: shift.isActive, loading: updateShift.isPending, disabled: updateShift.isPending }}
          cancelButtonProps={{ disabled: updateShift.isPending }}
        >
          <Button className="admin-action-button" danger={shift.isActive} disabled={updateShift.isPending}>{shift.isActive ? 'Ngừng kích hoạt' : 'Kích hoạt lại'}</Button>
        </Popconfirm>
      </div>,
    }] : []),
  ]

  if (!isAdmin) return <div className="service-list-page staff-schedule-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ quản trị viên có thể xem ca làm việc." /></div>

  return <div className="service-list-page staff-schedule-page">
    <ServiceListHeader title={<>Ca làm việc <span className="staff-schedule-count">{query.isLoading ? 'Đang tải…' : query.isError ? 'Không khả dụng' : `${query.data?.totalCount ?? 0} ca`}</span></>} action={isAdmin ? <Button type="primary" icon={<PlusOutlined />} onClick={() => openShiftDrawer()}>Tạo ca</Button> : null} />
    <section className="service-list-toolbar staff-schedule-toolbar" aria-label="Bộ lọc ca làm việc"><div className="service-list-filters staff-schedule-filters"><label className="service-filter-control"><span>Trạng thái</span><Select allowClear value={isActive === undefined ? undefined : String(isActive)} onChange={(value) => { setIsActive(value === undefined ? undefined : value === 'true'); setPageIndex(1) }} placeholder="Tất cả trạng thái" options={activeOptions} /></label><div className="admin-action-group"><Button className="admin-action-button admin-action-button--primary" icon={<ReloadOutlined />} onClick={query.refetch}>Làm mới</Button></div></div></section>
    <section className="service-list-table staff-schedule-table" aria-label="Danh sách ca làm việc">
      {updateShift.error && <Alert type="error" showIcon title="Không thể thay đổi trạng thái ca" description={updateShift.error?.response?.data?.detail || updateShift.error?.response?.data?.message || 'Máy chủ không thể cập nhật trạng thái ca.'} />}
      {query.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
      {query.isError && <Empty description="Không thể tải danh sách ca làm việc."><Button icon={<ReloadOutlined />} onClick={query.refetch}>Thử lại</Button></Empty>}
      {!query.isLoading && !query.isError && (query.data?.items?.length ?? 0) === 0 && <Empty description="Chưa có ca làm việc phù hợp." />}
      {!query.isLoading && !query.isError && (query.data?.items?.length ?? 0) > 0 && <Table rowKey="id" columns={columns} dataSource={query.data.items} pagination={false} />}
      {!query.isLoading && !query.isError && (query.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={query.data.totalCount} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} /></div>}
    </section>
    <ShiftCreateDrawer open={isDrawerOpen} shift={selectedShift} onClose={() => { setDrawerOpen(false); setSelectedShift(null) }} />
  </div>
}
