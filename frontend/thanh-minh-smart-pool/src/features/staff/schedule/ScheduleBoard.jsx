import { Button, Collapse, Empty, Tag } from 'antd'
import { CalendarOutlined, PlusOutlined } from '@ant-design/icons'
import { formatDate, formatTime, isOvernight, statusColor, statusLabel, weekDates } from './scheduleUtils'

function AssignmentTag({ assignment, pending, onRemove, isAdmin, onCancel, cancelPending, actionsDisabled }) {
  const label = pending ? assignment.employeeName : assignment.employeeName || 'Nhân viên'
  return <div className="staff-week-assignment">
    <div className="staff-week-assignment-identity">
      <strong>{label}</strong>
      <Tag color={pending ? 'cyan' : statusColor(assignment.status)}>{pending ? 'Chưa lưu' : statusLabel(assignment.status)}</Tag>
    </div>
    {pending && <Button type="text" size="small" aria-label={`Bỏ phân công ${label}`} onClick={() => onRemove(assignment.localId)}>Bỏ</Button>}
    {!pending && isAdmin && assignment.status === 'Scheduled' && <Button type="text" size="small" danger aria-label={`Hủy phân công ${label}`} onClick={() => onCancel(assignment)} loading={cancelPending} disabled={actionsDisabled}>Hủy</Button>}
  </div>
}

function ShiftCard({ shift, assignments, drafts, isAdmin, onAdd, onAttendance, onRemove, onCancel, cancelId, actionsDisabled, activeShiftIds, activeShiftsStatus }) {
  const isActive = activeShiftsStatus === 'ready' && activeShiftIds.has(shift.shiftId)
  const isInactive = activeShiftsStatus === 'ready' && !isActive
  const addDisabled = actionsDisabled || !isActive
  const addExplanation = isInactive
    ? 'Ca đã ngừng hoạt động. Lịch đã phân vẫn được giữ; không thể thêm nhân viên.'
    : activeShiftsStatus === 'loading'
      ? 'Đang tải trạng thái ca. Không thể thêm nhân viên.'
      : activeShiftsStatus === 'unavailable'
        ? 'Không thể xác minh trạng thái ca. Không thể thêm nhân viên.'
        : null

  return <article className="staff-week-shift-card">
    <header className="staff-week-shift-heading">
      <strong>{shift.shiftName || shift.name}{isInactive && <Tag>Không hoạt động</Tag>}</strong>
      <span>{formatTime(shift.startTime)} - {formatTime(shift.endTime)}{isOvernight(shift) ? ' · ngày sau' : ''}</span>
      {isAdmin && assignments.length > 0 && <div className="admin-action-group staff-week-attendance-actions"><Button className="admin-action-button admin-action-button--primary staff-week-attendance" icon={<CalendarOutlined />} onClick={() => onAttendance(assignments[0])}>Chấm công</Button></div>}
    </header>
    {isAdmin && addExplanation && <span className="staff-schedule-overnight">{addExplanation}</span>}
    <div className="staff-week-assignment-list">
      {assignments.map((assignment) => <AssignmentTag assignment={assignment} isAdmin={isAdmin} onCancel={onCancel} cancelPending={cancelId === assignment.id} actionsDisabled={actionsDisabled} key={assignment.id} />)}
      {drafts.map((assignment) => <AssignmentTag assignment={assignment} pending onRemove={onRemove} key={assignment.localId} />)}
    </div>
    {isAdmin && <Button className="staff-week-add" type="link" icon={<PlusOutlined />} onClick={() => isActive && !actionsDisabled && onAdd(shift)} disabled={addDisabled}>Thêm nhân viên</Button>}
  </article>
}

export default function ScheduleBoard({ monday, assignments, drafts, isAdmin, employees, onAdd, onAttendance, onRemove, onCancel, cancelId, actionsDisabled, activeShiftIds, activeShiftsStatus }) {
  const dates = weekDates(monday)
  const assignedCount = (employeeId) => assignments.filter((item) => item.employeeId === employeeId && item.status !== 'Cancelled').length
  return <>
    <section className="staff-week-board" aria-label="Lịch tuần">
      {dates.map((workDate) => {
        const saved = assignments.filter((item) => item.workDate === workDate)
        const pending = drafts.filter((item) => item.workDate === workDate)
        const groups = new Map()
        ;[...saved, ...pending].forEach((item) => {
          const key = item.shiftId
          const current = groups.get(key) || { shift: item, assignments: [], drafts: [] }
          current[item.localId ? 'drafts' : 'assignments'].push(item)
          groups.set(key, current)
        })
        return <section className="staff-week-day" key={workDate}>
          <header><strong>{dayjsLabel(workDate)}</strong><span>{formatDate(workDate)}</span></header>
          {groups.size === 0 ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có ca được phân" /> : [...groups.values()].map((group) => <ShiftCard {...group} isAdmin={isAdmin} key={group.shift.id} onAdd={(shift) => onAdd({ ...shift, workDate })} onAttendance={onAttendance} onRemove={onRemove} onCancel={onCancel} cancelId={cancelId} actionsDisabled={actionsDisabled} activeShiftIds={activeShiftIds} activeShiftsStatus={activeShiftsStatus} />)}
        </section>
      })}
    </section>
    {isAdmin && <Collapse className="staff-schedule-roster" items={[{ key: 'roster', label: `Nhân viên đủ điều kiện (${employees.length})`, children: employees.length ? <div className="staff-roster-list">{employees.map((employee) => <div key={employee.employeeId}><strong>{employee.name}</strong><span>{employee.employmentStatus}</span><span>{assignedCount(employee.employeeId)} ca trong tuần</span></div>)}</div> : <Empty description="Không có nhân viên đủ điều kiện." /> }]} />}
  </>
}

function dayjsLabel(date) {
  return ['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ Nhật'][(new Date(`${date}T00:00:00`).getDay() + 6) % 7]
}
