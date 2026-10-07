import { useState, useEffect, useCallback } from 'react';
import {
  Table,
  Tag,
  Button,
  Select,
  Space,
  Badge,
  Typography,
  Tooltip,
  Empty,
  Spin,
  message as antMessage,
} from 'antd';
import {
  BellOutlined,
  CheckOutlined,
  ReloadOutlined,
  ExclamationCircleOutlined,
  ShoppingOutlined,
  CalendarOutlined,
  ToolOutlined,
  InfoCircleOutlined,
} from '@ant-design/icons';
import { notificationService } from '../services/notificationService';

const { Text } = Typography;
const { Option } = Select;

const TYPE_CONFIG = {
  ticket_day: {
    label: 'Vé ngày',
    color: 'blue',
    icon: <ShoppingOutlined />,
  },
  ticket_month: {
    label: 'Vé tháng',
    color: 'purple',
    icon: <CalendarOutlined />,
  },
  rental: {
    label: 'Cho thuê đồ',
    color: 'cyan',
    icon: <ToolOutlined />,
  },
  incident: {
    label: 'Sự cố',
    color: 'red',
    icon: <ExclamationCircleOutlined />,
  },
  system: {
    label: 'Hệ thống',
    color: 'default',
    icon: <InfoCircleOutlined />,
  },
};

export default function NotificationList() {
  const [data, setData] = useState({
    items: [],
    totalCount: 0,
    unreadCount: 0,
  });

  const [loading, setLoading] = useState(false);
  const [markingId, setMarkingId] = useState(null);

  const [filters, setFilters] = useState({
    page: 1,
    pageSize: 20,
    type: undefined,
    isRead: undefined,
  });

  const fetchData = useCallback(async () => {
    setLoading(true);

    try {
      const params = { ...filters };

      if (params.type === 'all') {
        delete params.type;
      }

      if (params.isRead === 'all') {
        delete params.isRead;
      }

      const result = await notificationService.getNotifications(params);

      setData(result);
    } catch (error) {
      console.error('Failed to fetch notifications:', error);
      antMessage.error('Không thể tải danh sách thông báo.');
    } finally {
      setLoading(false);
    }
  }, [filters]);

  // Fetch notifications whenever filters change.
  // This effect intentionally triggers state updates after an async API request.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleMarkAsRead = async (id) => {
    setMarkingId(id);

    try {
      await notificationService.markAsRead(id);

      antMessage.success('Đã đánh dấu là đã đọc.');

      await fetchData();
    } catch (error) {
      console.error('Failed to mark notification as read:', error);
      antMessage.error('Không thể cập nhật trạng thái.');
    } finally {
      setMarkingId(null);
    }
  };

  const columns = [
    {
      title: '',
      dataIndex: 'isRead',
      width: 8,
      render: (isRead) => (
        <div
          style={{
            width: 6,
            height: 6,
            borderRadius: '50%',
            background: isRead ? 'transparent' : '#1890ff',
            margin: 'auto',
          }}
        />
      ),
    },

    {
      title: 'Loại',
      dataIndex: 'type',
      width: 130,
      render: (type) => {
        const cfg = TYPE_CONFIG[type] ?? TYPE_CONFIG.system;

        return (
          <Tag color={cfg.color} icon={cfg.icon}>
            {cfg.label}
          </Tag>
        );
      },
    },

    {
      title: 'Tiêu đề',
      dataIndex: 'title',
      render: (title, record) => (
        <Text strong={!record.isRead}>
          {title}
        </Text>
      ),
    },

    {
      title: 'Nội dung',
      dataIndex: 'content',
      ellipsis: true,
      render: (content) => (
        <Text type="secondary">
          {content}
        </Text>
      ),
    },

    {
      title: 'Thời gian',
      dataIndex: 'createdAt',
      width: 160,
      render: (date) =>
        date
          ? new Date(date).toLocaleString('vi-VN')
          : '—',
    },

    {
      title: 'Thao tác',
      width: 120,
      render: (_, record) =>
        record.isRead ? (
          <Text
            type="secondary"
            style={{ fontSize: 12 }}
          >
            Đã đọc
          </Text>
        ) : (
          <Tooltip title="Đánh dấu đã đọc">
            <Button
              size="small"
              type="primary"
              ghost
              icon={<CheckOutlined />}
              loading={markingId === record.id}
              onClick={() => handleMarkAsRead(record.id)}
            >
              Đọc
            </Button>
          </Tooltip>
        ),
    },
  ];

  return (
    <div>
      {/* Toolbar */}
      <Space
        wrap
        style={{
          marginBottom: 16,
          justifyContent: 'space-between',
          width: '100%',
        }}
      >
        <Space wrap>
          <Badge
            count={data.unreadCount}
            overflowCount={99}
          >
            <BellOutlined
              style={{
                fontSize: 20,
                color: '#1890ff',
              }}
            />
          </Badge>

          <Text
            strong
            style={{
              fontSize: 15,
            }}
          >
            {data.unreadCount > 0
              ? `${data.unreadCount} thông báo chưa đọc`
              : 'Tất cả đã đọc'}
          </Text>
        </Space>

        <Space wrap>
          {/* Filter by notification type */}
          <Select
            placeholder="Loại thông báo"
            allowClear
            style={{ width: 160 }}
            onChange={(value) =>
              setFilters((currentFilters) => ({
                ...currentFilters,
                type: value,
                page: 1,
              }))
            }
          >
            {Object.entries(TYPE_CONFIG).map(
              ([key, cfg]) => (
                <Option
                  key={key}
                  value={key}
                >
                  {cfg.label}
                </Option>
              )
            )}
          </Select>

          {/* Filter by read status */}
          <Select
            placeholder="Trạng thái"
            allowClear
            style={{ width: 140 }}
            onChange={(value) =>
              setFilters((currentFilters) => ({
                ...currentFilters,
                isRead: value,
                page: 1,
              }))
            }
          >
            <Option value={false}>
              Chưa đọc
            </Option>

            <Option value={true}>
              Đã đọc
            </Option>
          </Select>

          {/* Refresh */}
          <Button
            icon={<ReloadOutlined />}
            onClick={fetchData}
            loading={loading}
          >
            Làm mới
          </Button>
        </Space>
      </Space>

      {/* Table */}
      <Spin spinning={loading}>
        <Table
          rowKey="id"
          dataSource={data.items}
          columns={columns}
          locale={{
            emptyText: (
              <Empty description="Không có thông báo nào" />
            ),
          }}
          rowClassName={(record) =>
            record.isRead
              ? ''
              : 'notification-row-unread'
          }
          expandable={{
            expandedRowRender: (record) => (
              <div
                style={{
                  padding: '12px 16px',
                  background: '#f8fafc',
                  borderRadius: 6,
                  border: '1px solid #e2e8f0',
                }}
              >
                <Text
                  strong
                  style={{
                    color: '#1e293b',
                  }}
                >
                  Nội dung chi tiết:
                </Text>

                <div
                  style={{
                    marginTop: 6,
                    color: '#475569',
                    whiteSpace: 'pre-wrap',
                    lineHeight: 1.6,
                  }}
                >
                  {record.content}
                </div>
              </div>
            ),

            rowExpandable: (record) =>
              Boolean(record.content),
          }}
          pagination={{
            current: filters.page,
            pageSize: filters.pageSize,
            total: data.totalCount,
            showSizeChanger: true,

            showTotal: (total) =>
              `Tổng ${total} thông báo`,

            onChange: (page, pageSize) =>
              setFilters((currentFilters) => ({
                ...currentFilters,
                page,
                pageSize,
              })),
          }}
        />
      </Spin>

      <style>
        {`
          .notification-row-unread td {
            background: #f0f7ff !important;
          }

          .notification-row-unread:hover td {
            background: #e0f0ff !important;
          }
        `}
      </style>
    </div>
  );
}