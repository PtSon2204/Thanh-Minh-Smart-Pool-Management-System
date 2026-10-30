import { useEffect, useState } from 'react'
import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, message } from 'antd'
import { useUpdateSwimClass } from '../hooks/useSwimClassManagement'
import { applyClassServerErrors, classServerMessage } from '../utils/classDisplay'
import { utcTimeToVietnamPicker, vietnamPickerTimeToUtc } from '../utils/classSchedule'

const TIME_PICKER = { format: 'HH:mm', minuteStep: 15 }

export default function SwimClassEditModal({ swimClass, open, onClose, onReload }) {
  const [form] = Form.useForm()
  const [error, setError] = useState(null)
  const update = useUpdateSwimClass()

  useEffect(() => {
    if (!open || !swimClass) return
    form.setFieldsValue({
      name: swimClass.name,
      description: swimClass.description || '',
      capacity: swimClass.capacity,
      fee: swimClass.fee,
      registrationStartAt: utcTimeToVietnamPicker(swimClass.registrationStartAt),
      registrationEndAt: utcTimeToVietnamPicker(swimClass.registrationEndAt),
    })
  }, [open, swimClass, form])

  async function submit(values) {
    setError(null)
    try {
      await update.mutateAsync({
        classId: swimClass.id,
        payload: {
          name: values.name.trim(),
          description: values.description?.trim() || null,
          capacity: values.capacity,
          fee: values.fee,
          registrationStartAt: vietnamPickerTimeToUtc(values.registrationStartAt),
          registrationEndAt: vietnamPickerTimeToUtc(values.registrationEndAt),
          expectedUpdatedAt: swimClass.updatedAt,
        },
      })
      onClose()
      message.success('Đã cập nhật thông tin lớp.')
    } catch (requestError) {
      applyClassServerErrors(form, requestError)
      setError(classServerMessage(requestError, 'Không thể cập nhật lớp. Vui lòng thử lại.'))
    }
  }

  return <Modal title="Chỉnh sửa thông tin lớp" open={open} onCancel={onClose} footer={null}
    width={720} destroyOnHidden maskClosable={!update.isPending} closable={!update.isPending}>
    {error && <Alert className="swim-class-form-alert" type="error" showIcon title={error}
      action={update.isError && update.error?.response?.status === 409
        ? <Button size="small" onClick={() => { onClose(); onReload() }}>Tải lại dữ liệu</Button> : undefined} />}
    <Form form={form} layout="vertical" onFinish={submit} disabled={update.isPending}>
      <div className="swim-class-form-grid">
        <Form.Item className="swim-class-span-full" label="Tên lớp" name="name" rules={[
          { required: true, whitespace: true, message: 'Nhập tên lớp.' },
          { transform: value => value?.trim(), max: 150, message: 'Tên lớp tối đa 150 ký tự.' },
        ]}><Input maxLength={150} showCount /></Form.Item>
        <Form.Item className="swim-class-span-full" label="Mô tả" name="description"><Input.TextArea rows={3} /></Form.Item>
        <Form.Item label="Sức chứa" name="capacity" rules={[{ required: true, message: 'Nhập sức chứa.' }, { type: 'number', min: 1, message: 'Sức chứa phải lớn hơn 0.' }]}><InputNumber min={1} precision={0} className="swim-class-full-width" addonAfter="học viên" /></Form.Item>
        <Form.Item label="Học phí" name="fee" rules={[{ required: true, message: 'Nhập học phí.' }, { type: 'number', min: 0, message: 'Học phí không được âm.' }]}><InputNumber min={0} precision={2} className="swim-class-full-width" addonAfter="VNĐ" /></Form.Item>
        <Form.Item label="Bắt đầu nhận đăng ký" name="registrationStartAt" rules={[{ required: true, message: 'Chọn thời gian bắt đầu.' }]}><DatePicker className="swim-class-full-width" showTime={TIME_PICKER} format="DD/MM/YYYY HH:mm" /></Form.Item>
        <Form.Item label="Kết thúc nhận đăng ký" name="registrationEndAt" dependencies={['registrationStartAt']} rules={[
          { required: true, message: 'Chọn thời gian kết thúc.' },
          ({ getFieldValue }) => ({ validator(_, value) {
            const start = getFieldValue('registrationStartAt')
            return !value || !start || vietnamPickerTimeToUtc(value) > vietnamPickerTimeToUtc(start)
              ? Promise.resolve() : Promise.reject(new Error('Thời gian kết thúc phải sau thời gian bắt đầu.'))
          } }),
        ]}><DatePicker className="swim-class-full-width" showTime={TIME_PICKER} format="DD/MM/YYYY HH:mm" /></Form.Item>
      </div>
      <p className="swim-class-detail-timezone">Thời gian nhập theo giờ Việt Nam (UTC+7).</p>
      <div className="swim-class-edit-actions"><Button onClick={onClose} disabled={update.isPending}>Hủy</Button>
        <Button type="primary" htmlType="submit" loading={update.isPending}>Lưu thay đổi</Button></div>
    </Form>
  </Modal>
}
