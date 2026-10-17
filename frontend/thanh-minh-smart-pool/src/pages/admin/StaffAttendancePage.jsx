import { useState } from 'react'
import { ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Empty, message, Modal, Spin } from 'antd'
import { useSearchParams } from 'react-router-dom'
import { useAuthStore } from '../../features/auth/store/authStore'
import StaffAttendanceTable from '../../features/staff/components/StaffAttendanceTable'
import { useAttendanceSchedule, useCheckIn, useCheckOut } from '../../features/staff/hooks/useStaffs'
import { formatTime, vietnamToday } from '../../features/staff/schedule/scheduleUtils'
import { ServiceListHeader } from '../../features/services/components/ServiceListChrome'
import '../../features/services/styles/serviceManagement.css'
import '../../features/shared/styles/adminActions.css'
import '../../features/staff/styles/staffSchedule.css'

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/
const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const vietnamDate = new Intl.DateTimeFormat('vi-VN', { dateStyle: 'full', timeZone: 'Asia/Ho_Chi_Minh' })

function isValidDate(value) {
  if (!DATE_PATTERN.test(value)) return false
  const parsed = new Date(`${value}T00:00:00.000Z`)
  return !Number.isNaN(parsed.getTime()) && parsed.toISOString().slice(0, 10) === value
}

function selectionFrom(searchParams) {
  const dates = searchParams.getAll('date')
  const shiftIds = searchParams.getAll('shiftId')
  const date = dates.length === 0 ? vietnamToday() : dates[0]
  const shiftId = shiftIds.length === 0 ? undefined : shiftIds[0]

  if (dates.length > 1 || !isValidDate(date)) return { valid: false, error: 'Tham số date phải có dạng YYYY-MM-DD hợp lệ.' }
  if (shiftIds.length > 1 || (shiftIds.length > 0 && (!shiftId || !GUID_PATTERN.test(shiftId)))) return { valid: false, error: 'Tham số shiftId phải là GUID hợp lệ.' }
  return { valid: true, date, shiftId }
}

function groupByShift(assignments) {
  const groups = new Map()
  assignments.forEach((assignment) => {
    const current = groups.get(assignment.shiftId)
    if (current) {
      current.assignments.push(assignment)
      return
    }
    groups.set(assignment.shiftId, { shift: assignment, assignments: [assignment] })
  })
  return [...groups.values()]
}

function formatWorkDate(value) {
  return vietnamDate.format(new Date(`${value}T12:00:00+07:00`))
}

function errorMessage(error) {
  const status = error?.response?.status
  if (status === 409) return 'Lịch chấm công đã thay đổi. Dữ liệu đã được làm mới, không gửi lại thao tác.'
  if (!status) return 'Không xác định được kết quả thao tác. Dữ liệu đã được làm mới, không gửi lại thao tác.'
  if (status === 403) return 'Bạn không có quyền chấm công thủ công cho ca này.'
  if (status === 404) return 'Không tìm thấy phân công. Hãy làm mới dữ liệu trước khi thao tác lại.'
  return 'Máy chủ không thể ghi nhận thao tác. Không gửi lại tự động.'
}

export default function StaffAttendancePage() {
  const session = useAuthStore((state) => state.session)
  const isAdmin = session?.role?.toUpperCase() === 'ADMIN'
  const [searchParams, setSearchParams] = useSearchParams()
  const selection = selectionFrom(searchParams)
  const attendance = useAttendanceSchedule(session?.id, selection.date, Boolean(isAdmin && selection.valid))
  const checkIn = useCheckIn()
  const checkOut = useCheckOut()
  const [pendingId, setPendingId] = useState(null)
  const [errorContext, setErrorContext] = useState(null)
  const [refreshLocked, setRefreshLocked] = useState(false)
  const groups = groupByShift(attendance.data?.items || [])
  const selectedGroup = selection.shiftId ? groups.find((group) => group.shift.shiftId === selection.shiftId) : undefined
  const visibleGroups = selection.shiftId ? (selectedGroup ? [selectedGroup] : []) : groups
  const actionsDisabled = Boolean(pendingId) || refreshLocked
  const openedFromShortcut = searchParams.has('date') || Boolean(selection.shiftId)

  const refresh = async () => {
    if (!searchParams.has('date') && selection.date !== vietnamToday()) {
      setSearchParams({})
      return
    }
    const result = await attendance.refetch()
    if (result.error) {
      message.error('Không thể làm mới dữ liệu chấm công.')
      return
    }
    setRefreshLocked(false)
    message.success('Đã làm mới dữ liệu chấm công.')
  }

  const submitAction = async (assignment, action) => {
    if (actionsDisabled) return

    setPendingId(assignment.id)
    try {
      await (action === 'check-in' ? checkIn : checkOut).mutateAsync(assignment.id)
      setErrorContext(null)
      message.success(action === 'check-in' ? 'Đã ghi nhận vào ca.' : 'Đã ghi nhận ra ca.')
    } catch (error) {
      const isNetworkFailure = !error?.response
      let refreshFailed = false
      if (error?.response?.status === 409 || isNetworkFailure) {
        const result = await attendance.refetch()
        refreshFailed = Boolean(result.error)
      }
      if (refreshFailed) setRefreshLocked(true)

      const detail = refreshFailed
        ? 'Không thể xác minh trạng thái chấm công mới. Các thao tác bị khóa đến khi làm mới thành công.'
        : errorMessage(error)
      setErrorContext({ assignment, action, detail })
      message.error(detail)
    } finally {
      setPendingId(null)
    }
  }

  const confirmAction = (assignment, action) => {
    if (actionsDisabled) return
    const actionLabel = action === 'check-in' ? 'Vào ca' : 'Ra ca'
    Modal.confirm({
      title: `Xác nhận ${actionLabel.toLowerCase()}?`,
      content: `Nhân viên: ${assignment.employeeName || 'Nhân viên'} · Ca: ${assignment.shiftName || 'Ca làm việc'} · Ngày: ${formatWorkDate(assignment.workDate)}`,
      okText: actionLabel,
      cancelText: 'Hủy',
      onOk: () => submitAction(assignment, action),
    })
  }

  if (!isAdmin) return <div className="service-list-page staff-schedule-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ quản trị viên có thể chấm công thủ công." /></div>

  if (!selection.valid) return <div className="service-list-page staff-schedule-page">
    <ServiceListHeader title="Chấm công thủ công" />
    <Alert type="error" showIcon title="Liên kết chấm công không hợp lệ" description={selection.error} action={<Button onClick={() => setSearchParams({})}>Về hôm nay</Button>} />
  </div>

  return <div className="service-list-page staff-schedule-page">
    <ServiceListHeader
      title={<>Chấm công thủ công <span className="staff-schedule-count">{attendance.isLoading ? 'Đang tải…' : `${attendance.data?.totalCount ?? 0} phân công`}</span></>}
      action={<div className="admin-action-group">
        {openedFromShortcut && <Button disabled={Boolean(pendingId)} onClick={() => { setSearchParams({}); setErrorContext(null) }}>Về hôm nay</Button>}
        <Button className="admin-action-button admin-action-button--primary" icon={<ReloadOutlined />} loading={attendance.isFetching} disabled={Boolean(pendingId)} onClick={refresh}>Làm mới</Button>
      </div>}
    />
    <section className="service-list-table staff-schedule-table" aria-label="Danh sách chấm công">
      {errorContext && <Alert type="error" showIcon message={`Không thể ${errorContext.action === 'check-in' ? 'vào ca' : 'ra ca'} cho ${errorContext.assignment.employeeName || 'nhân viên'}`} description={`${errorContext.detail} Ca: ${errorContext.assignment.shiftName || 'Ca làm việc'} · Ngày: ${formatWorkDate(errorContext.assignment.workDate)}`} />}
      {refreshLocked && <Alert type="warning" showIcon message="Thao tác chấm công đang bị khóa" description="Hãy làm mới thành công trước khi thao tác lại." />}
      {attendance.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
      {attendance.isError && <Empty description="Không thể tải phân công trong ngày."><Button icon={<ReloadOutlined />} onClick={refresh}>Thử lại</Button></Empty>}
      {!attendance.isLoading && !attendance.isError && visibleGroups.length === 0 && <Empty description={selection.shiftId ? 'Không có phân công cho ca đã chọn trong ngày này.' : 'Chưa có phân công trong ngày này.'} />}
      {!attendance.isLoading && !attendance.isError && visibleGroups.map((group) => <section key={group.shift.shiftId} className="staff-week-shift-card">
        <header className="staff-week-shift-heading"><strong>{group.shift.shiftName || 'Ca làm việc'}</strong><span>{formatTime(group.shift.startTime)} - {formatTime(group.shift.endTime)}</span></header>
        <StaffAttendanceTable assignments={group.assignments} actionsDisabled={actionsDisabled} pendingId={pendingId} onAction={confirmAction} />
      </section>)}
    </section>
  </div>
}
