import { ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Empty, Spin, Table, Tag } from 'antd'
import { useAuthStore } from '../../../features/auth/store/authStore'
import { useShifts } from '../../../features/staff/hooks/useShifts'
import { ServiceListHeader } from '../../../features/services/components/ServiceListChrome'
import '../../../features/services/styles/serviceManagement.css'
import '../../../features/staff/styles/staffSchedule.css'

const columns = [
  { title: 'Tên ca', dataIndex: 'name', key: 'name' },
  { title: 'Bắt đầu', dataIndex: 'startTime', key: 'startTime', render: (value) => value?.slice(0, 5) || '-' },
  { title: 'Kết thúc', dataIndex: 'endTime', key: 'endTime', render: (value) => value?.slice(0, 5) || '-' },
  { title: 'Loại ca', key: 'fixed', render: () => <Tag>Ca cố định</Tag> },
]

export default function ShiftManagementPage() {
  const isAdmin = useAuthStore((state) => state.session?.role?.toUpperCase() === 'ADMIN')
  const query = useShifts({ pageIndex: 1, pageSize: 2 }, isAdmin)

  if (!isAdmin) return <div className="service-list-page staff-schedule-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ quản trị viên có thể xem ca làm việc." /></div>

  return <div className="service-list-page staff-schedule-page">
    <ServiceListHeader title="Ca làm việc" action={<Button icon={<ReloadOutlined />} loading={query.isFetching} onClick={() => query.refetch()}>Làm mới</Button>} />
    <section className="service-list-table staff-schedule-table" aria-label="Danh sách ca làm việc cố định">
      <Alert className="staff-schedule-alert" type="info" showIcon title="Hai ca làm việc cố định" description="Ca sáng: 06:00–11:30. Ca chiều: 13:30–19:30. Không tạo thêm hoặc thay đổi giờ ca. Ngày làm và nhân viên được chọn khi phân ca." />
      {query.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
      {query.isError && <Empty description="Không thể tải danh sách ca làm việc."><Button onClick={() => query.refetch()}>Thử lại</Button></Empty>}
      {!query.isLoading && !query.isError && !query.data?.items?.length && <Empty description="Chưa tải được hai ca cố định. Hãy khởi động lại máy chủ rồi làm mới." />}
      {!query.isLoading && !query.isError && query.data?.items?.length > 0 && <Table className="staff-schedule-alert" rowKey="id" columns={columns} dataSource={query.data.items} pagination={false} scroll={{ x: 560 }} />}
    </section>
  </div>
}
