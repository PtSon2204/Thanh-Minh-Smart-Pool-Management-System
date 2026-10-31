import { useEffect, useRef } from 'react'
import { Alert, Button, DatePicker, Drawer, Empty, Form, Select, Spin } from 'antd'
import dayjs from 'dayjs'
import { getStaffErrorMessage, useCreateStaff, useUpdateStaff } from '../hooks/useStaffMutations'
import { useStaffOptions } from '../hooks/useStaffs'

const statusOptions = [
  { label: 'Đang làm việc', value: 'Working' },
  { label: 'Không hoạt động', value: 'Inactive' },
]

export default function StaffEmploymentFormDrawer({ open, onClose, staff }) {
  const [form] = Form.useForm()
  const submitting = useRef(false)
  const isEditing = Boolean(staff)
  const optionsQuery = useStaffOptions(open && !isEditing)
  const createMutation = useCreateStaff()
  const updateMutation = useUpdateStaff()
  const mutation = isEditing ? updateMutation : createMutation
  const isPending = mutation.isPending

  useEffect(() => {
    if (!open) return
    form.resetFields()
    form.setFieldsValue(isEditing
      ? { status: staff.status }
      : { joinDate: dayjs(), status: 'Working' })
  }, [form, isEditing, open, staff])

  const close = () => {
    if (!isPending) onClose()
  }

  const handleFinish = (values) => {
    if (submitting.current || isPending) return
    submitting.current = true
    const data = { status: values.status }
    if (isEditing) {
      updateMutation.mutate({ userId: staff.userId, data }, { onSuccess: onClose, onSettled: () => { submitting.current = false } })
      return
    }
    createMutation.mutate({ userId: values.userId, joinDate: values.joinDate.format('YYYY-MM-DD'), ...data }, { onSuccess: onClose, onSettled: () => { submitting.current = false } })
  }

  const users = optionsQuery.data?.users || []
  return <Drawer rootClassName="staff-employment-drawer" title={isEditing ? 'Cập nhật việc làm' : 'Thêm nhân viên'} open={open} onClose={close} size="default" forceRender closable={!isPending} mask={{ closable: !isPending }} extra={<Button type="primary" onClick={() => form.submit()} loading={isPending}>Lưu</Button>}>
    {optionsQuery.isLoading && !isEditing && <div className="admin-management-state"><Spin size="large" /></div>}
    {optionsQuery.isError && !isEditing && <Empty description="Không thể tải tài khoản đủ điều kiện."><Button onClick={optionsQuery.refetch}>Thử lại</Button></Empty>}
    {(isEditing || (!optionsQuery.isLoading && !optionsQuery.isError)) && <Form form={form} layout="vertical" onFinish={handleFinish} initialValues={isEditing ? { status: staff.status } : { joinDate: dayjs(), status: 'Working' }} preserve={false}>
      {mutation.error && <Alert type="error" showIcon title="Không thể lưu hồ sơ nhân viên" description={getStaffErrorMessage(mutation.error, 'Máy chủ không thể lưu hồ sơ nhân viên.')} action={<Button size="small" onClick={() => form.submit()} disabled={isPending}>Thử lại</Button>} />}
      {!isEditing && <>
        <Form.Item label="Tài khoản" name="userId" rules={[{ required: true, message: 'Chọn tài khoản.' }]}>
          <Select showSearch optionFilterProp="label" placeholder="Chọn tài khoản đủ điều kiện" options={users.map((user) => ({ value: user.userId, label: user.fullName || user.username || user.email || user.userId }))} />
        </Form.Item>
        <Form.Item label="Ngày vào làm" name="joinDate" rules={[{ required: true, message: 'Chọn ngày vào làm.' }]}><DatePicker className="service-full-width" format="DD/MM/YYYY" /></Form.Item>
      </>}
      <Form.Item label="Trạng thái việc làm" name="status" rules={[{ required: true, message: 'Chọn trạng thái.' }]}><Select options={statusOptions} /></Form.Item>
    </Form>}
  </Drawer>
}
