import { useEffect, useRef } from 'react'
import { Alert, Button, Form, Input, Modal, Select } from 'antd'
import { useEquipmentFilters } from '../hooks/useEquipments'
import { useUpdateEquipment } from '../hooks/useEquipmentMutations'
import { isCamera } from '../utils/equipmentDisplay'
import { specificationError, specificationsFromMetadata, specificationsToMetadata } from '../utils/equipmentSpecifications'
import EquipmentSpecificationsEditor from './EquipmentSpecificationsEditor'

export default function EquipmentUpdateModal({ equipment, onClose }) {
  const [form] = Form.useForm()
  const filters = useEquipmentFilters()
  const mutation = useUpdateEquipment()
  const submitting = useRef(false)
  const { reset } = mutation
  const categories = [...new Set([equipment?.category, ...(filters.data?.categories || [])].filter(Boolean))]

  useEffect(() => {
    if (equipment) {
      form.resetFields()
      form.setFieldsValue({ name: equipment.name, category: equipment.category, location: equipment.location || '', specifications: specificationsFromMetadata(equipment.metadata) })
      reset()
    }
  }, [equipment, form, reset])

  function handleFinish(values) {
    if (submitting.current || mutation.isPending) return
    submitting.current = true
    mutation.mutate({
      id: equipment.id, name: values.name.trim(), category: values.category.trim(),
      location: values.location?.trim() || null,
      metadata: specificationsToMetadata(values.specifications),
    }, {
      onSuccess: onClose,
      onError: error => {
        const errors = error?.response?.data?.errors
        if (errors && typeof errors === 'object') {
          form.setFields(Object.entries(errors).flatMap(([key, messages]) => {
            const field = key.split('.').at(-1)
            const name = field.toLowerCase() === 'metadata' ? 'specifications' : field.charAt(0).toLowerCase() + field.slice(1)
            return ['name', 'category', 'location', 'specifications'].includes(name) ? [{ name, errors: Array.isArray(messages) ? messages : [String(messages)] }] : []
          }))
        }
      },
      onSettled: () => { submitting.current = false },
    })
  }

  return <Modal title="Cập nhật thiết bị" open={Boolean(equipment)} forceRender className="equipment-modal" width={680}
    onCancel={() => { if (!submitting.current) onClose() }} onOk={() => form.submit()} okText="Lưu thay đổi" cancelText="Hủy"
    confirmLoading={mutation.isPending} cancelButtonProps={{ disabled: mutation.isPending }} closable={!mutation.isPending}
    keyboard={!mutation.isPending} mask={{ closable: !mutation.isPending }}>
    <Form form={form} layout="vertical" disabled={mutation.isPending} onFinish={handleFinish} onValuesChange={() => { if (mutation.isError) mutation.reset() }}>
      {mutation.isError && <Alert type="error" showIcon className="service-inline-error" title="Cập nhật thiết bị thất bại" description={mutation.error?.response?.data?.message || 'Không thể lưu thay đổi. Vui lòng thử lại.'} />}
      {filters.isError && <Alert type="warning" showIcon className="service-inline-error" title="Không thể tải các nhóm thiết bị khác" action={<Button onClick={() => filters.refetch()}>Thử lại</Button>} />}
      <Form.Item label="Tên thiết bị" name="name" rules={[{ required: true, whitespace: true, message: 'Nhập tên thiết bị.' }, { transform: value => value?.trim(), max: 255, message: 'Tên thiết bị không được vượt quá 255 ký tự.' }]}><Input maxLength={255} placeholder="Nhập tên thiết bị" /></Form.Item>
      <Form.Item label="Nhóm thiết bị" name="category" extra={isCamera(equipment?.category) ? 'Nhóm Camera được giữ nguyên khi cập nhật thiết bị.' : undefined} rules={[{ required: true, message: 'Chọn nhóm thiết bị.' }]}>
        <Select showSearch optionFilterProp="label" loading={filters.isFetching} disabled={mutation.isPending || isCamera(equipment?.category)}
          options={categories.map(value => ({ value, label: value, disabled: isCamera(value) !== isCamera(equipment?.category) }))} />
      </Form.Item>
      <Form.Item label="Khu vực" name="location" rules={[{ transform: value => value?.trim(), max: 255, message: 'Khu vực không được vượt quá 255 ký tự.' }]}><Input maxLength={255} placeholder="Nhập khu vực đặt thiết bị" /></Form.Item>
      <Form.Item label="Thông số thiết bị" name="specifications" extra="Mỗi thiết bị có thông số riêng. Thêm, sửa hoặc xóa từng dòng theo nhu cầu." rules={[{
        validator: (_, value) => {
          const error = specificationError(value)
          return error ? Promise.reject(new Error(error)) : Promise.resolve()
        },
      }]}>
        <EquipmentSpecificationsEditor disabled={mutation.isPending} />
      </Form.Item>
    </Form>
  </Modal>
}
