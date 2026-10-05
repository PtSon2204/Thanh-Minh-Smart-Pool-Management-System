import { Card, Typography } from 'antd';
import { BellOutlined } from '@ant-design/icons';
import NotificationList from '../../features/notifications/components/NotificationList';

const { Title } = Typography;

export default function NotificationPage() {
  return (
    <div style={{ padding: 24 }}>
      <div style={{ marginBottom: 20, display: 'flex', alignItems: 'center', gap: 10 }}>
        <BellOutlined style={{ fontSize: 24, color: '#1890ff' }} />
        <Title level={3} style={{ margin: 0 }}>Thông báo hệ thống</Title>
      </div>

      <Card
        bordered={false}
        style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.06)' }}
      >
        <NotificationList />
      </Card>
    </div>
  );
}
