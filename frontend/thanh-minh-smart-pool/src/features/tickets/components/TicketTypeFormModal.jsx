import { useEffect } from 'react'
import { Modal, Form, Input, InputNumber, Button, Tag } from 'antd'
import { 
  CloseOutlined, 
  ThunderboltOutlined, 
  CalendarOutlined, 
  CheckCircleFilled, 
  TagOutlined,
  DollarOutlined,
  InfoCircleOutlined,
  PlusCircleOutlined,
  EditOutlined
} from '@ant-design/icons'
import { useCreateTicketType } from '../hooks/useCreateTicketType'
import { useUpdateTicketType } from '../hooks/useTicketTypeMutations'

/**
 * Danh sách phân loại vé với cấu hình trực quan
 */
const CATEGORY_CONFIGS = [
  {
    key: 'VE_LUOT',
    title: 'Vé lượt',
    badge: 'Linh hoạt số lượt',
    badgeColor: 'cyan',
    icon: <ThunderboltOutlined style={{ fontSize: 20, color: '#0d9488' }} />,
    desc: 'Khách tự nhập số lượt khi mua (1 lượt = 24h, ≥ 2 lượt không thời hạn).',
    unit: 'đ / lượt',
    priceLabel: 'Đơn giá cho mỗi lượt bơi (VNĐ)',
    quickPrices: [15000, 20000, 25000, 30000, 50000],
  },
  {
    key: 'VE_THANG',
    title: 'Vé tháng',
    badge: 'Trọn gói 30 ngày',
    badgeColor: 'blue',
    icon: <CalendarOutlined style={{ fontSize: 20, color: '#0284c7' }} />,
    desc: 'Hiệu lực 30 ngày liên tục kể từ khi mua, giới hạn 1 lượt bơi / ngày.',
    unit: 'đ / 30 ngày',
    priceLabel: 'Giá vé trọn gói 30 ngày (VNĐ)',
    quickPrices: [300000, 450000, 500000, 600000, 800000],
  },
]

/**
 * Component chọn phân loại vé dạng thẻ tương tác (Interactive Card Selector)
 */
function CategoryCardSelector({ value, onChange }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '12px' }}>
      {CATEGORY_CONFIGS.map((cat) => {
        const isSelected = value === cat.key
        return (
          <div
            key={cat.key}
            onClick={() => onChange?.(cat.key)}
            style={{
              position: 'relative',
              padding: '16px 14px',
              borderRadius: '12px',
              border: isSelected ? '2px solid #0096c7' : '1.5px solid #e2e8f0',
              background: isSelected ? '#f0f9ff' : '#ffffff',
              cursor: 'pointer',
              transition: 'all 0.25s cubic-bezier(0.4, 0, 0.2, 1)',
              boxShadow: isSelected ? '0 6px 16px rgba(0, 150, 199, 0.15)' : '0 1px 3px rgba(0,0,0,0.02)',
              display: 'flex',
              flexDirection: 'column',
              gap: '6px',
            }}
          >
            {/* Header: Icon + Badge + Checkmark */}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <div
                  style={{
                    width: 32,
                    height: 32,
                    borderRadius: 8,
                    background: isSelected ? '#e0f2fe' : '#f8fafc',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  {cat.icon}
                </div>
                <span style={{ fontWeight: 700, fontSize: '15px', color: '#1e293b' }}>
                  {cat.title}
                </span>
              </div>
              {isSelected ? (
                <CheckCircleFilled style={{ color: '#0096c7', fontSize: '18px' }} />
              ) : (
                <div
                  style={{
                    width: 16,
                    height: 16,
                    borderRadius: '50%',
                    border: '2px solid #cbd5e1',
                  }}
                />
              )}
            </div>

            {/* Badge pill */}
            <div style={{ marginTop: 2 }}>
              <Tag
                color={cat.badgeColor}
                style={{
                  borderRadius: 12,
                  fontSize: '11px',
                  fontWeight: 600,
                  margin: 0,
                  padding: '1px 8px',
                  border: 'none',
                }}
              >
                {cat.badge}
              </Tag>
            </div>

            {/* Description */}
            <div style={{ fontSize: '12px', color: '#64748b', lineHeight: 1.45, marginTop: 4 }}>
              {cat.desc}
            </div>
          </div>
        )
      })}
    </div>
  )
}

/**
 * Modal form để thêm mới / sửa loại vé với UI cao cấp và trực quan.
 */
export default function TicketTypeFormModal({ open, onClose, editingTicket }) {
  const [form] = Form.useForm()
  const ticketCategory = Form.useWatch('ticketCategory', form)
  const price = Form.useWatch('price', form)
  const name = Form.useWatch('name', form)

  const { mutate: createTicketType, isPending: isCreating } = useCreateTicketType()
  const { mutate: updateTicketType, isPending: isUpdating } = useUpdateTicketType()

  const isEditMode = !!editingTicket
  const isPending = isCreating || isUpdating

  // Đổ dữ liệu vào form khi mở (nếu có editingTicket)
  useEffect(() => {
    if (open) {
      if (isEditMode) {
        form.setFieldsValue({
          name: editingTicket.name,
          ticketCategory: editingTicket.ticketCategory,
          price: editingTicket.price,
        })
      } else {
        form.resetFields()
        // Mặc định chọn Vé lượt để trải nghiệm nhanh
        form.setFieldsValue({ ticketCategory: 'VE_LUOT' })
      }
    }
  }, [open, isEditMode, editingTicket, form])

  const handleSubmit = () => {
    form.validateFields().then((values) => {
      const payload = {
        name: values.name.trim(),
        ticketCategory: values.ticketCategory,
        price: values.price,
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

  const currentCategoryConfig = CATEGORY_CONFIGS.find((c) => c.key === ticketCategory)

  return (
    <Modal
      title={
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div
            style={{
              width: 36,
              height: 36,
              borderRadius: 10,
              background: 'rgba(255, 255, 255, 0.18)',
              backdropFilter: 'blur(4px)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#ffffff',
              fontSize: 18,
            }}
          >
            {isEditMode ? <EditOutlined /> : <PlusCircleOutlined />}
          </div>
          <div>
            <div style={{ color: '#ffffff', fontSize: '17px', fontWeight: 700, lineHeight: 1.25 }}>
              {isEditMode ? 'Cập Nhật Loại Vé' : 'Thêm Loại Vé Mới'}
            </div>
            <div style={{ color: '#e0f2fe', fontSize: '12px', fontWeight: 400, marginTop: 2 }}>
              Cấu hình phân loại, mức giá và chính sách áp dụng vé bơi
            </div>
          </div>
        </div>
      }
      open={open}
      onOk={handleSubmit}
      onCancel={onClose}
      okText={isEditMode ? 'Cập nhật loại vé' : 'Tạo loại vé mới'}
      cancelText="Hủy bỏ"
      confirmLoading={isPending}
      destroyOnClose
      width={600}
      closeIcon={<CloseOutlined style={{ color: '#ffffff', fontSize: 16 }} />}
      styles={{
        content: {
          padding: 0,
          overflow: 'hidden',
          borderRadius: 16,
          boxShadow: '0 20px 40px rgba(0, 31, 62, 0.2)',
          border: 'none',
        },
        header: {
          background: 'linear-gradient(135deg, #002244 0%, #004b75 55%, #0096c7 100%)',
          padding: '18px 24px',
          margin: 0,
          borderBottom: 'none',
        },
        body: {
          padding: '24px',
          background: '#f8fafc',
          maxHeight: 'calc(85vh - 120px)',
          overflowY: 'auto',
        },
        footer: {
          padding: '14px 24px',
          margin: 0,
          background: '#ffffff',
          borderTop: '1px solid #e2e8f0',
          display: 'flex',
          justifyContent: 'flex-end',
          gap: 12,
        },
      }}
      okButtonProps={{
        style: {
          background: 'linear-gradient(135deg, #005f8e 0%, #0096c7 100%)',
          borderColor: '#005f8e',
          fontWeight: 600,
          height: 38,
          borderRadius: 8,
          boxShadow: '0 4px 12px rgba(0, 150, 199, 0.25)',
        },
      }}
      cancelButtonProps={{
        style: {
          height: 38,
          borderRadius: 8,
          fontWeight: 500,
        },
      }}
    >
      <Form form={form} layout="vertical" requiredMark="optional">
        {/* 1. Tên loại vé */}
        <Form.Item
          label={
            <span style={{ fontWeight: 600, color: '#1e293b', fontSize: '13px' }}>
              <TagOutlined style={{ marginRight: 6, color: '#0096c7' }} />
              Tên loại vé
            </span>
          }
          name="name"
          normalize={(value) =>
            value.normalize('NFC').replace(/[^\p{L}\p{M}\p{Nd} ()]/gu, '').slice(0, 30)
          }
          rules={[
            { required: true, whitespace: true, message: 'Vui lòng nhập tên loại vé.' },
            { max: 30, message: 'Tên loại vé không được vượt quá 30 ký tự.' },
            {
              pattern: /^[\p{L}\p{M}\p{Nd} ()]+$/u,
              message: 'Tên loại vé chỉ được chứa chữ, số, khoảng trắng và dấu ngoặc ().',
            },
          ]}
          style={{ marginBottom: 18 }}
        >
          <Input
            placeholder={
              ticketCategory === 'VE_THANG'
                ? 'Ví dụ: Vé tháng người lớn, Vé tháng học sinh...'
                : 'Ví dụ: Vé lượt bơi tiêu chuẩn, Vé lượt cuối tuần...'
            }
            maxLength={30}
            showCount
            style={{
              height: 42,
              borderRadius: 8,
              fontSize: '14px',
              border: '1.5px solid #cbd5e1',
            }}
          />
        </Form.Item>

        {/* 2. Phân loại vé (Interactive Card Selector) */}
        <Form.Item
          label={
            <span style={{ fontWeight: 600, color: '#1e293b', fontSize: '13px' }}>
              <InfoCircleOutlined style={{ marginRight: 6, color: '#0096c7' }} />
              Phân loại vé
            </span>
          }
          name="ticketCategory"
          rules={[{ required: true, message: 'Vui lòng chọn phân loại vé.' }]}
          style={{ marginBottom: 18 }}
        >
          <CategoryCardSelector />
        </Form.Item>

        {/* 3. Giá vé + Quick Chips */}
        <div
          style={{
            background: '#ffffff',
            borderRadius: 12,
            padding: '16px',
            border: '1px solid #e2e8f0',
            marginBottom: 18,
          }}
        >
          <Form.Item
            label={
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', width: '100%' }}>
                <span style={{ fontWeight: 600, color: '#1e293b', fontSize: '13px' }}>
                  <DollarOutlined style={{ marginRight: 6, color: '#10b981' }} />
                  {currentCategoryConfig?.priceLabel ?? 'Giá vé (VNĐ)'}
                </span>
              </div>
            }
            name="price"
            rules={[
              { required: true, message: 'Vui lòng nhập giá vé.' },
              { type: 'number', min: 1, message: 'Giá vé phải lớn hơn 0.' },
            ]}
            style={{ marginBottom: 10 }}
          >
            <InputNumber
              style={{
                width: '100%',
                height: 42,
                borderRadius: 8,
                fontSize: '15px',
                fontWeight: 600,
                border: '1.5px solid #cbd5e1',
              }}
              formatter={(v) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
              parser={(v) => v?.replace(/,/g, '')}
              min={1}
              placeholder={ticketCategory === 'VE_THANG' ? '500,000' : '20,000'}
              addonAfter={
                <span style={{ fontWeight: 600, color: '#475569' }}>
                  {ticketCategory === 'VE_THANG' ? 'đ / tháng' : 'đ / lượt'}
                </span>
              }
            />
          </Form.Item>

          {/* Gợi ý giá nhanh (Quick Price Chips) */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap', marginTop: 8 }}>
            <span style={{ fontSize: '12px', color: '#64748b', marginRight: 4 }}>Gợi ý nhanh:</span>
            {currentCategoryConfig?.quickPrices.map((quickVal) => (
              <Button
                key={quickVal}
                size="small"
                onClick={() => form.setFieldValue('price', quickVal)}
                style={{
                  borderRadius: 14,
                  fontSize: '11px',
                  fontWeight: price === quickVal ? 700 : 500,
                  borderColor: price === quickVal ? '#0096c7' : '#e2e8f0',
                  color: price === quickVal ? '#0096c7' : '#475569',
                  background: price === quickVal ? '#e0f2fe' : '#f8fafc',
                  padding: '0 9px',
                }}
              >
                {new Intl.NumberFormat('vi-VN').format(quickVal)} đ
              </Button>
            ))}
          </div>
        </div>

       

        {/* 5. Xem trước tóm tắt loại vé (Live Mini Preview) */}
        {(name || price) && (
          <div
            style={{
              padding: '12px 14px',
              borderRadius: 10,
              background: '#f1f5f9',
              border: '1px dashed #cbd5e1',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flexWrap: 'wrap',
              gap: 8,
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontSize: '11px', color: '#64748b', textTransform: 'uppercase', fontWeight: 700 }}>
                Xem trước:
              </span>
              <span style={{ fontWeight: 700, color: '#1e293b', fontSize: '13px' }}>
                {name || 'Chưa đặt tên vé'}
              </span>
              <Tag
                color={ticketCategory === 'VE_LUOT' ? 'green' : 'blue'}
                style={{ borderRadius: 10, fontSize: '11px', margin: 0 }}
              >
                {ticketCategory === 'VE_LUOT' ? 'Vé lượt' : 'Vé tháng'}
              </Tag>
            </div>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 4 }}>
              <span style={{ fontWeight: 800, color: '#005f8e', fontSize: '15px' }}>
                {price ? new Intl.NumberFormat('vi-VN').format(price) : '0'} đ
              </span>
              <span style={{ fontSize: '11px', color: '#64748b' }}>
                {ticketCategory === 'VE_LUOT' ? '/ lượt' : '/ 30 ngày'}
              </span>
            </div>
          </div>
        )}
      </Form>
    </Modal>
  )
}
