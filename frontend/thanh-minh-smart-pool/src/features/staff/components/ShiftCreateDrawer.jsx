import { useEffect, useRef, useState } from 'react'
import { Alert, Button, Drawer, Form, Input, message, TimePicker } from 'antd'
import dayjs from 'dayjs'
import { useCreateShift, useUpdateShift } from '../hooks/useShifts'

function getShiftErrorMessage(error) {
  if (error?.response?.status === 401 || error?.response?.status === 403) return 'Bạn không có quyền lưu ca làm việc.'
  const data = error?.response?.data
  if (error?.response?.status === 409) return data?.detail || data?.message || 'Ca làm việc đã được cập nhật bởi người khác.'
  return data?.detail || data?.message || 'Máy chủ không thể lưu ca làm việc.'
}

function isOvernight(startTime, endTime) {
  return Boolean(startTime && endTime && endTime.isBefore(startTime))
}

export default function ShiftCreateDrawer({ open, onClose, shift = null }) {
  const [form] = Form.useForm()
  const [messageApi, messageContextHolder] = message.useMessage()
  const submitting = useRef(false)
  const [saveError, setSaveError] = useState(null)
  const createMutation = useCreateShift()
  const updateMutation = useUpdateShift()
  const mutation = shift ? updateMutation : createMutation

  useEffect(() => {
    if (!open) {
      form.resetFields()
      setSaveError(null)
      createMutation.reset()
      updateMutation.reset()
      return
    }
    setSaveError(null)
    if (shift) {
      form.setFieldsValue({
        name: shift.name,
        startTime: dayjs(`2000-01-01T${shift.startTime}`),
        endTime: dayjs(`2000-01-01T${shift.endTime}`),
      })
    } else {
      form.resetFields()
    }
  }, [createMutation.reset, form, open, shift, updateMutation.reset])

  const close = () => {
    if (!mutation.isPending) onClose()
  }

  const handleFinish = (values) => {
    if (submitting.current || mutation.isPending) return
    submitting.current = true
    setSaveError(null)
    const data = {
      name: values.name.trim(),
      startTime: values.startTime.format('HH:mm:ss'),
      endTime: values.endTime.format('HH:mm:ss'),
      isActive: shift ? shift.isActive : true,
    }
    mutation.mutate(shift ? { id: shift.id, data } : data, {
      onSuccess: () => {
        form.resetFields()
        onClose()
      },
      onError: (error) => {
        setSaveError(error)
        messageApi.error(getShiftErrorMessage(error))
      },
      onSettled: () => { submitting.current = false },
    })
  }

  const startTime = Form.useWatch('startTime', form)
  const endTime = Form.useWatch('endTime', form)

  return <Drawer rootClassName="staff-shift-drawer" title={shift ? 'Chỉnh sửa ca làm việc' : 'Tạo ca làm việc'} open={open} onClose={close} size="default" forceRender closable={!mutation.isPending} mask={{ closable: !mutation.isPending }} extra={<Button type="primary" loading={mutation.isPending} disabled={mutation.isPending} onClick={() => form.submit()}>{shift ? 'Lưu thay đổi' : 'Lưu ca'}</Button>}>
    {messageContextHolder}
    <Form form={form} layout="vertical" onFinish={handleFinish} preserve>
      {(saveError || mutation.error) && <Alert type="error" showIcon role="alert" title="Không thể lưu ca làm việc" description={getShiftErrorMessage(saveError || mutation.error)} action={<Button size="small" disabled={mutation.isPending} onClick={() => form.submit()}>Thử lại</Button>} />}
      <p className="staff-shift-drawer-note">Ca làm việc là mẫu giờ; ngày làm và nhân viên được chọn khi phân ca.</p>
      <Form.Item label="Tên ca" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên ca.' }, { max: 100, message: 'Tên ca không được vượt quá 100 ký tự.' }]}>
        <Input maxLength={100} placeholder="Ví dụ: Ca sáng" />
      </Form.Item>
      <div className="staff-shift-time-grid">
        <Form.Item label="Bắt đầu" name="startTime" rules={[{ required: true, message: 'Chọn giờ bắt đầu.' }]}><TimePicker className="staff-shift-time-picker" format="HH:mm" minuteStep={5} /></Form.Item>
        <Form.Item label="Kết thúc" name="endTime" dependencies={['startTime']} rules={[{ required: true, message: 'Chọn giờ kết thúc.' }, ({ getFieldValue }) => ({ validator(_, value) { const start = getFieldValue('startTime'); return start && value && value.isSame(start) ? Promise.reject(new Error('Giờ kết thúc phải khác giờ bắt đầu.')) : Promise.resolve() } })]}><TimePicker className="staff-shift-time-picker" format="HH:mm" minuteStep={5} /></Form.Item>
      </div>
      {isOvernight(startTime, endTime) && <Alert type="info" showIcon message="Kết thúc vào ngày hôm sau" />}
    </Form>
  </Drawer>
}
