import { useState } from 'react'
import { ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, DatePicker, Empty, Pagination, Select, Spin, Table, Tag } from 'antd'
import dayjs from 'dayjs'
import { useAuthStore } from '../../features/auth/store/authStore'
import { useStaffSchedulePage } from '../../features/staff/hooks/useStaffs'
import { ServiceListHeader } from '../../features/services/components/ServiceListChrome'
import '../../features/services/styles/serviceManagement.css'
import '../../features/shared/styles/adminActions.css'
import '../../features/staff/styles/staffSchedule.css'

const statusOptions = [{ label: 'Đã lên lịch', value: 'Scheduled' }, { label: 'Có mặt', value: 'Present' }, { label: 'Hoàn thành', value: 'Completed' }, { label: 'Đã hủy', value: 'Cancelled' }]

function vietnamToday() {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  const value = (type) => parts.find((part) => part.type === type)?.value
  return `${value('year')}-${value('month')}-${value('day')}`
}

function formatDate(value) {
  if (!value) return '-'
  const [year, month, day] = value.split('-')
  return year && month && day ? `${day}/${month}/${year}` : '-'
}

function formatTime(value) {
  return value ? value.slice(0, 5) : '-'
}

function isOvernight(item) {
  return Boolean(item.startTime && item.endTime && item.endTime < item.startTime)
}

function statusLabel(value) {
  return statusOptions.find((option) => option.value === value)?.label || value || 'Chưa xác định'
}

function statusColor(value) {
  if (value === 'Present') return 'success'
  if (value === 'Cancelled') return 'error'
  if (value === 'Completed') return 'blue'
  return 'default'
}

function ScheduleCards({ items, isAdmin }) {
  return <div className="staff-schedule-cards">{items.map((item) => <article className="staff-schedule-card" key={item.id}>
    {isAdmin && <div><span>Nhân viên</span><strong>{item.employeeName || '-'}</strong></div>}
    <div><span>Ngày làm</span><strong>{formatDate(item.workDate)}</strong></div>
    <div><span>Ca</span><strong>{item.shiftName || '-'}</strong></div>
    <div><span>Thời gian</span><strong>{formatTime(item.startTime)} - {formatTime(item.endTime)}{isOvernight(item) ? ' (ngày sau)' : ''}</strong></div>
    <div><span>Trạng thái</span><Tag color={statusColor(item.status)}>{statusLabel(item.status)}</Tag></div>
  </article>)}</div>
}

export default function StaffSchedulePage() {
  const session = useAuthStore((state) => state.session)
  const role = session?.role?.toUpperCase()
  const isAdmin = role === 'ADMIN'
  const isStaff = ['LIFEGUARD', 'SALES'].includes(role)
  const canRead = isAdmin || isStaff
  const [date, setDate] = useState(vietnamToday)
  const [status, setStatus] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const query = useStaffSchedulePage({ scope: isAdmin ? 'all' : 'self', sessionUserId: session?.id, params: { pageIndex, pageSize, date: date || undefined, status }, enabled: canRead })
  const columns = [
    ...(isAdmin ? [{ title: 'Nhân viên', dataIndex: 'employeeName', key: 'employeeName', render: (value) => value || '-' }] : []),
    { title: 'Ngày làm', dataIndex: 'workDate', key: 'workDate', render: formatDate },
    { title: 'Ca', dataIndex: 'shiftName', key: 'shiftName', render: (value) => value || '-' },
    { title: 'Bắt đầu', dataIndex: 'startTime', key: 'startTime', render: formatTime },
    { title: 'Kết thúc', dataIndex: 'endTime', key: 'endTime', render: (value, item) => <>{formatTime(value)}{isOvernight(item) && <span className="staff-schedule-overnight">Ngày sau</span>}</> },
    { title: 'Ca mẫu', dataIndex: 'isShiftActive', key: 'isShiftActive', render: (value) => <Tag color={value ? 'success' : 'default'}>{value ? 'Đang hoạt động' : 'Không hoạt động'}</Tag> },
    { title: 'Trạng thái', dataIndex: 'status', key: 'status', render: (value) => <Tag color={statusColor(value)}>{statusLabel(value)}</Tag> },
  ]

  if (!canRead) return <div className="service-list-page staff-schedule-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ quản trị viên và nhân viên mới có thể xem lịch làm việc." /></div>

  return <div className="service-list-page staff-schedule-page">
    <ServiceListHeader title={<>Lịch làm việc <span className="staff-schedule-count">{query.isLoading ? 'Đang tải…' : query.isError ? 'Không khả dụng' : `${query.data?.totalCount ?? 0} lịch`}</span></>} />
    <section className="service-list-toolbar staff-schedule-toolbar" aria-label="Bộ lọc lịch làm việc"><div className="service-list-filters staff-schedule-filters"><label className="service-filter-control"><span>Ngày làm</span><DatePicker allowClear value={date ? dayjs(date) : null} format="DD/MM/YYYY" onChange={(value) => { setDate(value?.format('YYYY-MM-DD')); setPageIndex(1) }} /></label>{isAdmin && <label className="service-filter-control"><span>Trạng thái</span><Select allowClear value={status} onChange={(value) => { setStatus(value); setPageIndex(1) }} placeholder="Tất cả trạng thái" options={statusOptions} /></label>}<div className="admin-action-group"><Button className="admin-action-button admin-action-button--primary" icon={<ReloadOutlined />} onClick={query.refetch}>Làm mới</Button></div></div></section>
    <section className="service-list-table staff-schedule-table" aria-label="Danh sách lịch làm việc">
      {query.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
      {query.isError && <Empty description="Không thể tải lịch làm việc."><Button icon={<ReloadOutlined />} onClick={query.refetch}>Thử lại</Button></Empty>}
      {!query.isLoading && !query.isError && (query.data?.items?.length ?? 0) === 0 && <Empty description="Chưa có lịch làm việc phù hợp." />}
      {!query.isLoading && !query.isError && (query.data?.items?.length ?? 0) > 0 && <><div className="staff-schedule-desktop"><Table rowKey="id" columns={columns} dataSource={query.data.items} pagination={false} /></div><ScheduleCards items={query.data.items} isAdmin={isAdmin} /></>}
      {!query.isLoading && !query.isError && (query.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={query.data.totalCount} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} /></div>}
    </section>
  </div>
}
