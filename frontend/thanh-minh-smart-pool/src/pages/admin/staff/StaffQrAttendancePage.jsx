import { useState } from 'react'
import { ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, DatePicker, Descriptions, Empty, Select, Spin, Tag } from 'antd'
import dayjs from 'dayjs'
import { useAuthStore } from "../../../features/auth/store/authStore"
import QrDisplay from "../../../features/qr/components/QrDisplay"
import { useAttendanceSchedule } from "../../../features/staff/hooks/useStaffs"
import { useStaffAttendanceQr } from "../../../features/staff/hooks/useStaffAttendanceQr"
import { formatTime, vietnamToday } from "../../../features/staff/schedule/scheduleUtils"
import { ServiceListHeader } from "../../../features/services/components/ServiceListChrome"
import "../../../features/services/styles/serviceManagement.css"
import "../../../components/shared/styles/adminActions.css"
import "../../../features/staff/styles/staffSchedule.css"

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/
const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const vietnamDate = new Intl.DateTimeFormat('vi-VN', { dateStyle: 'full', timeZone: 'Asia/Ho_Chi_Minh' })

function isValidWorkDate(value) {
  if (!DATE_PATTERN.test(value)) return false
  const parsed = new Date(`${value}T00:00:00.000Z`)
  return !Number.isNaN(parsed.getTime()) && parsed.toISOString().slice(0, 10) === value
}

function supportedAction(assignment) {
  if (assignment?.status === 'Scheduled' && !assignment.checkInTime && !assignment.checkOutTime) return 'CheckIn'
  if (assignment?.status === 'Present' && assignment.checkInTime && !assignment.checkOutTime) return 'CheckOut'
  return null
}

function groupByShift(assignments) {
  const groups = new Map()
  for (const assignment of assignments) {
    let group = groups.get(assignment.shiftId)
    if (!group) {
      group = { shiftId: assignment.shiftId, shift: assignment, assignments: [] }
      groups.set(assignment.shiftId, group)
    }
    group.assignments.push(assignment)
  }
  return [...groups.values()]
}

function eligibleCount(group, action) {
  return group?.assignments.filter((assignment) => supportedAction(assignment) === action).length ?? 0
}

function actionLabel(action) {
  return action === 'CheckIn' ? 'Vào ca' : 'Ra ca'
}

function formatWorkDate(value) {
  return vietnamDate.format(new Date(`${value}T12:00:00+07:00`))
}

function secondsUntil(value, now) {
  return Math.max(0, Math.ceil((Date.parse(value) - now) / 1_000))
}

function formatCountdown(seconds) {
  return `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`
}

function sessionErrorMessage(error) {
  if (error?.operation === 'expired') return 'Mã QR đã hết hạn. Không hiển thị mã cũ.'
  if (error?.error?.response?.status === 409) return error.error.response.data?.message || 'Máy chủ chưa thể cấp mã QR cho ca này.'
  if (error?.operation === 'stop') return 'Không xác định được kết quả dừng phiên. QR vẫn bị ẩn và không gửi lại tự động.'
  if (!error?.error?.response) return 'Không xác định được kết quả phiên QR. Không gửi lại tự động.'
  return 'Máy chủ không thể cấp hoặc làm mới mã QR. Không hiển thị mã cũ.'
}

export default function StaffQrAttendancePage() {
  const session = useAuthStore((state) => state.session)
  const isAdmin = session?.role?.toUpperCase() === 'ADMIN'
  const [workDate, setWorkDate] = useState(vietnamToday)
  const [shiftId, setShiftId] = useState()
  const [sessionAction, setSessionAction] = useState()
  const attendance = useAttendanceSchedule(session?.id, workDate, isAdmin)
  const qrSession = useStaffAttendanceQr()
  const assignments = attendance.data?.items || []
  const shiftGroups = groupByShift(assignments)
  const selectedGroup = shiftGroups.find((group) => group.shiftId === shiftId)
  const locked = qrSession.isBusy || qrSession.state.phase === 'active' || qrSession.state.phase === 'stopUncertain'
  const canStart = (action) => Boolean(selectedGroup && eligibleCount(selectedGroup, action) > 0 && isValidWorkDate(workDate) && GUID_PATTERN.test(selectedGroup.shiftId)) && !locked
  const tokenSeconds = qrSession.state.session ? secondsUntil(qrSession.state.session.tokenExpiresAtUtc, qrSession.now) : 0
  const sessionSeconds = qrSession.state.session ? secondsUntil(qrSession.state.session.sessionExpiresAtUtc, qrSession.now) : 0

  const changeDate = (value) => {
    const nextDate = value?.format('YYYY-MM-DD')
    if (!nextDate) return
    setWorkDate(nextDate)
    setShiftId(undefined)
  }

  const start = (action) => {
    if (!canStart(action)) return
    setSessionAction(action)
    void qrSession.start({ shiftId: selectedGroup.shiftId, workDate, action })
  }

  if (!isAdmin) return <div className="service-list-page staff-schedule-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ quản trị viên có thể tạo phiên chấm công QR." /></div>

  return <div className="service-list-page staff-schedule-page">
    <ServiceListHeader title="Chấm công QR" />
    <section className="service-list-toolbar staff-schedule-toolbar" aria-label="Chọn phiên chấm công QR">
      <div className="service-list-filters staff-schedule-filters">
        <label className="service-filter-control"><span>Ngày làm việc</span><DatePicker value={dayjs(workDate)} format="DD/MM/YYYY" allowClear={false} disabled={locked} onChange={changeDate} /></label>
        <label className="service-filter-control"><span>Ca đã phân công</span><Select
          aria-label="Ca đã phân công"
          value={shiftId}
          disabled={locked || attendance.isLoading || attendance.isError}
          placeholder="Chọn ca"
          onChange={setShiftId}
          options={shiftGroups.map((group) => ({
            value: group.shiftId,
            label: `${group.shift.shiftName || 'Ca làm việc'} · ${formatTime(group.shift.startTime)} - ${formatTime(group.shift.endTime)} · ${group.assignments.length} nhân viên`,
          }))}
        /></label>
      </div>
      <div className="admin-action-group">
        <Button icon={<ReloadOutlined />} onClick={() => attendance.refetch()} loading={attendance.isFetching} disabled={locked}>Làm mới ca</Button>
        <Button className="admin-action-button admin-action-button--primary" type="primary" onClick={() => start('CheckIn')} loading={qrSession.state.phase === 'starting' && sessionAction === 'CheckIn'} disabled={!canStart('CheckIn')}>Tạo QR vào ca · {eligibleCount(selectedGroup, 'CheckIn')}</Button>
        <Button className="admin-action-button" onClick={() => start('CheckOut')} loading={qrSession.state.phase === 'starting' && sessionAction === 'CheckOut'} disabled={!canStart('CheckOut')}>Tạo QR ra ca · {eligibleCount(selectedGroup, 'CheckOut')}</Button>
      </div>
    </section>
    <section className="service-list-table staff-schedule-table" aria-label="Phiên chấm công QR">
      <Alert type="info" showIcon title="Một mã QR dùng chung cho ca" description="Mỗi nhân viên đủ điều kiện có thể quét cùng mã một lần. Máy chủ kiểm tra riêng phân công, thời gian và trạng thái của từng người." />
      {attendance.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
      {attendance.isError && <Empty description="Không thể tải phân công đã lưu trong ngày này."><Button onClick={() => attendance.refetch()}>Thử lại</Button></Empty>}
      {!attendance.isLoading && !attendance.isError && assignments.length === 0 && <Empty description="Chưa có phân công đã lưu cho ngày này." />}
      {!attendance.isLoading && !attendance.isError && selectedGroup && eligibleCount(selectedGroup, 'CheckIn') === 0 && eligibleCount(selectedGroup, 'CheckOut') === 0 && <Alert className="staff-schedule-alert" type="warning" showIcon title="Chưa có nhân viên đủ điều kiện" description="Mã vào ca cần ít nhất một phân công chưa chấm công. Mã ra ca cần ít nhất một nhân viên đã vào ca." />}
      {qrSession.error && <Alert className="staff-schedule-alert" type={qrSession.error.operation === 'stop' ? 'warning' : 'error'} showIcon title="Phiên QR không thể tiếp tục" description={sessionErrorMessage(qrSession.error)} />}
      {qrSession.state.phase === 'stopUncertain' && qrSession.state.stopBoundary && <Alert className="staff-schedule-alert" type="warning" showIcon title="Phiên dừng chưa được xác nhận" description={`QR vẫn bị ẩn. Phiên có thể còn hiệu lực đến ${new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'medium', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(qrSession.state.stopBoundary))}.`} />}
      {qrSession.state.phase === 'active' && qrSession.state.session && selectedGroup && <section className="staff-week-shift-card staff-schedule-alert" aria-label="Mã QR đang hoạt động">
        <header className="staff-week-shift-heading"><strong>Phiên QR đang hoạt động</strong><span>Mỗi mã QR dùng tối đa 60 giây; phiên kéo dài tối đa 15 phút</span></header>
        <Descriptions size="small" layout="vertical" column={{ xs: 1, sm: 2 }}>
          <Descriptions.Item label="Thao tác"><Tag color={sessionAction === 'CheckIn' ? 'success' : 'blue'}>{actionLabel(sessionAction)}</Tag></Descriptions.Item>
          <Descriptions.Item label="Mã QR (token) còn hiệu lực"><strong aria-live="polite">{formatCountdown(tokenSeconds)}</strong></Descriptions.Item>
          <Descriptions.Item label="Phiên máy chủ còn hiệu lực"><strong aria-live="polite">{formatCountdown(sessionSeconds)}</strong></Descriptions.Item>
          <Descriptions.Item label="Ngày">{formatWorkDate(workDate)}</Descriptions.Item>
          <Descriptions.Item label="Ca">{selectedGroup.shift.shiftName || 'Ca làm việc'} · {formatTime(selectedGroup.shift.startTime)} - {formatTime(selectedGroup.shift.endTime)}</Descriptions.Item>
        </Descriptions>
        {qrSession.canDisplay
          ? <div className="admin-management-state"><QrDisplay payload={qrSession.state.session.qrPayload} size={220} includeMargin /></div>
          : <div className="admin-management-state"><Spin tip="Đang làm mới mã QR" /></div>}
        <div className="admin-action-group"><Button className="admin-action-button admin-action-button--danger" danger onClick={() => { void qrSession.stop() }} loading={qrSession.state.phase === 'stopping'}>Dừng phiên QR</Button></div>
      </section>}
    </section>
  </div>
}
