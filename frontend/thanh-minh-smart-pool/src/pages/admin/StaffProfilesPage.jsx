import { useState } from 'react'
import { EditOutlined, EyeOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Empty, Input, Pagination, Select, Spin, Table, Tag } from 'antd'
import { useAuthStore } from '../../features/auth/store/authStore'
import StaffDetailsDrawer from '../../features/staff/components/StaffDetailsDrawer'
import StaffEmploymentFormDrawer from '../../features/staff/components/StaffEmploymentFormDrawer'
import { useStaffs } from '../../features/staff/hooks/useStaffs'
import { ServiceListHeader } from '../../features/services/components/ServiceListChrome'
import '../../features/services/styles/serviceManagement.css'
import '../../features/shared/styles/adminActions.css'
import '../../features/staff/styles/staffProfiles.css'

const statusOptions = [{ label: 'Đang làm việc', value: 'Working' }, { label: 'Không hoạt động', value: 'Inactive' }]
const roleOptions = [{ label: 'Quản trị viên', value: 'ADMIN' }, { label: 'Nhân viên', value: 'STAFF' }]
const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function StaffRoleTag({ roleName }) {
  const role = roleName?.toUpperCase()
  if (role === 'ADMIN') return <Tag color="purple">Quản trị viên</Tag>
  if (role === 'STAFF') return <Tag color="blue">Nhân viên</Tag>
  return <Tag>{roleName || '-'}</Tag>
}

function StaffMobileCards({ staffList, onDetails, onEdit }) {
  return <div className="staff-profiles-mobile-list">{staffList.map((staff) => <article className="staff-profile-mobile-card" key={staff.userId}>
    <div className="staff-profile-mobile-heading"><div className="staff-identity-cell"><strong>{staff.fullName || staff.username || '-'}</strong><span className="staff-username">{staff.username || '-'}</span></div><StaffRoleTag roleName={staff.roleName} /></div>
    <dl>
      <div><dt>Liên hệ</dt><dd>{staff.phone || staff.email || '-'}</dd></div>
      <div><dt>Ngày vào làm</dt><dd>{staff.joinDate || '-'}</dd></div>
      <div><dt>Lương cơ bản</dt><dd className="staff-salary-cell">{currencyFormatter.format(staff.baseSalary || 0)}</dd></div>
      <div><dt>Trạng thái</dt><dd><Tag color={staff.status === 'Working' ? 'success' : 'default'}>{staff.status === 'Working' ? 'Đang làm việc' : 'Không hoạt động'}</Tag></dd></div>
    </dl>
    <div className="admin-action-group"><Button className="admin-action-button admin-action-button--view" icon={<EyeOutlined />} onClick={() => onDetails(staff)}>Xem</Button><Button className="admin-action-button admin-action-button--edit" icon={<EditOutlined />} onClick={() => onEdit(staff)}>Sửa</Button></div>
  </article>)}</div>
}

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
  const columns = [
    {
      title: 'Nhân viên', width: 200,
      key: 'staff',
      render: (_, staff) => <div className="staff-identity-cell"><strong>{staff.fullName || staff.username || '-'}</strong><span className="staff-username">{staff.username || '-'}</span><span>{staff.phone || staff.email || '-'}</span></div>,
    },
    { title: 'Vai trò', dataIndex: 'roleName', width: 125, render: (value) => <StaffRoleTag roleName={value} /> },
    { title: 'Ngày vào làm', dataIndex: 'joinDate', width: 130, render: (value) => value || '-' },
    { title: 'Lương cơ bản', dataIndex: 'baseSalary', width: 145, align: 'right', render: (value) => <span className="staff-salary-cell">{currencyFormatter.format(value || 0)}</span> },
    { title: 'Trạng thái', dataIndex: 'status', width: 140, render: (value) => <Tag color={value === 'Working' ? 'success' : 'default'}>{value === 'Working' ? 'Đang làm việc' : 'Không hoạt động'}</Tag> },
    {
      title: 'Thao tác', key: 'actions', width: 180, align: 'right', render: (_, staff) => <div className="admin-action-group">
        <Button className="admin-action-button admin-action-button--view" icon={<EyeOutlined />} onClick={() => setSelectedStaff(staff)}>Xem</Button>
        <Button className="admin-action-button admin-action-button--edit" icon={<EditOutlined />} onClick={() => setFormStaff(staff)}>Sửa</Button>
      </div>,
    },
  ]
  if (!isAdmin) return <div className="service-list-page staff-profiles-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ quản trị viên mới có thể quản lý hồ sơ nhân viên." /></div>
  return <div className="service-list-page staff-profiles-page">
    <ServiceListHeader title="Hồ sơ nhân viên" action={<Button type="primary" icon={<PlusOutlined />} onClick={() => setFormStaff(null)}>Thêm nhân viên</Button>} />
    <section className="service-list-toolbar staff-profiles-toolbar" aria-label="Bộ lọc hồ sơ nhân viên">
      <div className="service-list-filters staff-profiles-filters">
        <label className="service-filter-control staff-profiles-search"><span>Tìm nhân viên</span><Input.Search allowClear value={searchTerm} onChange={(event) => resetPage(setSearchTerm)(event.target.value)} aria-label="Tìm theo tên, email hoặc số điện thoại" placeholder="Tên, email hoặc số điện thoại" /></label>
        <label className="service-filter-control"><span>Trạng thái</span><Select allowClear value={status} onChange={resetPage(setStatus)} placeholder="Tất cả trạng thái" options={statusOptions} /></label>
        <label className="service-filter-control"><span>Vai trò</span><Select allowClear value={roleName} onChange={resetPage(setRoleName)} placeholder="Tất cả vai trò" options={roleOptions} /></label>
      </div>
    </section>
    <section className="service-list-table" aria-label="Danh sách hồ sơ nhân viên">
      {query.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
      {query.isError && <Empty description="Không thể tải danh sách nhân viên."><Button icon={<ReloadOutlined />} onClick={query.refetch}>Thử lại</Button></Empty>}
      {!query.isLoading && !query.isError && <Table className="staff-profiles-table" rowKey="userId" columns={columns} dataSource={query.data?.items || []} pagination={false} scroll={{ x: 920 }} locale={{ emptyText: 'Chưa có hồ sơ nhân viên phù hợp.' }} />}
      {!query.isLoading && !query.isError && <StaffMobileCards staffList={query.data?.items || []} onDetails={setSelectedStaff} onEdit={setFormStaff} />}
      {!query.isLoading && !query.isError && (query.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={query.data.totalCount} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} showTotal={(total, range) => `${range[0]}-${range[1]} của ${total} nhân viên`} /></div>}
    </section>
    <StaffEmploymentFormDrawer open={formStaff !== undefined} staff={formStaff} onClose={() => setFormStaff(undefined)} />
    <StaffDetailsDrawer key={selectedStaff?.userId || 'staff-details'} open={Boolean(selectedStaff)} staff={selectedStaff} onClose={() => setSelectedStaff(null)} />
  </div>
}
