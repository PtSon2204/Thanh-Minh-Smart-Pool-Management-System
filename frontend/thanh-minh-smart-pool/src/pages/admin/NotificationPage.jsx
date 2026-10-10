import { useState } from 'react';
import { Card, Typography, Tabs } from 'antd';
import { BellOutlined, MailOutlined } from '@ant-design/icons';
import NotificationList from '../../features/notifications/components/NotificationList';
import EmailNotificationManager from '../../features/notifications/components/EmailNotificationManager';

const { Title } = Typography;

export default function NotificationPage() {
  const [activeTab, setActiveTab] = useState('notifications');

  const tabItems = [
    {
      key: 'notifications',
      label: (
        <span>
          <BellOutlined /> Danh sách thông báo
        </span>
      ),
      children: <NotificationList />,
    },
    {
      key: 'email',
      label: (
        <span>
          <MailOutlined /> Gửi thông báo tự động qua Email
        </span>
      ),
      children: <EmailNotificationManager />,
    },
  ];

  return (
    <div style={{ padding: 24 }}>
      <div style={{ marginBottom: 20, display: 'flex', alignItems: 'center', gap: 10 }}>
        <BellOutlined style={{ fontSize: 24, color: '#005f8e' }} />
        <Title level={3} style={{ margin: 0, color: '#0f172a' }}>
          Quản Lý Thông Báo & Hỗ Trợ
        </Title>
      </div>

      <Card
        bordered={false}
        style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.06)' }}
      >
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={tabItems}
          type="line"
          size="large"
        />
      </Card>
    </div>
  );
}
