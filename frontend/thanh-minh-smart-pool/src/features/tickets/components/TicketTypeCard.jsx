import { Card, Tag, Tooltip, Button } from 'antd'
import { 
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
export default function TicketTypeCard({ ticketType, onEdit, onView, onToggleLock }) {
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
        borderRadius: 16,
        boxShadow: isActive ? '0 10px 30px rgba(0, 95, 142, 0.08)' : '0 4px 12px rgba(0,0,0,0.05)',
        opacity: isActive ? 1 : 0.65,
        border: 'none',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        height: '100%',
        overflow: 'hidden',
        position: 'relative',
        transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)',
      }}
      bodyStyle={{ flex: 1, padding: '24px 20px' }}
      actions={[
        <Tooltip title="Xem chi tiết" key="view">
          <Button type="text" icon={<EyeOutlined />} onClick={() => onView(ticketType)} style={{ width: '100%' }} />
        </Tooltip>,
        <Tooltip title="Sửa" key="edit">
          <Button type="text" icon={<EditOutlined />} style={{ color: '#00b4d8', width: '100%' }} onClick={() => onEdit(ticketType)} />
        </Tooltip>,
        <Tooltip title={isActive ? "Khóa vé" : "Mở khóa"} key="lock">
          <Button 
            type="text" 
            icon={isActive ? <LockOutlined /> : <UnlockOutlined />} 
            style={{ color: isActive ? '#ff4d4f' : '#52c41a', width: '100%' }} 
            onClick={() => onToggleLock(ticketType)}
          />
        </Tooltip>,
      ]}
    >
      {/* Accent Header Line */}
      <div 
        style={{ 
          height: 6, 
          background: isActive ? 'linear-gradient(90deg, #005f8e 0%, #00b4d8 100%)' : '#d9d9d9', 
          width: '100%', 
          position: 'absolute', 
          top: 0, 
          left: 0 
        }} 
      />

      {/* Header: Phân loại + Status */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <Tag 
          icon={<TagOutlined />} 
          color={categoryColor} 
          style={{ fontSize: 13, borderRadius: 20, padding: '2px 10px', border: 'none', fontWeight: 600 }}
        >
          {categoryLabel}
        </Tag>
        <span style={{ fontSize: 12, fontWeight: 600, color: isActive ? '#52c41a' : '#8c8c8c' }}>
          {isActive ? '● Đang hoạt động' : '○ Bị khóa'}
        </span>
      </div>

      {/* Tên vé */}
      <div style={{ fontWeight: 800, fontSize: 18, color: '#1a1a2e', marginBottom: 16, lineHeight: 1.3 }}>
        {name}
      </div>

      {/* Thông tin giá */}
      <div style={{ background: '#f8fafc', padding: 12, borderRadius: 12, marginBottom: 12 }}>
        <div style={{ fontSize: 12, color: '#64748b', marginBottom: 4, textTransform: 'uppercase', fontWeight: 600 }}>
          Mức giá
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontWeight: 800, fontSize: 24, color: '#005f8e' }}>
            {formattedPrice}
          </span>
        </div>
      </div>

      {/* Hiệu lực */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: '#64748b', fontSize: 13, fontWeight: 500, paddingLeft: 4 }}>
        <ClockCircleOutlined style={{ fontSize: 15 }} />
        <span>
          {durationDays ? `Hiệu lực: ${durationDays} ngày` : 'Không giới hạn thời hạn'}
        </span>
      </div>
    </Card>
  )
}
