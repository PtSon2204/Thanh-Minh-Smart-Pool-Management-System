import { useEffect } from 'react'
import { Alert, DatePicker, Form, Modal, Select } from 'antd'
import dayjs from 'dayjs'
import { conflictFor } from './scheduleUtils'

export default function AssignmentModal({ open, context, employees, shifts, assignments, drafts, onClose, onConfirm }) {
  const [form] = Form.useForm()
  const selectedShiftId = Form.useWatch('shiftId', form)
  const selectedDate = Form.useWatch('workDate', form)
  const selectedEmployees = Form.useWatch('employeeIds', form) || []
  const workDate = selectedDate?.format('YYYY-MM-DD')
  const blocked = (employeeId) => conflictFor({ employeeId, shiftId: selectedShiftId, workDate }, [...assignments, ...drafts], shifts)
  const employeeOptions = employees.map((employee) => ({ value: employee.employeeId, label: `${employee.name} · ${employee.employmentStatus}`, disabled: Boolean(selectedShiftId && workDate && blocked(employee.employeeId)) }))
  const selectedErrors = selectedEmployees.map(blocked).filter(Boolean)

  useEffect(() => {
    if (!open) return
    form.setFieldsValue({
      employeeIds: [],
      shiftId: context?.shiftId,
      workDate: context?.workDate ? dayjs(context.workDate) : undefined,
    })
  }, [context, form, open])

  const confirm = () => form.validateFields().then((values) => {
    const date = values.workDate.format('YYYY-MM-DD')
    const rows = values.employeeIds.map((employeeId) => ({ employeeId, shiftId: values.shiftId, workDate: date }))
    const conflicts = rows.map((row) => conflictFor(row, [...assignments, ...drafts], shifts)).filter(Boolean)
    if (!conflicts.length) onConfirm(rows)
  })

  return <Modal title="Phân ca" open={open} onCancel={onClose} onOk={confirm} okText="Xác nhận" cancelText="Đóng" destroyOnHidden>
    <Form form={form} layout="vertical">
      <Form.Item label="Ngày làm" name="workDate" rules={[{ required: true, message: 'Chọn ngày làm.' }]}><DatePicker className="staff-full-width" format="DD/MM/YYYY" /></Form.Item>
      <Form.Item label="Ca làm" name="shiftId" rules={[{ required: true, message: 'Chọn ca làm.' }]}><Select placeholder="Chọn ca đang hoạt động" options={shifts.map((shift) => ({ value: shift.id, label: `${shift.name} · ${shift.startTime?.slice(0, 5)} - ${shift.endTime?.slice(0, 5)}` }))} /></Form.Item>
      <Form.Item label="Nhân viên" name="employeeIds" rules={[{ required: true, message: 'Chọn ít nhất một nhân viên.' }]}><Select mode="multiple" placeholder="Chọn nhân viên đủ điều kiện" options={employeeOptions} /></Form.Item>
      {selectedErrors.length > 0 && <Alert type="warning" showIcon message="Có nhân viên không thể thêm" description={selectedErrors[0]} />}
    </Form>
  </Modal>
}
