import { useState } from 'react'
import { Alert, Button, DatePicker, Descriptions, Drawer, Pagination, Select, Spin, Table, Tabs, Tag } from 'antd'
import dayjs from 'dayjs'
import { useStaffSalaries, useStaffSchedule } from '../hooks/useStaffs'

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function formatDate(value) {
  return value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short' }).format(new Date(value)) : '-'
}

function QueryTable({ query, columns, emptyText, pagination, onPageChange }) {
  if (query.isLoading) return <div className="admin-management-state"><Spin size="large" /></div>
  if (query.isError) return <Alert type="error" showIcon title="Không thể tải dữ liệu" description="Hãy thử lại sau." action={<Button size="small" onClick={query.refetch}>Thử lại</Button>} />
  return <><Table rowKey="id" columns={columns} dataSource={query.data?.items || []} pagination={false} scroll={{ x: true }} locale={{ emptyText }} />
    {(query.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pagination.pageIndex} pageSize={pagination.pageSize} total={query.data.totalCount} showSizeChanger onChange={onPageChange} /></div>}
  </>
}

function ScheduleContent({ employeeId, attendance }) {
  const [date, setDate] = useState()
  const [status, setStatus] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const query = useStaffSchedule(employeeId, { date, status, pageIndex, pageSize }, true)
  const columns = attendance
    ? [{ title: 'Ngày', dataIndex: 'workDate', render: formatDate }, { title: 'Ca', dataIndex: 'shiftName' }, { title: 'Vào ca', dataIndex: 'checkInTime', render: formatDate }, { title: 'Ra ca', dataIndex: 'checkOutTime', render: formatDate }, { title: 'Trạng thái', dataIndex: 'status', render: (value) => <Tag>{value}</Tag> }]
    : [{ title: 'Ngày làm', dataIndex: 'workDate', render: formatDate }, { title: 'Ca', dataIndex: 'shiftName' }, { title: 'Bắt đầu', dataIndex: 'startTime' }, { title: 'Kết thúc', dataIndex: 'endTime' }, { title: 'Trạng thái', dataIndex: 'status', render: (value) => <Tag>{value}</Tag> }]
  return <><div className="service-list-filters"><DatePicker allowClear value={date ? dayjs(date) : null} onChange={(value) => { setDate(value?.format('YYYY-MM-DD')); setPageIndex(1) }} placeholder="Lọc theo ngày" /><Select allowClear value={status} onChange={(value) => { setStatus(value); setPageIndex(1) }} placeholder="Lọc trạng thái" options={[{ value: 'Scheduled', label: 'Đã lên lịch' }, { value: 'Present', label: 'Có mặt' }, { value: 'Completed', label: 'Hoàn thành' }, { value: 'Cancelled', label: 'Đã hủy' }]} /></div><QueryTable query={query} columns={columns} emptyText={attendance ? 'Chưa có chấm công.' : 'Chưa có lịch làm việc.'} pagination={{ pageIndex, pageSize }} onPageChange={(page, size) => { setPageIndex(page); setPageSize(size) }} /></>
}

function SalaryContent({ employeeId }) {
  const [month, setMonth] = useState()
  const [year, setYear] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const query = useStaffSalaries(employeeId, { month, year, pageIndex, pageSize }, true)
  const columns = [{ title: 'Kỳ lương', key: 'period', render: (_, record) => `${record.month}/${record.year}` }, { title: 'Số ca', dataIndex: 'totalShifts' }, { title: 'Thưởng', dataIndex: 'bonus', render: (value) => currencyFormatter.format(value) }, { title: 'Khấu trừ', dataIndex: 'deduction', render: (value) => currencyFormatter.format(value) }, { title: 'Thực nhận', dataIndex: 'netSalary', render: (value) => <strong>{currencyFormatter.format(value)}</strong> }]
  return <><div className="service-list-filters"><Select allowClear value={month} onChange={(value) => { setMonth(value); setPageIndex(1) }} placeholder="Tháng" options={Array.from({ length: 12 }, (_, index) => ({ value: index + 1, label: `Tháng ${index + 1}` }))} /><Select allowClear value={year} onChange={(value) => { setYear(value); setPageIndex(1) }} placeholder="Năm" options={Array.from({ length: 5 }, (_, index) => ({ value: dayjs().year() - index, label: String(dayjs().year() - index) }))} /></div><QueryTable query={query} columns={columns} emptyText="Chưa có bảng lương." pagination={{ pageIndex, pageSize }} onPageChange={(page, size) => { setPageIndex(page); setPageSize(size) }} /></>
}

export default function StaffDetailsDrawer({ open, onClose, staff }) {
  const [tab, setTab] = useState('profile')
  if (!staff) return null
  const profile = <Descriptions bordered column={1} size="small"><Descriptions.Item label="Họ tên">{staff.fullName || '-'}</Descriptions.Item><Descriptions.Item label="Tên đăng nhập">{staff.username || '-'}</Descriptions.Item><Descriptions.Item label="Email">{staff.email || '-'}</Descriptions.Item><Descriptions.Item label="Điện thoại">{staff.phone || '-'}</Descriptions.Item><Descriptions.Item label="Vai trò">{staff.roleName || '-'}</Descriptions.Item><Descriptions.Item label="Ngày vào làm">{formatDate(staff.joinDate)}</Descriptions.Item><Descriptions.Item label="Lương cơ bản">{currencyFormatter.format(staff.baseSalary || 0)}</Descriptions.Item><Descriptions.Item label="Trạng thái"><Tag color={staff.status === 'Working' ? 'success' : 'default'}>{staff.status || '-'}</Tag></Descriptions.Item></Descriptions>
  return <Drawer rootClassName="staff-profiles-drawer" title={`Hồ sơ: ${staff.fullName || staff.username || ''}`} open={open} onClose={onClose} size="large"><Tabs activeKey={tab} onChange={setTab} items={[{ key: 'profile', label: 'Hồ sơ', children: profile }, { key: 'schedule', label: 'Lịch làm việc', children: tab === 'schedule' ? <ScheduleContent employeeId={staff.userId} attendance={false} /> : null }, { key: 'attendance', label: 'Chấm công', children: tab === 'attendance' ? <ScheduleContent employeeId={staff.userId} attendance /> : null }, { key: 'salary', label: 'Lương', children: tab === 'salary' ? <SalaryContent employeeId={staff.userId} /> : null }]} /></Drawer>
}
