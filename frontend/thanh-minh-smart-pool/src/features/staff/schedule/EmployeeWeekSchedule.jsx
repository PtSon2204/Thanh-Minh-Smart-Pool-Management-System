import { PlusOutlined, UserOutlined } from '@ant-design/icons'
import { Avatar, Button, Grid, Table, Tag } from 'antd'
import dayjs from 'dayjs'
import { assignmentKey, formatDate, formatTime, isOvernight, scheduledHours, statusColor, statusLabel, vietnamToday, weekDates } from './scheduleUtils'

const dayNames = ['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ Nhật']
const hoursFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 })

function EmployeeIdentity({ name }) {
  return <div className="staff-employee-identity"><Avatar size="small" icon={!name ? <UserOutlined /> : undefined}>{name.trim().split(/\s+/).slice(-2).map((part) => part[0]).join('').toLocaleUpperCase('vi')}</Avatar><strong>{name}</strong></div>
}

function DateLabel({ workDate, index }) {
  return <div className="staff-employee-date"><strong>{dayNames[index]}</strong><span>{dayjs(workDate).format('DD/MM')}</span></div>
}

function ShiftPill({ assignment, onRemove, actionsDisabled, draftsLocked }) {
  const pending = Boolean(assignment.localId)
  return <div className="staff-employee-assignment">
    <Tag className="staff-employee-shift" color={pending ? 'cyan' : statusColor(assignment.status)}>
      <strong>{assignment.shiftName}</strong>
      <span>{formatTime(assignment.startTime)} - {formatTime(assignment.endTime)}{isOvernight(assignment) ? ' · ngày sau' : ''}</span>
      <span>{pending ? 'Chưa lưu' : statusLabel(assignment.status)}</span>
    </Tag>
    {pending && <div className="staff-employee-shift-actions">
      <Button type="text" danger size="small" disabled={actionsDisabled || draftsLocked} aria-label={`Bỏ phân công ${assignment.employeeName}, ${assignment.shiftName}, ${formatDate(assignment.workDate)}`} onClick={() => onRemove(assignment.localId)}>Bỏ</Button>
    </div>}
  </div>
}

export default function EmployeeWeekSchedule({ monday, employees, assignments, drafts, search, onAdd, onRemove, actionsDisabled, draftsLocked, canAssign }) {
  const screens = Grid.useBreakpoint()
  const dates = weekDates(monday)
  const eligibleIds = new Set(employees.map((employee) => employee.employeeId))
  const employeeMap = new Map(employees.map((employee) => [employee.employeeId, { employeeId: employee.employeeId, name: employee.name }]))
  const items = [...assignments, ...drafts]
  items.forEach((item) => {
    if (!employeeMap.has(item.employeeId)) employeeMap.set(item.employeeId, { employeeId: item.employeeId, name: item.employeeName || 'Nhân viên' })
  })
  const normalizedSearch = search.trim().toLocaleLowerCase('vi')
  const rows = [...employeeMap.values()]
    .filter((employee) => employee.name.toLocaleLowerCase('vi').includes(normalizedSearch))
    .sort((first, second) => first.name.localeCompare(second.name, 'vi'))
  const byEmployeeDate = new Map()
  const byEmployee = new Map()
  items.forEach((item) => {
    const key = `${item.employeeId}:${item.workDate}`
    byEmployeeDate.set(key, [...(byEmployeeDate.get(key) || []), item])
    byEmployee.set(item.employeeId, [...(byEmployee.get(item.employeeId) || []), item])
  })

  const renderHours = (employee) => <strong className="staff-employee-hours">{hoursFormat.format(scheduledHours(byEmployee.get(employee.employeeId) || []))} h</strong>
  const renderEmployee = (employee) => <div className="staff-employee-summary"><EmployeeIdentity name={employee.name} /><span className="staff-employee-weekly-hours">Giờ phân công tuần: {renderHours(employee)}</span></div>
  const renderDay = (employee, workDate) => {
    const dayItems = [...(byEmployeeDate.get(`${employee.employeeId}:${workDate}`) || [])].sort((first, second) => (first.startTime || '').localeCompare(second.startTime || ''))
    const canAdd = canAssign && eligibleIds.has(employee.employeeId) && workDate > vietnamToday()
    return <div className="staff-employee-day">
      {dayItems.map((assignment) => <ShiftPill key={assignment.localId || assignment.id || assignmentKey(assignment)} assignment={assignment} onRemove={onRemove} actionsDisabled={actionsDisabled} draftsLocked={draftsLocked} />)}
      {canAdd ? <Button className="staff-employee-add" type="text" size="small" icon={<PlusOutlined />} disabled={actionsDisabled || draftsLocked} aria-label={`Phân ca cho ${employee.name}, ${formatDate(workDate)}`} onClick={() => onAdd({ employeeId: employee.employeeId, workDate })}>{dayItems.length ? 'Thêm ca' : 'Phân ca'}</Button> : !dayItems.length && <span className="staff-employee-empty">-</span>}
    </div>
  }
  const emptyText = normalizedSearch ? 'Không có nhân viên phù hợp.' : 'Không có nhân viên hoặc phân công trong tuần.'

  if (!screens.md) return <section className="staff-employee-agenda" aria-label="Lịch tuần theo nhân viên, từ Thứ Hai đến Chủ Nhật">
    {dates.map((workDate, index) => {
      const dayEmployees = rows.filter((employee) => eligibleIds.has(employee.employeeId) || byEmployeeDate.has(`${employee.employeeId}:${workDate}`))
      return <section className="staff-employee-agenda-day" key={workDate} aria-label={`${dayNames[index]}, ${formatDate(workDate)}`}>
        <header><DateLabel workDate={workDate} index={index} /></header>
        {dayEmployees.length ? dayEmployees.map((employee) => <article className="staff-employee-agenda-card" key={employee.employeeId} aria-label={employee.name}>
          {renderEmployee(employee)}
          {renderDay(employee, workDate)}
        </article>) : <p className="staff-employee-agenda-empty">{emptyText}</p>}
      </section>
    })}
  </section>

  const tablet = !screens.lg
  const columns = tablet ? [
    { title: 'Ngày', key: 'date', fixed: 'left', width: 152, rowScope: 'row', render: (_, day) => <DateLabel workDate={day.workDate} index={day.index} /> },
    ...rows.map((employee) => ({ key: employee.employeeId, width: 192, title: renderEmployee(employee), render: (_, day) => renderDay(employee, day.workDate) })),
  ] : [
    { title: 'Nhân viên', dataIndex: 'name', fixed: 'left', width: 192, render: (name) => <EmployeeIdentity name={name} /> },
    ...dates.map((workDate, index) => ({
      key: workDate,
      width: 152,
      title: <DateLabel workDate={workDate} index={index} />,
      render: (_, employee) => renderDay(employee, workDate),
    })),
    { title: 'Giờ phân công', key: 'hours', width: 112, align: 'right', render: (_, employee) => renderHours(employee) },
  ]

  return <div className="staff-employee-week" tabIndex={0} role="region" aria-label={tablet ? 'Lịch tuần theo ngày, cuộn ngang để xem các nhân viên' : 'Lịch tuần theo nhân viên, cuộn ngang để xem đủ bảy ngày'}>
    {tablet && !rows.length && <p className="staff-employee-agenda-empty">{emptyText}</p>}
    <Table rowKey={tablet ? 'workDate' : 'employeeId'} columns={columns} dataSource={tablet ? dates.map((workDate, index) => ({ workDate, index })) : rows} pagination={false} scroll={{ x: tablet ? 152 + rows.length * 192 : 1368 }} locale={{ emptyText }} />
  </div>
}
