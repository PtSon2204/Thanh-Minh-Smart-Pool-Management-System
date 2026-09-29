import { Card, Tag, Tooltip, Button } from 'antd'
import { 
  CalendarOutlined, 
  TagOutlined, 
  ClockCircleOutlined,
  EditOutlined,
  LockOutlined,
  UnlockOutlined,
  EyeOutlined
} from '@ant-design/icons'
import { TicketCategoryLabel, TicketCategoryColor } from '../types/ticketType'

/**
 * Card hiển thị thông tin 1 loại vé.
 * Props:
 *   ticketType — object từ API (id, name, ticketCategory, price, durationDays, isActive, createdAt)
 */
export default function TicketTypeCard({ ticketType }) {
  const { name, ticketCategory, price, durationDays, isActive } = ticketType

  const categoryLabel = TicketCategoryLabel[ticketCategory] ?? ticketCategory
  const categoryColor = TicketCategoryColor[ticketCategory] ?? 'default'

  const formattedPrice = new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
  }).format(price)

  return (
    <Card
      hoverable
      style={{
        borderRadius: 12,
        boxShadow: '0 2px 8px rgba(0,0,0,0.07)',
        opacity: isActive ? 1 : 0.6,
        borderTop: `4px solid ${isActive ? '#005f8e' : '#d9d9d9'}`,
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        height: '100%',
      }}
      bodyStyle={{ flex: 1 }}
      actions={[
        <Tooltip title="Xem chi tiết" key="view">
          <Button type="text" icon={<EyeOutlined />} />
        </Tooltip>,
        <Tooltip title="Sửa" key="edit">
          <Button type="text" icon={<EditOutlined />} style={{ color: '#1677ff' }} />
        </Tooltip>,
        <Tooltip title={isActive ? "Khóa loại vé" : "Mở khóa"} key="lock">
          <Button 
            type="text" 
            icon={isActive ? <LockOutlined /> : <UnlockOutlined />} 
            style={{ color: isActive ? '#ff4d4f' : '#52c41a' }} 
          />
        </Tooltip>,
      ]}
    >
      {/* Header: Tên + Badge trạng thái */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 12 }}>
        <span style={{ fontWeight: 700, fontSize: 16, color: '#1a1a2e', flex: 1, marginRight: 8 }}>
          {name}
        </span>
        <Tag color={isActive ? 'success' : 'default'}>
          {isActive ? 'Đang hoạt động' : 'Ngừng hoạt động'}
        </Tag>
      </div>

      {/* Phân loại */}
      <div style={{ marginBottom: 8 }}>
        <Tag icon={<TagOutlined />} color={categoryColor} style={{ fontSize: 13 }}>
          {categoryLabel}
        </Tag>
      </div>

      {/* Giá */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
        <CalendarOutlined style={{ color: '#005f8e' }} />
        <span style={{ fontWeight: 700, fontSize: 18, color: '#005f8e' }}>
          {formattedPrice}
        </span>
      </div>

      {/* Hiệu lực */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: '#64748b', fontSize: 13 }}>
        <ClockCircleOutlined />
        <span>
          {durationDays ? `Hiệu lực: ${durationDays} ngày` : 'Không giới hạn thời hạn'}
        </span>
      </div>
    </Card>
  )
}
