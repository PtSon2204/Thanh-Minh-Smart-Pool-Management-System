import { useEffect, useState } from 'react'
import { LeftOutlined, ReloadOutlined, RightOutlined } from '@ant-design/icons'
import { Alert, Button, DatePicker, Empty, Modal, Pagination, Segmented, Spin, Table, Tag, message } from 'antd'
import dayjs from 'dayjs'
import { useBlocker, useBeforeUnload } from 'react-router-dom'
import { useAuthStore } from '../../features/auth/store/authStore'
import { useAssignShiftBatch, useCancelSchedule, useScheduleEmployees, useStaffSchedulePage } from '../../features/staff/hooks/useStaffs'
import { useActiveShifts } from '../../features/staff/hooks/useShifts'
import AssignmentModal from '../../features/staff/schedule/AssignmentModal'
import ScheduleBoard from '../../features/staff/schedule/ScheduleBoard'
import { assignmentKey, formatDate, formatTime, mondayFor, statusColor, statusLabel, vietnamToday, weekDates } from '../../features/staff/schedule/scheduleUtils'
import { ServiceListHeader } from '../../features/services/components/ServiceListChrome'
import '../../features/services/styles/serviceManagement.css'
import '../../features/shared/styles/adminActions.css'
import '../../features/staff/styles/staffSchedule.css'

function ScheduleList({ items, pageIndex, pageSize, onPageChange, total, isAdmin, onCancel, cancelId, actionsDisabled }) {
  const columns = [
    ...(isAdmin ? [{ title: 'Nhân viên', dataIndex: 'employeeName', render: (value) => value || '-' }] : []),
    { title: 'Ngày làm', dataIndex: 'workDate', render: formatDate },
    { title: 'Ca', dataIndex: 'shiftName', render: (value) => value || '-' },
    { title: 'Bắt đầu', dataIndex: 'startTime', render: formatTime },
    { title: 'Kết thúc', dataIndex: 'endTime', render: formatTime },
    { title: 'Trạng thái', dataIndex: 'status', render: (value) => <Tag color={statusColor(value)}>{statusLabel(value)}</Tag> },
    ...(isAdmin ? [{ title: 'Thao tác', key: 'actions', render: (_, assignment) => assignment.status === 'Scheduled' ? <Button type="link" danger size="small" onClick={() => onCancel(assignment)} loading={cancelId === assignment.id} disabled={actionsDisabled}>Hủy</Button> : null }] : []),
  ]
  const start = (pageIndex - 1) * pageSize
  return <><Table rowKey="id" scroll={{ x: 760 }} columns={columns} dataSource={items.slice(start, start + pageSize)} pagination={false} locale={{ emptyText: 'Chưa có lịch làm việc phù hợp.' }} />
    {total > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={total} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={onPageChange} /></div>}
  </>
}

export default function StaffSchedulePage() {
  const session = useAuthStore((state) => state.session)
  const role = session?.role?.toUpperCase()
  const isAdmin = role === 'ADMIN'
  const canRead = isAdmin || ['LIFEGUARD', 'SALES'].includes(role)
  const [monday, setMonday] = useState(() => mondayFor(vietnamToday()))
  const [view, setView] = useState('Tuần')
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [drafts, setDrafts] = useState([])
  const [modalContext, setModalContext] = useState(null)
  const [saveState, setSaveState] = useState('ready')
  const isSaving = saveState === 'saving'
  const week = weekDates(monday)
  const schedule = useStaffSchedulePage({ scope: isAdmin ? 'all' : 'self', sessionUserId: session?.id, params: { fromDate: dayjs(monday).subtract(1, 'day').format('YYYY-MM-DD'), toDate: dayjs(monday).add(7, 'day').format('YYYY-MM-DD'), pageIndex, pageSize }, enabled: canRead })
  const roster = useScheduleEmployees(isAdmin)
  const shifts = useActiveShifts(isAdmin)
  const batch = useAssignShiftBatch()
  const cancellation = useCancelSchedule()
  const savedItems = schedule.data?.items || []
  const visibleItems = savedItems.filter((item) => week.includes(item.workDate))
  const activeShiftIds = new Set(shifts.data?.items?.map((shift) => shift.id) || [])
  const activeShiftsStatus = shifts.isSuccess ? 'ready' : shifts.isError ? 'unavailable' : 'loading'
  const draftsDirty = drafts.length > 0
  const actionsDisabled = isSaving || cancellation.isPending
  const blocker = useBlocker(() => isSaving || draftsDirty)

  useBeforeUnload((event) => {
    if (isSaving || draftsDirty) event.preventDefault()
  })

  useEffect(() => {
    if (blocker.state !== 'blocked') return
    if (isSaving) {
      message.warning('Đang lưu phân công. Không thể rời trang.')
      blocker.reset()
      return
    }
    Modal.confirm({ title: 'Bỏ thay đổi chưa lưu?', content: 'Các phân công đang chờ sẽ bị xóa.', okText: 'Bỏ thay đổi', cancelText: 'Ở lại', onOk: () => { setDrafts([]); blocker.proceed() }, onCancel: blocker.reset })
  }, [blocker, isSaving])

  const requestChange = (apply) => {
    if (!draftsDirty || isSaving) return apply()
    Modal.confirm({ title: 'Bỏ thay đổi chưa lưu?', content: 'Các phân công đang chờ sẽ bị xóa.', okText: 'Bỏ thay đổi', cancelText: 'Ở lại', onOk: () => { setDrafts([]); apply() } })
  }

  const addDrafts = (rows) => {
    setDrafts((current) => [...current, ...rows.map((row) => ({ ...row, localId: crypto.randomUUID(), employeeName: roster.data?.find((employee) => employee.employeeId === row.employeeId)?.name || 'Nhân viên' }))])
    setModalContext(null)
  }

  const reconcile = async (submitted) => {
    const result = await schedule.refetch()
    if (result.error) return 'unavailable'
    const confirmed = new Set((result.data?.items || []).filter((item) => item.status === 'Scheduled' && !item.checkInTime && !item.checkOutTime).map(assignmentKey))
    const matches = submitted.filter((item) => confirmed.has(assignmentKey(item)))
    if (matches.length === submitted.length) { setDrafts([]); return 'all' }
    if (matches.length === 0) return 'none'
    setDrafts((current) => current.map((draft) => matches.some((item) => assignmentKey(item) === assignmentKey(draft)) ? { ...draft, reconciled: true } : draft))
    return 'partial'
  }

  const save = async () => {
    if (!drafts.length || isSaving || saveState === 'partial' || saveState === 'unavailable') return
    setSaveState('saving')
    const submitted = drafts.map(({ employeeId, shiftId, workDate }) => ({ employeeId, shiftId, workDate }))
    try {
      await batch.mutateAsync(submitted)
      await schedule.refetch()
      setDrafts([])
      setSaveState('ready')
      message.success('Đã lưu phân công')
    } catch (error) {
      if (error?.response) {
        setSaveState('ready')
        message.error(error.response.status === 409 ? 'Phân công bị xung đột. Bản nháp được giữ lại.' : 'Không thể lưu phân công. Bản nháp được giữ lại.')
        return
      }
      const outcome = await reconcile(submitted).catch(() => 'unavailable')
      setSaveState(outcome === 'partial' ? 'partial' : outcome === 'unavailable' ? 'unavailable' : 'ready')
      message.warning(outcome === 'all' ? 'Máy chủ đã lưu phân công.' : outcome === 'partial' ? 'Chỉ một phần phân công đã được lưu. Không thể gửi lại tự động.' : outcome === 'none' ? 'Chưa có phân công nào được lưu. Bạn có thể thử lại.' : 'Không thể xác minh kết quả. Bản nháp được giữ lại và bị khóa để an toàn.')
    }
  }

  const confirmCancellation = (assignment) => {
    if (!isAdmin || assignment.status !== 'Scheduled' || actionsDisabled) return
    Modal.confirm({
      title: 'Hủy phân công đã lưu?',
      content: `Nhân viên: ${assignment.employeeName || 'Nhân viên'} · Ca: ${assignment.shiftName || 'Ca làm việc'} · Ngày làm: ${formatDate(assignment.workDate)}`,
      okText: 'Hủy phân công',
      cancelText: 'Giữ lại',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await cancellation.mutateAsync(assignment.id)
          await schedule.refetch()
          message.success('Đã hủy phân công.')
        } catch (error) {
          if (error?.response?.status === 409) {
            message.error('Không thể hủy vì lịch đã thay đổi. Đang làm mới lịch.')
            await schedule.refetch()
            return
          }
          if (!error?.response) {
            message.warning('Chưa xác định được kết quả hủy. Đang làm mới lịch; không gửi lại tự động.')
            await schedule.refetch()
            return
          }
          message.error(error.response.data?.message || 'Không thể hủy phân công.')
        }
      },
    })
  }

  if (!canRead) return <div className="service-list-page staff-schedule-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ quản trị viên và nhân viên mới có thể xem lịch làm việc." /></div>

  return <div className="service-list-page staff-schedule-page">
    <ServiceListHeader title={<>Lịch làm việc <span className="staff-schedule-count">{schedule.isLoading ? 'Đang tải…' : `${visibleItems.length} lịch trong tuần`}</span></>} action={isAdmin ? <Button type="primary" onClick={() => setModalContext({ workDate: monday })} disabled={isSaving}>Phân ca</Button> : null} />
    <section className="service-list-toolbar staff-schedule-toolbar" aria-label="Điều khiển lịch làm việc"><div className="service-list-filters staff-schedule-filters"><Button aria-label="Tuần trước" icon={<LeftOutlined />} onClick={() => requestChange(() => setMonday(dayjs(monday).subtract(7, 'day').format('YYYY-MM-DD')))} disabled={isSaving} /><label className="service-filter-control"><span>Tuần chứa ngày</span><DatePicker value={dayjs(monday)} format="DD/MM/YYYY" onChange={(value) => value && requestChange(() => setMonday(mondayFor(value.format('YYYY-MM-DD'))))} disabled={isSaving} /></label><Button aria-label="Tuần sau" icon={<RightOutlined />} onClick={() => requestChange(() => setMonday(dayjs(monday).add(7, 'day').format('YYYY-MM-DD')))} disabled={isSaving} /><Segmented value={view} options={['Tuần', 'Danh sách']} onChange={(value) => requestChange(() => setView(value))} disabled={isSaving} /><Button icon={<ReloadOutlined />} onClick={() => { schedule.refetch(); shifts.refetch() }} disabled={isSaving}>Làm mới</Button></div></section>
    {draftsDirty && isAdmin && <section className="staff-draft-controls"><span>{drafts.length} phân công chưa lưu{saveState === 'partial' ? ' · một phần đã tồn tại' : ''}</span><Button onClick={() => requestChange(() => setDrafts([]))} disabled={isSaving}>Hủy thay đổi</Button><Button type="primary" onClick={save} loading={isSaving} disabled={saveState === 'partial' || saveState === 'unavailable'}>Lưu phân ca</Button></section>}
    <section className="service-list-table staff-schedule-table" aria-label="Lịch làm việc">
      {schedule.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
      {schedule.isError && <Empty description="Không thể tải lịch làm việc."><Button onClick={() => schedule.refetch()}>Thử lại</Button></Empty>}
      {!schedule.isLoading && !schedule.isError && view === 'Tuần' && <ScheduleBoard monday={monday} assignments={visibleItems} drafts={drafts} isAdmin={isAdmin} employees={roster.data || []} onAdd={setModalContext} onRemove={(localId) => setDrafts((current) => current.filter((draft) => draft.localId !== localId))} onCancel={confirmCancellation} cancelId={cancellation.isPending ? cancellation.variables : null} actionsDisabled={actionsDisabled} activeShiftIds={activeShiftIds} activeShiftsStatus={activeShiftsStatus} />}
      {!schedule.isLoading && !schedule.isError && view === 'Danh sách' && <ScheduleList items={visibleItems} pageIndex={pageIndex} pageSize={pageSize} total={visibleItems.length} isAdmin={isAdmin} onPageChange={(page, size) => { setPageIndex(page); setPageSize(size) }} onCancel={confirmCancellation} cancelId={cancellation.isPending ? cancellation.variables : null} actionsDisabled={actionsDisabled} />}
    </section>
    {roster.isError && <Alert className="staff-schedule-alert" type="error" showIcon message="Không thể tải danh sách nhân viên đủ điều kiện." />}
    {shifts.isError && <Alert className="staff-schedule-alert" type="error" showIcon message="Không thể tải ca đang hoạt động." />}
    <AssignmentModal open={Boolean(modalContext)} context={modalContext} employees={roster.data || []} shifts={shifts.data?.items || []} assignments={savedItems} drafts={drafts} onClose={() => setModalContext(null)} onConfirm={addDrafts} />
  </div>
}
