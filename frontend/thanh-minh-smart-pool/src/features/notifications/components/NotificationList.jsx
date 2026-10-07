import { useState, useEffect } from 'react';
import { Table, Button, Space, Tag, Popconfirm, message } from 'antd';
import { CheckOutlined, ReloadOutlined } from '@ant-design/icons';
import { notificationService } from '../services/notificationService';

export default function NotificationList() {
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState([]);

  useEffect(() => {
    let ignore = false;
    notificationService.getNotifications()
      .then((res) => {
        if (!ignore) setData(res?.items || []);
      })
      .catch((error) => {
        if (!ignore) message.error(error?.message || 'Không thể tải danh sách thông báo');
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, []);

  const handleReload = async () => {
    setLoading(true);
    try {
      const res = await notificationService.getNotifications();
      setData(res?.items || []);
    } catch (error) {
      message.error(error?.message || 'Không thể tải danh sách thông báo');
    } finally {
      setLoading(false);
    }
  };

  const handleMarkAsRead = async (id) => {
    try {
      await notificationService.markAsRead(id);
      message.success('Đã đánh dấu đã đọc thông báo');
      const res = await notificationService.getNotifications();
      setData(res?.items || []);
    } catch (error) {
      message.error(error?.message || 'Thao tác thất bại');
    }
  };

  const columns = [
    {
      title: 'Mã',
      dataIndex: 'id',
      key: 'id',
      width: 90,
      render: (id) => <span style={{ fontSize: 12 }}>{id?.slice(0, 8)}...</span>,
    },
    {
      title: 'Tiêu đề',
      dataIndex: 'title',
      key: 'title',
    },
    {
      title: 'Nội dung',
      dataIndex: 'content',
      key: 'content',
    },
    {
      title: 'Trạng thái',
      dataIndex: 'isRead',
      key: 'isRead',
      width: 120,
      render: (isRead) => (
        <Tag color={isRead ? 'default' : 'processing'}>
          {isRead ? 'Đã đọc' : 'Chưa đọc'}
        </Tag>
      ),
    },
    {
      title: 'Thao tác',
      key: 'action',
      width: 160,
      render: (_, record) => (
        <Space size="middle">
          {!record.isRead && (
            <Popconfirm
              title="Đánh dấu đã đọc thông báo này?"
              onConfirm={() => handleMarkAsRead(record.id)}
              okText="Đồng ý"
              cancelText="Hủy"
            >
              <Button type="link" icon={<CheckOutlined />}>
                Đánh dấu đã đọc
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  return (
    <div style={{ background: '#fff', padding: 24, borderRadius: 8 }}>
      <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'flex-end' }}>
        <Button icon={<ReloadOutlined />} onClick={handleReload} loading={loading}>
          Làm mới
        </Button>
      </div>
      <Table
        rowKey="id"
        columns={columns}
        dataSource={data}
        loading={loading}
        pagination={{ pageSize: 10 }}
      />
    </div>
  );
}