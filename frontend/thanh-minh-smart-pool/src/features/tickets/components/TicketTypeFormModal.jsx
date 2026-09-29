import { useEffect } from 'react'
import { Modal, Form, Input, Select, InputNumber } from 'antd'
import { CloseOutlined } from '@ant-design/icons'
import { useCreateTicketType } from '../hooks/useCreateTicketType'
import { TicketCategoryOptions } from '../types/ticketType'

/**
 * Modal form để thêm mới loại vé.
 * Props:
 *   open     — boolean hiển thị/ẩn modal
 *   onClose  — callback đóng modal
 */
export default function TicketTypeFormModal({ open, onClose }) {
  const [form] = Form.useForm()
  const { mutate: createTicketType, isPending } = useCreateTicketType()

  // Reset form mỗi lần mở
  useEffect(() => {
    if (open) form.resetFields()
  }, [open, form])

  const handleSubmit = () => {
    form.validateFields().then((values) => {
      createTicketType(
        {
          name:           values.name,
          ticketCategory: values.ticketCategory,
          price:          values.price,
          durationDays:   values.durationDays ?? null,
        },
        {
          onSuccess: () => onClose(),
        }
      )
    })
  }

  return (
    <Modal
      title={<span style={{ color: '#fff', fontSize: '18px' }}>Thêm Loại Vé Mới</span>}
      open={open}
      onOk={handleSubmit}
      onCancel={onClose}
      okText="Lưu"
      cancelText="Hủy"
      confirmLoading={isPending}
      destroyOnClose
      width={520}
      closeIcon={<CloseOutlined style={{ color: '#fff' }} />}
      styles={{
        content: {
          padding: 0,
          overflow: 'hidden',
          borderRadius: 12,
        },
        header: {
          background: 'linear-gradient(90deg, #005f8e 0%, #00b4d8 100%)',
          padding: '16px 24px',
          margin: 0,
        },
        body: {
          padding: '24px',
          background: '#f8fafc',
        },
        footer: {
          padding: '12px 24px',
          margin: 0,
          background: '#f8fafc',
          borderTop: '1px solid #e2e8f0',
        }
      }}
      okButtonProps={{
        style: { background: '#005f8e', borderColor: '#005f8e' }
      }}
    >
      <Form
        form={form}
        layout="vertical"
        initialValues={{ durationDays: null }}
      >
        {/* Tên loại vé */}
        <Form.Item
          label="Tên loại vé"
          name="name"
          rules={[
            { required: true, message: 'Vui lòng nhập tên loại vé.' },
            { max: 255, message: 'Tên không vượt quá 255 ký tự.' },
          ]}
        >
          <Input placeholder="Ví dụ: Vé tháng học sinh" />
        </Form.Item>

        {/* Phân loại */}
        <Form.Item
          label="Phân loại vé"
          name="ticketCategory"
          rules={[{ required: true, message: 'Vui lòng chọn phân loại vé.' }]}
        >
          <Select placeholder="Chọn phân loại..." options={TicketCategoryOptions} />
        </Form.Item>

        {/* Giá + Hiệu lực — 2 cột */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
          <Form.Item
            label="Giá vé (VNĐ)"
            name="price"
            rules={[
              { required: true, message: 'Vui lòng nhập giá vé.' },
              { type: 'number', min: 1, message: 'Giá phải lớn hơn 0.' },
            ]}
          >
            <InputNumber
              style={{ width: '100%' }}
              formatter={(v) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
              parser={(v) => v?.replace(/,/g, '')}
              min={1}
              placeholder="500,000"
            />
          </Form.Item>

          <Form.Item
            label="Hiệu lực (ngày)"
            name="durationDays"
            extra="Để trống nếu không giới hạn"
          >
            <InputNumber
              style={{ width: '100%' }}
              min={1}
              placeholder="30"
            />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}
