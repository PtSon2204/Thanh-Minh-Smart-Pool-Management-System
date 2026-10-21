import { useEffect, useRef } from 'react'
import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Select, Tag, message } from 'antd'
import dayjs from 'dayjs'
import dateLocale from 'antd/es/date-picker/locale/vi_VN'
import { useMaintenanceMutation, useMaintenanceOptions } from '../hooks/useMaintenance'
import { previewDates, recurrenceOptions, setMaintenanceErrors, vietnamToday } from '../utils/maintenanceDisplay'

export default function MaintenanceFormModal({ open, schedule, onClose }) {
  const [form] = Form.useForm()
  const mutation = useMaintenanceMutation()
  const options = useMaintenanceOptions(open)
  const submitting = useRef(false)
  const { reset } = mutation
  const start = Form.useWatch('startDate', form)
  const unit = Form.useWatch('repeatUnit', form)
  const every = Form.useWatch('repeatEvery', form)
  const dates = previewDates(start, unit, every)

  useEffect(() => {
    if (!open) return
    reset()
    form.resetFields()
    form.setFieldsValue(schedule ? { ...schedule, startDate: dayjs(schedule.startDate) } : {
      startDate: dayjs(vietnamToday()), repeatUnit: 'None', repeatEvery: 1, remindBeforeDays: 3,
    })
  }, [open, schedule, form, reset])

  async function handleFinish(values) {
    if (submitting.current) return
    submitting.current = true
    try {
      await mutation.mutateAsync({ action: schedule ? 'update' : 'create', id: schedule?.id,
        payload: { equipmentId: values.equipmentId, taskName: values.taskName.trim(), assignedTo: values.assignedTo,
          repeatUnit: values.repeatUnit, repeatEvery: values.repeatUnit === 'None' ? 1 : values.repeatEvery,
          startDate: values.startDate.format('YYYY-MM-DD'), remindBeforeDays: values.remindBeforeDays } })
      message.success(schedule ? 'Đã cập nhật lịch bảo trì' : 'Đã lập lịch bảo trì')
      onClose()
    } catch (error) { setMaintenanceErrors(form, error) }
    finally { submitting.current = false }
  }

  return <Modal title={schedule ? 'Cập nhật lịch bảo trì' : 'Lập lịch bảo trì'} open={open} forceRender width={680} className="maintenance-modal"
    onOk={() => form.submit()} onCancel={() => { if (!submitting.current) onClose() }} okText={schedule ? 'Lưu thay đổi' : 'Lập lịch'} cancelText="Hủy"
    confirmLoading={mutation.isPending} okButtonProps={{ disabled: options.isPending || options.isError || !options.data?.equipments.length || !options.data?.assignees.length }}
    cancelButtonProps={{ disabled: mutation.isPending }} closable={!mutation.isPending} keyboard={!mutation.isPending} mask={{ closable: !mutation.isPending }}>
    <Form form={form} layout="vertical" disabled={mutation.isPending} onFinish={handleFinish} onValuesChange={() => { if (mutation.isError) mutation.reset() }}>
      {mutation.isError && <Alert className="maintenance-inline-alert" type="error" showIcon title={mutation.error?.response?.data?.message || 'Không thể lưu lịch bảo trì. Vui lòng thử lại.'} />}
      {options.isError && <Alert className="maintenance-inline-alert" type="error" showIcon title="Không thể tải thiết bị và người phụ trách" action={<Button onClick={() => options.refetch()}>Thử lại</Button>} />}
      {options.data && (!options.data.equipments.length || !options.data.assignees.length) && <Alert className="maintenance-inline-alert" type="info" showIcon title={!options.data.equipments.length ? 'Thêm thiết bị trước khi lập lịch bảo trì.' : 'Cần có nhân sự đang hoạt động để phân công bảo trì.'} />}
      <Form.Item label="Thiết bị" name="equipmentId" rules={[{ required: true, message: 'Chọn thiết bị cần bảo trì.' }]}>
        <Select showSearch optionFilterProp="label" placeholder="Chọn thiết bị" loading={options.isFetching} options={options.data?.equipments || []} />
      </Form.Item>
      <Form.Item label="Tên công việc" name="taskName" rules={[{ required: true, whitespace: true, message: 'Nhập tên công việc.' }, { max: 255, message: 'Tên công việc tối đa 255 ký tự.' }]}>
        <Input maxLength={255} placeholder="Ví dụ: Kiểm tra và vệ sinh máy bơm" />
      </Form.Item>
      <div className="maintenance-form-grid">
        <Form.Item label="Chu kỳ bảo trì" name="repeatUnit" rules={[{ required: true, message: 'Chọn chu kỳ bảo trì.' }]}><Select options={recurrenceOptions} /></Form.Item>
        <Form.Item label="Lặp mỗi" name="repeatEvery" rules={[{ required: unit !== 'None', message: 'Nhập chu kỳ lặp.' }, { type: 'integer', min: 1, max: 365, message: 'Nhập số nguyên từ 1 đến 365.' }]}>
          <InputNumber min={1} max={365} precision={0} disabled={mutation.isPending || unit === 'None'} suffix={{ Day: 'ngày', Week: 'tuần', Month: 'tháng' }[unit]} />
        </Form.Item>
        <Form.Item label="Ngày bắt đầu" name="startDate" rules={[{ required: true, message: 'Chọn ngày bắt đầu.' }]}>
          <DatePicker locale={dateLocale} format="DD/MM/YYYY" placeholder="Chọn ngày bắt đầu" disabledDate={date => date.isBefore(dayjs(vietnamToday()), 'day') && date.format('YYYY-MM-DD') !== schedule?.startDate} />
        </Form.Item>
        <Form.Item label="Nhắc trước" name="remindBeforeDays" extra="Nhập 0 để chỉ nhắc từ ngày đến hạn." rules={[{ required: true, message: 'Nhập số ngày nhắc trước.' }, { type: 'integer', min: 0, max: 365, message: 'Nhập số nguyên từ 0 đến 365.' }]}>
          <InputNumber min={0} max={365} precision={0} suffix="ngày" />
        </Form.Item>
      </div>
      <Form.Item label="Người phụ trách" name="assignedTo" rules={[{ required: true, message: 'Chọn người phụ trách.' }]}>
        <Select showSearch optionFilterProp="label" placeholder="Chọn người phụ trách" loading={options.isFetching} options={options.data?.assignees || []} />
      </Form.Item>
      {dates.length > 0 && <div className="maintenance-preview"><strong>{unit === 'None' ? 'Ngày thực hiện' : 'Các mốc dự kiến'}</strong><div>{dates.map(date => <Tag key={date}>{date}</Tag>)}</div>
        {unit !== 'None' && <p>Lần tiếp theo được tạo sau khi hoàn thành công việc, theo chu kỳ đã đặt. Lịch quá hạn vẫn chờ xử lý.</p>}
      </div>}
    </Form>
  </Modal>
}
