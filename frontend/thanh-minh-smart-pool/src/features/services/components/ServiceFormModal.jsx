import { useEffect, useRef } from 'react'
import { Alert, Button, Form, Input, InputNumber, Modal, Select, Switch } from 'antd'
import { getServiceErrorMessage, useCreateService, useUpdateService } from '../hooks/useServiceMutations'

const serviceTypeOptions = [
  { label: 'Bán hàng', value: 'Sale' },
  { label: 'Cho thuê', value: 'Rental' },
]

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
        type: service.type,
        price: service.price,
        stockQuantity: undefined,
      })
      return
    }

    form.setFieldsValue({ name: undefined, type: undefined, price: undefined, trackStock: true })
  }, [form, open, service])

  const handleFinish = (values) => {
    if (submitting.current || isPending) return
    submitting.current = true

    const payload = {
      name: values.name.trim(),
      type: values.type,
      price: values.price,
    }

    if (isEditing) {
      updateServiceMutation.reset()
      updateServiceMutation.mutate({ id: service.id, data: payload }, { onSuccess: onClose, onSettled: () => { submitting.current = false } })
      return
    }

    createServiceMutation.reset()
    createServiceMutation.mutate({ ...payload, isActive: true, stockQuantity: values.trackStock ? 0 : null }, { onSuccess: onClose, onSettled: () => { submitting.current = false } })
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
      title={isEditing ? 'Cập nhật dịch vụ' : 'Thêm dịch vụ'}
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
        <Form.Item label="Tên dịch vụ" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên dịch vụ.' }, { max: 255, message: 'Tên không vượt quá 255 ký tự.' }]}>
          <Input placeholder="Ví dụ: Khăn tắm" />
        </Form.Item>
        <div className="service-form-grid">
          <Form.Item label="Loại dịch vụ" name="type" rules={[{ required: true, message: 'Chọn loại dịch vụ.' }]}>
            <Select options={serviceTypeOptions} placeholder="Chọn loại" />
          </Form.Item>
          <Form.Item label="Giá bán / thuê (VNĐ)" name="price" rules={[{ required: true, message: 'Nhập giá dịch vụ.' }, { type: 'number', min: 0, message: 'Giá không được âm.' }]}>
            <InputNumber min={0} className="service-full-width" />
          </Form.Item>
        </div>
        {!isEditing && (
          <Form.Item label="Theo dõi tồn kho" name="trackStock" valuePropName="checked" extra="Tồn kho ban đầu là 0. Lập phiếu nhập hàng để lưu số lượng và giá nhập.">
            <Switch />
          </Form.Item>
        )}
      </Form>
    </Modal>
  )
}
