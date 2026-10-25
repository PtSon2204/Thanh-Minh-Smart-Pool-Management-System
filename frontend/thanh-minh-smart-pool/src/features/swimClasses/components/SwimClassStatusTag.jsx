import { Tag } from 'antd'

const display = {
  Draft: { label: 'Bản nháp', color: 'default' },
  Active: { label: 'Đang hoạt động', color: 'success' },
  Inactive: { label: 'Ngừng hoạt động', color: 'error' },
}

export default function SwimClassStatusTag({ status }) {
  const item = display[status] || { label: status || 'Chưa xác định', color: 'default' }
  return <Tag color={item.color}>{item.label}</Tag>
}
