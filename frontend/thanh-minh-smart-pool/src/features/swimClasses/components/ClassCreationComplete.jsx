import { ArrowLeftOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Card, Result, Space, Tag } from 'antd'

export default function ClassCreationComplete({ draft, onStartNew }) {
  return <Card className="swim-class-complete-card"><Result status="success" title="Lớp đã được kích hoạt" subTitle={`${draft.name} đã có ${draft.sessions.length} buổi học được lưu. Lớp có thể nhận đăng ký trong khoảng thời gian đã thiết lập.`} extra={<Space wrap><Button type="primary" icon={<PlusOutlined />} onClick={onStartNew}>Tạo lớp khác</Button><Button icon={<ArrowLeftOutlined />} href="/admin">Về bảng điều khiển</Button></Space>} />
    <div className="swim-class-complete-meta"><Tag color="success">Active</Tag><span>Mã lớp: {draft.id}</span></div>
  </Card>
}
