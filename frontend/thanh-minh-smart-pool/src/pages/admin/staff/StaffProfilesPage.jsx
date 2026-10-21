import { useState } from 'react'
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Empty, Input, Pagination, Select, Spin } from 'antd'
import { useAuthStore } from "../../../features/auth/store/authStore"
import StaffDetailsDrawer from "../../../features/staff/components/StaffDetailsDrawer"
import StaffEmploymentFormDrawer from "../../../features/staff/components/StaffEmploymentFormDrawer"
import StaffProfileCard from "../../../features/staff/components/StaffProfileCard"
import { useStaffs } from "../../../features/staff/hooks/useStaffs"
import { ServiceListHeader } from "../../../features/services/components/ServiceListChrome"
import "../../../features/services/styles/serviceManagement.css"
import "../../../components/shared/styles/adminActions.css"
import "../../../features/staff/styles/staffProfiles.css"

const statusOptions = [{ label: 'Đang làm việc', value: 'Working' }, { label: 'Không hoạt động', value: 'Inactive' }]
const roleOptions = [{ label: 'Quản trị viên', value: 'ADMIN' }, { label: 'Nhân viên cứu hộ', value: 'LIFEGUARD' }, { label: 'Nhân viên bán vé', value: 'SALES' }]

export default function StaffProfilesPage() {
  const role = useAuthStore((state) => state.session?.role)
  const [searchTerm, setSearchTerm] = useState('')
  const [status, setStatus] = useState()
  const [roleName, setRoleName] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [formStaff, setFormStaff] = useState(undefined)
  const [selectedStaff, setSelectedStaff] = useState(null)
  const isAdmin = role?.toUpperCase() === 'ADMIN'
  const query = useStaffs({ pageIndex, pageSize, searchTerm: searchTerm || undefined, status, roleName }, isAdmin)
  const resetPage = (setter) => (value) => { setter(value); setPageIndex(1) }
  if (!isAdmin) return <div className="service-list-page staff-profiles-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ quản trị viên mới có thể quản lý hồ sơ nhân viên." /></div>
  return <div className="service-list-page staff-profiles-page">
    <ServiceListHeader title={<>Hồ sơ nhân viên <span className="staff-profiles-count">{query.isLoading ? 'Đang tải…' : query.isError ? 'Không khả dụng' : `${query.data?.totalCount ?? 0} hồ sơ`}</span></>} action={<Button type="primary" icon={<PlusOutlined />} onClick={() => setFormStaff(null)}>Thêm nhân viên</Button>} />
    <section className="service-list-toolbar staff-profiles-toolbar" aria-label="Bộ lọc hồ sơ nhân viên">
      <div className="service-list-filters staff-profiles-filters">
        <label className="service-filter-control staff-profiles-search"><span>Tìm nhân viên</span><Input.Search allowClear value={searchTerm} onChange={(event) => resetPage(setSearchTerm)(event.target.value)} aria-label="Tìm theo tên, email hoặc số điện thoại" placeholder="Tên, email hoặc số điện thoại" /></label>
        <label className="service-filter-control"><span>Trạng thái</span><Select allowClear value={status} onChange={resetPage(setStatus)} placeholder="Tất cả trạng thái" options={statusOptions} /></label>
        <label className="service-filter-control"><span>Vai trò</span><Select allowClear value={roleName} onChange={resetPage(setRoleName)} placeholder="Tất cả vai trò" options={roleOptions} /></label>
      </div>
    </section>
    <section className="staff-profiles-content" aria-label="Danh sách hồ sơ nhân viên">
      {query.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
      {query.isError && <Empty description="Không thể tải danh sách nhân viên."><Button icon={<ReloadOutlined />} onClick={query.refetch}>Thử lại</Button></Empty>}
      {!query.isLoading && !query.isError && (query.data?.items?.length ?? 0) === 0 && <Empty description="Chưa có hồ sơ nhân viên phù hợp." />}
      {!query.isLoading && !query.isError && (query.data?.items?.length ?? 0) > 0 && <div className="staff-profiles-grid">
        {query.data.items.map((staff) => <StaffProfileCard key={staff.userId} staff={staff} onDetails={setSelectedStaff} onEdit={setFormStaff} />)}
      </div>}
      {!query.isLoading && !query.isError && (query.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={query.data.totalCount} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} showTotal={(total, range) => `${range[0]}-${range[1]} của ${total} nhân viên`} /></div>}
    </section>
    <StaffEmploymentFormDrawer open={formStaff !== undefined} staff={formStaff} onClose={() => setFormStaff(undefined)} />
    <StaffDetailsDrawer key={selectedStaff?.userId || 'staff-details'} open={Boolean(selectedStaff)} staff={selectedStaff} onClose={() => setSelectedStaff(null)} />
  </div>
}
