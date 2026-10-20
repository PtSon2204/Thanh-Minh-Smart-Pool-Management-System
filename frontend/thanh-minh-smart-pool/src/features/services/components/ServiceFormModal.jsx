import { useEffect, useRef } from 'react'
import { Alert, Button, Form, Input, InputNumber, Modal } from 'antd'
import { getServiceErrorMessage, useCreateService, useUpdateService } from '../hooks/useServiceMutations'

export default function ServiceFormModal({ open, onClose, service }) {
  const [form] = Form.useForm()
  const submitting = useRef(false)
  const createServiceMutation = useCreateService()
  const updateServiceMutation = useUpdateService()
  const isEditing = Boolean(service)
  const isPending = createServiceMutation.isPending || updateServiceMutation.isPending
  const mutationError = isEditing ? updateServiceMutation.error : createServiceMutation.error

  useEffect(() => {
    if (!open) return

    form.resetFields()

    if (service) {
      form.setFieldsValue({
        name: service.name,
        price: service.price,
        stockQuantity: undefined,
      })
      return
    }

    form.setFieldsValue({ name: undefined, price: undefined, stockQuantity: 0 })
  }, [form, open, service])

  const handleFinish = (values) => {
    if (submitting.current || isPending) return
    submitting.current = true

    const payload = {
      name: values.name.trim(),
      price: values.price,
    }

    if (isEditing) {
      updateServiceMutation.reset()
      updateServiceMutation.mutate({ id: service.id, data: payload }, { onSuccess: onClose, onSettled: () => { submitting.current = false } })
      return
    }

    createServiceMutation.reset()
    createServiceMutation.mutate({ ...payload, isActive: true, stockQuantity: values.stockQuantity }, { onSuccess: onClose, onSettled: () => { submitting.current = false } })
  }

  const handleCancel = () => {
    if (!isPending) {
      createServiceMutation.reset()
      updateServiceMutation.reset()
      onClose()
    }
  }

  return (
    <Modal
      title={isEditing ? 'Cập nhật đồ cho thuê' : 'Thêm đồ cho thuê'}
      open={open}
      forceRender
      onCancel={handleCancel}
      onOk={() => form.submit()}
      okText="Lưu"
      cancelText="Hủy"
      confirmLoading={isPending}
      cancelButtonProps={{ disabled: isPending }}
      closable={!isPending}
      keyboard={!isPending}
      mask={{ closable: !isPending }}
      width={560}
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} preserve={false}>
        {mutationError && <Alert className="service-inline-error" type="error" showIcon message="Không thể lưu dịch vụ" description={getServiceErrorMessage(mutationError, 'Máy chủ không thể lưu dịch vụ.')} action={<Button size="small" danger onClick={() => form.submit()} disabled={isPending}>Thử lại</Button>} />}
        <Form.Item label="Tên đồ cho thuê" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên đồ cho thuê.' }, { max: 255, message: 'Tên không vượt quá 255 ký tự.' }]}>
          <Input placeholder="Ví dụ: Khăn tắm" />
        </Form.Item>
        <Form.Item label="Giá thuê mỗi đơn vị (VNĐ)" name="price" rules={[{ required: true, message: 'Nhập giá thuê.' }, { type: 'number', min: 0, message: 'Giá không được âm.' }]}>
          <InputNumber min={0} className="service-full-width" />
        </Form.Item>
        {!isEditing && (
          <Form.Item label="Số lượng ban đầu có thể cho thuê" name="stockQuantity" rules={[{ required: true, type: 'integer', min: 0, max: 2147483647, message: 'Nhập số nguyên không âm hợp lệ.' }]} extra="Có thể điều chỉnh số lượng đồ thuê sau khi kiểm kê.">
            <InputNumber min={0} max={2147483647} precision={0} className="service-full-width" />
          </Form.Item>
        )}
      </Form>
    </Modal>
  )
}
