import { Button, Collapse, Empty, Tag } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { formatDate, formatTime, isOvernight, statusColor, statusLabel, weekDates } from './scheduleUtils'

function AssignmentTag({ assignment, pending, onRemove }) {
  const label = pending ? assignment.employeeName : assignment.employeeName || 'Nhân viên'
  return <div className="staff-week-assignment">
    <div className="staff-week-assignment-identity">
      <strong>{label}</strong>
      <Tag color={pending ? 'cyan' : statusColor(assignment.status)}>{pending ? 'Chưa lưu' : statusLabel(assignment.status)}</Tag>
    </div>
    {pending && <Button type="text" size="small" aria-label={`Bỏ phân công ${label}`} onClick={() => onRemove(assignment.localId)}>Bỏ</Button>}
  </div>
}

function ShiftCard({ shift, assignments, drafts, isAdmin, onAdd, onRemove }) {
  return <article className="staff-week-shift-card">
    <header className="staff-week-shift-heading">
      <strong>{shift.shiftName || shift.name}</strong>
      <span>{formatTime(shift.startTime)} - {formatTime(shift.endTime)}{isOvernight(shift) ? ' · ngày sau' : ''}</span>
    </header>
    <div className="staff-week-assignment-list">
      {assignments.map((assignment) => <AssignmentTag assignment={assignment} key={assignment.id} />)}
      {drafts.map((assignment) => <AssignmentTag assignment={assignment} pending onRemove={onRemove} key={assignment.localId} />)}
    </div>
    {isAdmin && <Button className="staff-week-add" type="link" icon={<PlusOutlined />} onClick={() => onAdd(shift)}>Thêm nhân viên</Button>}
  </article>
}

export default function ScheduleBoard({ monday, assignments, drafts, isAdmin, employees, onAdd, onRemove }) {
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
          {groups.size === 0 ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có ca được phân" /> : [...groups.values()].map((group) => <ShiftCard {...group} isAdmin={isAdmin} key={group.shift.id} onAdd={(shift) => onAdd({ ...shift, workDate })} onRemove={onRemove} />)}
        </section>
      })}
    </section>
    {isAdmin && <Collapse className="staff-schedule-roster" items={[{ key: 'roster', label: `Nhân viên đủ điều kiện (${employees.length})`, children: employees.length ? <div className="staff-roster-list">{employees.map((employee) => <div key={employee.employeeId}><strong>{employee.name}</strong><span>{employee.employmentStatus}</span><span>{assignedCount(employee.employeeId)} ca trong tuần</span></div>)}</div> : <Empty description="Không có nhân viên đủ điều kiện." /> }]} />}
  </>
}

function dayjsLabel(date) {
  return ['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ Nhật'][(new Date(`${date}T00:00:00`).getDay() + 6) % 7]
}
