import { useEffect, useRef } from 'react'
import { Alert, Checkbox, Form, Input, InputNumber, Modal, message } from 'antd'
import { useMaintenanceMutation } from '../hooks/useMaintenance'
import { setMaintenanceErrors } from '../utils/maintenanceDisplay'

export default function CompleteMaintenanceModal({ schedule, onClose }) {
  const [form] = Form.useForm()
  const mutation = useMaintenanceMutation()
  const submitting = useRef(false)
  const { reset } = mutation
  useEffect(() => { if (schedule) { form.resetFields(); reset() } }, [schedule, form, reset])

  async function finish(values) {
    if (submitting.current) return
    submitting.current = true
    try {
      await mutation.mutateAsync({ action: 'complete', id: schedule.id, payload: {
        cost: values.cost || 0, vendorName: values.vendorName?.trim() || null, note: values.note.trim(), readyForUse: Boolean(values.readyForUse),
      } })
      message.success('Đã lưu kết quả bảo trì')
      onClose()
    } catch (error) { setMaintenanceErrors(form, error) }
    finally { submitting.current = false }
  }

  return <Modal title="Hoàn thành bảo trì" open={Boolean(schedule)} forceRender width={600} className="maintenance-modal"
    onOk={() => form.submit()} onCancel={() => { if (!submitting.current) onClose() }} okText="Lưu kết quả" cancelText="Hủy"
    confirmLoading={mutation.isPending} closable={!mutation.isPending} keyboard={!mutation.isPending} mask={{ closable: !mutation.isPending }} cancelButtonProps={{ disabled: mutation.isPending }}>
    <p className="maintenance-modal-intro"><strong>{schedule?.taskName}</strong><br />{schedule?.equipmentName}</p>
    <Form form={form} layout="vertical" disabled={mutation.isPending} onFinish={finish} initialValues={{ cost: 0, readyForUse: false }} onValuesChange={() => { if (mutation.isError) mutation.reset() }}>
      {mutation.isError && <Alert className="maintenance-inline-alert" type="error" showIcon title={mutation.error?.response?.data?.message || 'Không thể lưu kết quả bảo trì.'} />}
      <Form.Item name="note" label="Kết quả thực hiện" rules={[{ required: true, whitespace: true, message: 'Nhập kết quả thực hiện.' }, { max: 4000, message: 'Tối đa 4000 ký tự.' }]}><Input.TextArea rows={4} maxLength={4000} placeholder="Nội dung đã thực hiện, linh kiện thay thế và kết quả kiểm tra..." /></Form.Item>
      <Form.Item name="cost" label="Chi phí thực tế" rules={[{ type: 'number', min: 0, message: 'Chi phí không được âm.' }]}><InputNumber min={0} max={9999999999999} precision={0} suffix="VNĐ" /></Form.Item>
      <Form.Item name="vendorName" label="Đơn vị bảo trì (nếu thuê ngoài)" rules={[{ max: 255, message: 'Tên đơn vị tối đa 255 ký tự.' }]}><Input maxLength={255} placeholder="Tên đơn vị thực hiện" /></Form.Item>
      <Form.Item name="readyForUse" valuePropName="checked"><Checkbox>Thiết bị đã đủ điều kiện sử dụng lại</Checkbox></Form.Item>
      <Alert type="info" showIcon title="Thiết bị đang bị khóa sẽ giữ nguyên trạng thái khóa." description="Chỉ đưa thiết bị đang bảo trì trở lại hoạt động khi bạn xác nhận đủ điều kiện sử dụng." />
    </Form>
  </Modal>
}
