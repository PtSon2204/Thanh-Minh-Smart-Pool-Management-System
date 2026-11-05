import { DeleteOutlined, LeftOutlined, PlusOutlined, RightOutlined } from '@ant-design/icons'
import { Alert, Button, Form, Modal, Select } from 'antd'
import dayjs from 'dayjs'
import { assignmentDateError, conflictFor, expandAssignmentRows, formatDate, formatTime, isOvernight, vietnamToday, weekDates } from './scheduleUtils'

const dayNames = ['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ Nhật']
const repeatedShiftMessage = 'Ca đã có trong ngày này. Thêm nhân viên vào dòng đã chọn.'

function shiftSelectedInOtherRow(shiftId, workDate, rows, index) {
  return Boolean(shiftId && workDate && rows.some((row, rowIndex) => rowIndex !== index && row?.shiftId === shiftId && row?.workDate?.format('YYYY-MM-DD') === workDate.format('YYYY-MM-DD')))
}

function rowConflicts(row, index, rows, assignments, drafts, shifts, employees) {
  if (!row?.shiftId || !row?.workDate) return []
  const otherRows = expandAssignmentRows(rows.filter((_, rowIndex) => rowIndex !== index))
  return [...(shiftSelectedInOtherRow(row.shiftId, row.workDate, rows, index) ? [repeatedShiftMessage] : []), ...(row?.employeeIds || []).flatMap((employeeId) => {
    const employee = employees.find((item) => item.employeeId === employeeId)
    const candidate = { employeeId, shiftId: row.shiftId, workDate: row.workDate?.format('YYYY-MM-DD') }
    const error = !employee ? 'Nhân viên không còn đủ điều kiện.' : conflictFor(candidate, [...assignments, ...drafts, ...otherRows], shifts)
    return error ? [`${employee?.name || 'Nhân viên'}: ${error}`] : []
  })]
}

function AssignmentRow({ field, index, rows, monday, employees, shifts, assignments, drafts, remove }) {
  const row = rows[index]
  const otherRows = expandAssignmentRows(rows.filter((_, rowIndex) => rowIndex !== index))
  const conflicts = rowConflicts(row, index, rows, assignments, drafts, shifts, employees)
  const shiftOptions = shifts.map((shift) => {
    const selectedElsewhere = shiftSelectedInOtherRow(shift.id, row?.workDate, rows, index)
    const conflicting = Boolean(row?.workDate && row?.employeeIds?.some((employeeId) => conflictFor({ employeeId, shiftId: shift.id, workDate: row.workDate.format('YYYY-MM-DD') }, [...assignments, ...drafts, ...otherRows], shifts)))
    return {
      value: shift.id,
      label: `${shift.name} · ${formatTime(shift.startTime)} - ${formatTime(shift.endTime)}${isOvernight(shift) ? ' · ngày sau' : ''}${selectedElsewhere ? ` · ${repeatedShiftMessage}` : conflicting ? ' · Đã phân công / trùng giờ' : ''}`,
      disabled: shift.id !== row?.shiftId && (selectedElsewhere || conflicting),
    }
  })
  const dateError = assignmentDateError(row?.workDate?.format('YYYY-MM-DD'), monday)
  const errors = [...(dateError ? [dateError] : []), ...conflicts]
  const employeeOptions = employees.map((employee) => ({
    value: employee.employeeId,
    label: employee.name,
    disabled: Boolean(!row?.employeeIds?.includes(employee.employeeId) && row?.shiftId && row?.workDate && conflictFor({ employeeId: employee.employeeId, shiftId: row.shiftId, workDate: row.workDate.format('YYYY-MM-DD') }, [...assignments, ...drafts, ...otherRows], shifts)),
  }))

  return <section className="staff-assignment-row" aria-label={`Phân công ${index + 1}`}>
    <Form.Item label="Ca làm" name={[field.name, 'shiftId']} rules={[
      { required: true, message: 'Chọn ca làm.' },
      { validator: (_, value) => value && !shifts.some((shift) => shift.id === value) ? Promise.reject(new Error('Ca không còn hoạt động. Chọn ca khác.')) : Promise.resolve() },
    ]}>
      <Select allowClear showSearch optionFilterProp="label" placeholder="Chọn ca" options={shiftOptions} />
    </Form.Item>
    <Form.Item label="Nhân viên" name={[field.name, 'employeeIds']} rules={[
      { required: true, type: 'array', min: 1, message: 'Chọn ít nhất một nhân viên.' },
    ]}>
      <Select mode="multiple" showSearch optionFilterProp="label" placeholder="Chọn nhân viên" options={employeeOptions} />
    </Form.Item>
    <Button className="staff-assignment-remove" type="text" danger icon={<DeleteOutlined />} aria-label={`Xóa dòng phân công ${index + 1}`} onClick={() => remove(field.name)} />
    {errors.length > 0 && <Alert className="staff-assignment-conflicts" type="warning" showIcon title="Phân công bị trùng hoặc không hợp lệ" description={<ul className="staff-schedule-error-list">{errors.map((error) => <li key={error}>{error}</li>)}</ul>} />}
  </section>
}

export default function AssignmentModal({ open, context, monday, employees, shifts, assignments, drafts, loading, onWeekChange, onClose, onConfirm }) {
  const [form] = Form.useForm()
  const initialDate = context?.workDate && !assignmentDateError(context.workDate, monday)
    ? context.workDate
    : weekDates(monday).find((date) => !assignmentDateError(date, monday))
  const initialRows = initialDate ? [{ employeeIds: context?.employeeId ? [context.employeeId] : [], shiftId: context?.shiftId, workDate: dayjs(initialDate) }] : []
  const rows = Form.useWatch('rows', { form, preserve: true }) || initialRows
  const unavailable = loading || !shifts.length || !employees.length

  const changeWeek = (offset) => {
    const apply = () => onWeekChange(dayjs(monday).add(offset, 'week').format('YYYY-MM-DD'))
    const entered = (form.getFieldValue('rows') || []).some((row) => row?.shiftId || row?.employeeIds?.length)
    if (!entered) return apply()
    Modal.confirm({ title: 'Bỏ nội dung phân ca đang nhập?', content: 'Các ca đang nhập trong hộp thoại sẽ bị xóa khi đổi tuần.', okText: 'Bỏ nội dung và đổi tuần', cancelText: 'Ở lại', autoFocusButton: 'cancel', onOk: apply })
  }

  const confirm = () => {
    const values = form.getFieldValue('rows') || []
    if (unavailable || !values.length) return
    const invalidIndex = values.findIndex((row, index) => {
      const dateError = assignmentDateError(row.workDate?.format('YYYY-MM-DD'), monday)
      const conflicts = rowConflicts(row, index, values, assignments, drafts, shifts, employees)
      return dateError || conflicts.length
    })
    if (invalidIndex !== -1) return form.scrollToField(['rows', invalidIndex, 'shiftId'], { block: 'center', focus: true })
    onConfirm(expandAssignmentRows(values))
  }

  return <Modal className="staff-assignment-modal" title="Phân ca" open={open} onCancel={onClose} onOk={() => form.submit()} okText="Thêm vào bản nháp" cancelText="Đóng" okButtonProps={{ disabled: unavailable || !rows.length }} afterOpenChange={(visible) => { if (visible && initialRows.length) form.scrollToField(['rows', 0, 'shiftId'], { block: 'center', focus: true }) }} destroyOnHidden>
    <div className="staff-assignment-toolbar">
      <Button aria-label="Tuần trước" icon={<LeftOutlined />} onClick={() => changeWeek(-1)} />
      <span aria-live="polite">Tuần {dayjs(monday).format('DD/MM')} - {dayjs(monday).add(6, 'day').format('DD/MM/YYYY')}</span>
      <Button aria-label="Tuần sau" icon={<RightOutlined />} onClick={() => changeWeek(1)} />
    </div>
    {loading && <Alert className="staff-assignment-empty" type="info" showIcon title="Đang tải dữ liệu phân ca của tuần…" />}
    {!shifts.length && <Alert className="staff-assignment-empty" type="info" showIcon title="Chưa có ca đang hoạt động" description="Cần có ca đang hoạt động trước khi phân công." />}
    {!employees.length && <Alert className="staff-assignment-empty" type="info" showIcon title="Không có nhân viên đủ điều kiện phân ca." />}
    <Form form={form} layout="vertical" initialValues={{ rows: initialRows }} onFinish={confirm} autoComplete="off" disabled={unavailable}>
      <Form.List name="rows">
        {(fields, { add, remove }) => <div className="staff-assignment-days">
          {weekDates(monday).map((workDate, dayIndex) => {
            const blocked = Boolean(assignmentDateError(workDate, monday))
            const dayFields = fields.filter((field) => rows[field.name]?.workDate?.format('YYYY-MM-DD') === workDate)
            return <section key={workDate} className={`staff-assignment-day${blocked ? ' staff-assignment-day--disabled' : ''}`} aria-label={`${dayNames[dayIndex]}, ${formatDate(workDate)}`}>
              <header><h3>{dayNames[dayIndex]} <span>{formatDate(workDate)}</span></h3><Button type="dashed" size="small" icon={<PlusOutlined />} disabled={blocked || unavailable} aria-label={`Thêm ca ${dayNames[dayIndex]}, ${formatDate(workDate)}`} onClick={() => add({ employeeIds: [], workDate: dayjs(workDate) })}>Thêm ca</Button></header>
              {dayFields.map((field) => <AssignmentRow key={field.key} field={field} index={field.name} rows={rows} monday={monday} employees={employees} shifts={shifts} assignments={assignments} drafts={drafts} remove={remove} />)}
              {!dayFields.length && <p className="staff-assignment-day-note">{blocked ? workDate === vietnamToday() ? 'Hôm nay · Không thể phân ca.' : 'Ngày đã qua · Không thể phân ca.' : 'Chưa thêm ca.'}</p>}
            </section>
          })}
        </div>}
      </Form.List>
    </Form>
  </Modal>
}
