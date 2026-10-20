import { useEffect } from 'react'
import { Modal, Form, Input, Select, InputNumber } from 'antd'
import { CloseOutlined } from '@ant-design/icons'
import { useCreateTicketType } from '../hooks/useCreateTicketType'
import { useUpdateTicketType } from '../hooks/useTicketTypeMutations'
import { TicketCategoryOptions, TicketValidityLabel } from '../types/ticketType'

/**
 * Modal form để thêm mới / sửa loại vé.
 * Props:
 *   open          — boolean hiển thị/ẩn modal
 *   onClose       — callback đóng modal
 *   editingTicket — object vé đang sửa (nếu null là chế độ thêm mới)
 */
export default function TicketTypeFormModal({ open, onClose, editingTicket }) {
  const [form] = Form.useForm()
  const ticketCategory = Form.useWatch('ticketCategory', form)
  
  const { mutate: createTicketType, isPending: isCreating } = useCreateTicketType()
  const { mutate: updateTicketType, isPending: isUpdating } = useUpdateTicketType()
  
  const isEditMode = !!editingTicket
  const isPending = isCreating || isUpdating

  // Đổ dữ liệu vào form khi mở (nếu có editingTicket)
  useEffect(() => {
    if (open) {
      if (isEditMode) {
        form.setFieldsValue(editingTicket)
      } else {
        form.resetFields()
      }
    }
  }, [open, isEditMode, editingTicket, form])

  const handleSubmit = () => {
    form.validateFields().then((values) => {
      const payload = {
        name:           values.name,
        ticketCategory: values.ticketCategory,
        price:          values.price,
      }

      if (isEditMode) {
        updateTicketType(
          { id: editingTicket.id, data: payload },
          { onSuccess: () => onClose() }
        )
      } else {
        createTicketType(payload, { onSuccess: () => onClose() })
      }
    })
  }

  return (
    <Modal
      title={<span style={{ color: '#fff', fontSize: '18px' }}>{isEditMode ? 'Cập Nhật Loại Vé' : 'Thêm Loại Vé Mới'}</span>}
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
      <Form form={form} layout="vertical">
        {/* Tên loại vé */}
        <Form.Item
          label="Tên loại vé"
          name="name"
          normalize={(value) => value.normalize('NFC').replace(/[^\p{L}\p{M}\p{Nd} ()]/gu, '').slice(0, 30)}
          rules={[
            { required: true, whitespace: true, message: 'Vui lòng nhập tên loại vé.' },
            { max: 30, message: 'Tên loại vé không được vượt quá 30 ký tự.' },
            { pattern: /^[\p{L}\p{M}\p{Nd} ()]+$/u, message: 'Tên loại vé chỉ được chứa chữ, số, khoảng trắng và dấu ngoặc ().' },
          ]}
        >
          <Input placeholder="Ví dụ: Vé tháng (30 ngày)" maxLength={30} showCount />
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

          <Form.Item label="Hiệu lực" extra="Tự động theo phân loại vé">
            <Input readOnly value={TicketValidityLabel[ticketCategory] ?? ''} placeholder="Chọn phân loại vé" />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}
