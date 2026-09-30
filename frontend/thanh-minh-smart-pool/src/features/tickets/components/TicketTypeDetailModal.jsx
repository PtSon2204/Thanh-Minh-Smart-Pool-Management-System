import { Modal, Descriptions, Tag } from 'antd'
import { CloseOutlined } from '@ant-design/icons'
import { TicketCategoryLabel, TicketCategoryColor } from '../types/ticketType'

export default function TicketTypeDetailModal({ open, onClose, ticketType }) {
  if (!ticketType) return null

  const { name, ticketCategory, price, durationDays, isActive, createdAt, updatedAt } = ticketType

  const formattedPrice = new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
  }).format(price)

  return (
    <Modal
      title={<span style={{ color: '#fff', fontSize: '18px' }}>Chi Tiết Loại Vé</span>}
      open={open}
      onCancel={onClose}
      footer={null}
      width={600}
      closeIcon={<CloseOutlined style={{ color: '#fff' }} />}
      styles={{
        content: { padding: 0, overflow: 'hidden', borderRadius: 12 },
        header: {
          background: 'linear-gradient(90deg, #005f8e 0%, #00b4d8 100%)',
          padding: '16px 24px',
          margin: 0,
        },
        body: { padding: '24px', background: '#f8fafc' },
      }}
    >
      <Descriptions bordered column={1} size="small" style={{ background: '#fff' }}>
        <Descriptions.Item label="Tên loại vé">
          <strong style={{ fontSize: 16 }}>{name}</strong>
        </Descriptions.Item>
        <Descriptions.Item label="Trạng thái">
          <Tag color={isActive ? 'success' : 'error'}>
            {isActive ? 'Đang hoạt động' : 'Đã khóa'}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label="Phân loại">
          <Tag color={TicketCategoryColor[ticketCategory]}>
            {TicketCategoryLabel[ticketCategory] ?? ticketCategory}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label="Giá vé">{formattedPrice}</Descriptions.Item>
        <Descriptions.Item label="Thời hạn">
          {durationDays ? `${durationDays} ngày` : 'Không giới hạn'}
        </Descriptions.Item>
        <Descriptions.Item label="Ngày tạo">
          {createdAt ? new Date(createdAt).toLocaleString('vi-VN') : '—'}
        </Descriptions.Item>
        <Descriptions.Item label="Cập nhật lần cuối">
          {updatedAt ? new Date(updatedAt).toLocaleString('vi-VN') : '—'}
        </Descriptions.Item>
        {!isActive && updatedAt && (
          <Descriptions.Item label="Ngày khóa">
            <span style={{ color: '#ff4d4f', fontWeight: 'bold' }}>
              {new Date(updatedAt).toLocaleString('vi-VN')}
            </span>
          </Descriptions.Item>
        )}
      </Descriptions>
    </Modal>
  )
}
