import { useState, useEffect, useCallback } from 'react';
import {
  Card,
  Row,
  Col,
  Statistic,
  Table,
  Tag,
  Button,
  Space,
  Modal,
  Form,
  Input,
  Select,
  message,
  notification as antNotification,
  Badge,
} from 'antd';
import {
  ArrowUpOutlined,
  ArrowDownOutlined,
  UserOutlined,
  ShoppingCartOutlined,
  DollarOutlined,
  BellOutlined,
  ReloadOutlined,
  CheckOutlined,
  SendOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { notificationService } from '../../../features/notifications/services/notificationService';
import { reportService } from '../../../features/reports/services/reportService';
import {
  startSignalRConnection,
  onReceiveNotification,
  sendNotificationViaHub,
} from '../../../services/signalrClient';

const renderTypeTag = (type) => {
  const t = (type || 'SYSTEM').toUpperCase();
  if (t === 'MAINTENANCE' || t.startsWith('MT:')) {
    return <Tag color="orange">Bảo trì</Tag>;
  }
  if (t === 'INCIDENT') {
    return <Tag color="red">Sự cố</Tag>;
  }
  if (t === 'TICKET' || t === 'TICKET_DAY' || t === 'TICKET_MONTH' || t === 'ORDER') {
    return <Tag color="green">Bán vé</Tag>;
  }
  if (t === 'RENTAL') {
    return <Tag color="purple">Thuê đồ</Tag>;
  }
  if (t === 'WARNING') {
    return <Tag color="gold">Cảnh báo</Tag>;
  }
  if (t === 'CUSTOMER' || t === 'USER') {
    return <Tag color="cyan">Khách hàng</Tag>;
  }
  return <Tag color="blue">Hệ thống</Tag>;
};

export default function DashboardPage() {
  const [notifications, setNotifications] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isTestModalOpen, setIsTestModalOpen] = useState(false);
  const [sendingTest, setSendingTest] = useState(false);
  const [testForm] = Form.useForm();

  // Dữ liệu thống kê thực tế từ API Reports
  const [stats, setStats] = useState({
    totalCustomers: 0,
    customerGrowthRate: 0,
    totalTicketsSold: 0,
    totalRevenue: 0,
    revenueGrowthRate: 0,
  });
  const [statsLoading, setStatsLoading] = useState(true);

  const loadNotifications = useCallback(async () => {
    setLoading(true);
    try {
      const res = await notificationService.getNotifications({ page: 1, pageSize: 20 });
      setNotifications(res?.items || []);
    } catch {
      setNotifications([]);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadStats = useCallback(async () => {
    setStatsLoading(true);
    try {
      const [custRes, revRes] = await Promise.allSettled([
        reportService.getCustomerStatistics({ period: 'month' }),
        reportService.getTicketRevenueStatistics({ period: 'month' }),
      ]);
      const custData = custRes.status === 'fulfilled' ? custRes.value : null;
      const revData = revRes.status === 'fulfilled' ? revRes.value : null;

      setStats({
        totalCustomers: custData?.totalCustomers ?? 0,
        customerGrowthRate: custData?.growthRate ?? 0,
        totalTicketsSold: revData?.totalTicketsSold ?? 0,
        totalRevenue: revData?.totalRevenue ?? 0,
        revenueGrowthRate: revData?.revenueGrowthRate ?? 0,
      });
    } finally {
      setStatsLoading(false);
    }
  }, []);

  const handleRefresh = useCallback(() => {
    loadNotifications();
    loadStats();
  }, [loadNotifications, loadStats]);

  useEffect(() => {
    let isMounted = true;

    // Tải thông báo
    notificationService.getNotifications({ page: 1, pageSize: 20 })
      .then((res) => {
        if (!isMounted) return;
        setNotifications(res?.items || []);
      })
      .catch(() => {
        if (isMounted) setNotifications([]);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    // Tải số liệu thống kê thật từ database
    Promise.allSettled([
      reportService.getCustomerStatistics({ period: 'month' }),
      reportService.getTicketRevenueStatistics({ period: 'month' }),
    ]).then(([custRes, revRes]) => {
      if (!isMounted) return;
      const custData = custRes.status === 'fulfilled' ? custRes.value : null;
      const revData = revRes.status === 'fulfilled' ? revRes.value : null;

      setStats({
        totalCustomers: custData?.totalCustomers ?? 0,
        customerGrowthRate: custData?.growthRate ?? 0,
        totalTicketsSold: revData?.totalTicketsSold ?? 0,
        totalRevenue: revData?.totalRevenue ?? 0,
        revenueGrowthRate: revData?.revenueGrowthRate ?? 0,
      });
      setStatsLoading(false);
    });

    // Khởi động kết nối SignalR Hub ngầm
    startSignalRConnection();

    const unsubNotif = onReceiveNotification((newNotif) => {
      if (!isMounted) return;

      // Hiển thị thông báo popup Ant Design góc phải màn hình
      antNotification.info({
        message: `🔔 ${newNotif.title || 'Thông báo hệ thống mới'}`,
        description: newNotif.content,
        placement: 'topRight',
        duration: 6,
        style: {
          borderRadius: 8,
          borderLeft: '4px solid #005f8e',
        },
      });

      // Cập nhật ngay lập tức vào bảng thông báo
      setNotifications((prev) => {
        const exists = prev.some((item) => item.id === newNotif.id);
        if (exists) return prev;
        return [
          {
            id: newNotif.id || `rt-${Date.now()}`,
            title: newNotif.title,
            content: newNotif.content,
            type: newNotif.type || 'SYSTEM',
            isRead: false,
            createdAt: newNotif.createdAt || new Date().toISOString(),
            isNew: true,
          },
          ...prev,
        ];
      });

      // Nếu có sự kiện bán vé hoặc đơn hàng mới, cập nhật lại số liệu thống kê
      const t = (newNotif.type || '').toUpperCase();
      if (t === 'TICKET' || t === 'ORDER' || t.includes('TICKET')) {
        reportService.getTicketRevenueStatistics({ period: 'month' }).then((res) => {
          if (!isMounted || !res) return;
          setStats((prev) => ({
            ...prev,
            totalTicketsSold: res.totalTicketsSold ?? prev.totalTicketsSold,
            totalRevenue: res.totalRevenue ?? prev.totalRevenue,
            revenueGrowthRate: res.revenueGrowthRate ?? prev.revenueGrowthRate,
          }));
        });
      }

      // Nếu có khách hàng mới đăng ký, tự động cập nhật số liệu tổng khách hàng
      if (t === 'CUSTOMER' || t === 'USER' || t.includes('CUSTOMER')) {
        reportService.getCustomerStatistics({ period: 'month' }).then((res) => {
          if (!isMounted || !res) return;
          setStats((prev) => ({
            ...prev,
            totalCustomers: res.totalCustomers ?? prev.totalCustomers,
            customerGrowthRate: res.growthRate ?? prev.growthRate,
          }));
        });
      }
    });

    return () => {
      isMounted = false;
      unsubNotif();
    };
  }, []);

  const handleMarkAsRead = async (id) => {
    try {
      await notificationService.markAsRead(id);
      message.success('Đã đánh dấu là đã đọc');
      setNotifications((prev) =>
        prev.map((item) => (item.id === id ? { ...item, isRead: true } : item))
      );
    } catch (error) {
      message.error(error?.message || 'Không thể cập nhật trạng thái');
    }
  };

  const handleSendTestNotification = async (values) => {
    setSendingTest(true);
    try {
      let sent = false;
      try {
        await notificationService.createNotification({
          title: values.title,
          content: values.content,
          type: values.type,
        });
        sent = true;
      } catch {
        sent = await sendNotificationViaHub(values.title, values.content, values.type);
      }

      if (!sent) {
        const localPayload = {
          id: `test-${Date.now()}`,
          title: values.title,
          content: values.content,
          type: values.type,
          isRead: false,
          createdAt: new Date().toISOString(),
          isNew: true,
        };
        setNotifications((prev) => [localPayload, ...prev]);
        antNotification.info({
          message: `🔔 ${localPayload.title}`,
          description: localPayload.content,
          placement: 'topRight',
          duration: 5,
        });
      }

      message.success('Đã gửi thông báo thành công!');
      setIsTestModalOpen(false);
      testForm.resetFields();
    } catch (err) {
      message.error(`Lỗi gửi thông báo: ${err.message}`);
    } finally {
      setSendingTest(false);
    }
  };

  const notificationColumns = [
    {
      title: 'Thời gian',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 170,
      render: (val, record) => {
        const formatted = val ? dayjs(val).format('DD/MM/YYYY HH:mm:ss') : 'Vừa xong';
        return (
          <Space orientation="vertical" size={2}>
            <span style={{ fontSize: 13, color: '#334155' }}>{formatted}</span>
            {record.isNew && (
              <Badge count="MỚI" style={{ backgroundColor: '#10b981', fontSize: 10, padding: '0 4px', height: 16, lineHeight: '16px' }} />
            )}
          </Space>
        );
      },
    },
    {
      title: 'Loại',
      dataIndex: 'type',
      key: 'type',
      width: 110,
      render: (type) => renderTypeTag(type),
    },
    {
      title: 'Tiêu đề',
      dataIndex: 'title',
      key: 'title',
      width: 220,
      render: (text, record) => (
        <span style={{ fontWeight: record.isRead ? 500 : 700, color: record.isRead ? '#475569' : '#0f172a' }}>
          {!record.isRead && <Badge status="processing" style={{ marginRight: 6 }} />}
          {text}
        </span>
      ),
    },
    {
      title: 'Nội dung chi tiết',
      dataIndex: 'content',
      key: 'content',
      render: (text) => <span style={{ color: '#334155' }}>{text}</span>,
    },
    {
      title: 'Trạng thái',
      dataIndex: 'isRead',
      key: 'isRead',
      width: 110,
      render: (isRead) => (
        <Tag color={isRead ? 'default' : 'processing'}>
          {isRead ? 'Đã đọc' : 'Chưa đọc'}
        </Tag>
      ),
    },
    {
      title: 'Thao tác',
      key: 'action',
      width: 140,
      render: (_, record) => (
        <Space size="small">
          {!record.isRead && (
            <Button
              type="link"
              size="small"
              icon={<CheckOutlined />}
              onClick={() => handleMarkAsRead(record.id)}
            >
              Đã đọc
            </Button>
          )}
        </Space>
      ),
    },
  ];

  return (
    <div>
      {/* Welcome Banner */}
      <div
        style={{
          background: 'linear-gradient(90deg, #005f8e 0%, #00b4d8 100%)',
          padding: '20px 24px',
          borderRadius: '12px',
          color: '#fff',
          marginBottom: '24px',
          boxShadow: '0 4px 12px rgba(0, 119, 182, 0.15)',
        }}
      >
        <h2 style={{ margin: 0, color: '#fff', fontSize: '20px' }}>Xin chào, Quản trị viên! 👋</h2>
        <p style={{ margin: '4px 0 0 0', opacity: 0.9 }}>
          Dưới đây là tổng quan tình hình kinh doanh và bảng thông báo hoạt động của hệ thống.
        </p>
      </div>

      {/* Metric Cards với dữ liệu thật */}
      <Row gutter={[24, 24]}>
        <Col xs={24} sm={12} lg={8}>
          <Card bordered={false} loading={statsLoading} style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <Statistic
              title="Tổng Khách Hàng"
              value={stats.totalCustomers}
              precision={0}
              valueStyle={{ color: '#3f8600', fontWeight: 600 }}
              prefix={<UserOutlined style={{ marginRight: 8 }} />}
              suffix={
                stats.customerGrowthRate !== 0 ? (
                  <small style={{ fontSize: '14px', marginLeft: 8, color: stats.customerGrowthRate >= 0 ? '#3f8600' : '#cf1322' }}>
                    {stats.customerGrowthRate >= 0 ? <ArrowUpOutlined /> : <ArrowDownOutlined />} {Math.abs(stats.customerGrowthRate)}%
                  </small>
                ) : null
              }
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card bordered={false} loading={statsLoading} style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <Statistic
              title="Tổng Vé Đã Bán (Tháng Này)"
              value={stats.totalTicketsSold}
              precision={0}
              valueStyle={{ color: '#1677ff', fontWeight: 600 }}
              prefix={<ShoppingCartOutlined style={{ marginRight: 8 }} />}
              suffix={<span style={{ fontSize: 14, color: '#64748b', marginLeft: 6 }}>vé</span>}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card bordered={false} loading={statsLoading} style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <Statistic
              title="Doanh Thu Bán Vé (Tháng Này)"
              value={stats.totalRevenue}
              precision={0}
              valueStyle={{ color: '#faad14', fontWeight: 600 }}
              prefix={<DollarOutlined style={{ marginRight: 8 }} />}
              suffix={
                <Space size={6} style={{ marginLeft: 6 }}>
                  <span style={{ fontSize: 16 }}>VNĐ</span>
                  {stats.revenueGrowthRate !== 0 && (
                    <small style={{ fontSize: '13px', color: stats.revenueGrowthRate >= 0 ? '#3f8600' : '#cf1322' }}>
                      {stats.revenueGrowthRate >= 0 ? <ArrowUpOutlined /> : <ArrowDownOutlined />} {Math.abs(stats.revenueGrowthRate)}%
                    </small>
                  )}
                </Space>
              }
            />
          </Card>
        </Col>
      </Row>

      {/* Notifications Section */}
      <Card
        id="notifications"
        title={
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <BellOutlined style={{ fontSize: 20, color: '#005f8e' }} />
            <span style={{ fontSize: '18px', fontWeight: 600, color: '#0f172a' }}>
              Bảng Thông Báo Hệ Thống & Hoạt Động
            </span>
          </div>
        }
        extra={
          <Space>
            <Button
              type="primary"
              icon={<SendOutlined />}
              onClick={() => setIsTestModalOpen(true)}
              style={{ backgroundColor: '#005f8e' }}
            >
              Gửi thử thông báo
            </Button>
            <Button
              icon={<ReloadOutlined />}
              onClick={handleRefresh}
              loading={loading || statsLoading}
              title="Tải lại danh sách và số liệu"
            >
              Làm mới
            </Button>
          </Space>
        }
        bordered={false}
        style={{ marginTop: 24, borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
      >
        <Table
          rowKey="id"
          scroll={{ x: 860 }}
          columns={notificationColumns}
          dataSource={notifications}
          loading={loading}
          pagination={{ pageSize: 5 }}
          locale={{ emptyText: 'Chưa có thông báo nào' }}
        />
      </Card>

      {/* Modal gửi thử thông báo */}
      <Modal
        title={
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <SendOutlined style={{ color: '#005f8e' }} />
            <span>Gửi thông báo thử nghiệm</span>
          </div>
        }
        open={isTestModalOpen}
        onCancel={() => setIsTestModalOpen(false)}
        footer={null}
        destroyOnClose
      >
        <p style={{ color: '#64748b', fontSize: 13, marginBottom: 16 }}>
          Nhập thông tin bên dưới để gửi một thông báo thử nghiệm. Thông báo sẽ xuất hiện tức thì trên bảng điều khiển.
        </p>
        <Form
          form={testForm}
          layout="vertical"
          initialValues={{
            type: 'SYSTEM',
            title: 'Cảnh báo nhiệt độ nước hồ bơi',
            content: 'Nhiệt độ nước hiện tại là 29°C, đạt điều kiện tiêu chuẩn cho khách bơi.',
          }}
          onFinish={handleSendTestNotification}
        >
          <Form.Item
            name="title"
            label="Tiêu đề thông báo"
            rules={[{ required: true, message: 'Vui lòng nhập tiêu đề' }]}
          >
            <Input placeholder="Nhập tiêu đề thông báo..." />
          </Form.Item>

          <Form.Item
            name="type"
            label="Loại thông báo"
            rules={[{ required: true, message: 'Vui lòng chọn loại thông báo' }]}
          >
            <Select>
              <Select.Option value="SYSTEM">Hệ thống</Select.Option>
              <Select.Option value="MAINTENANCE">Bảo trì thiết bị</Select.Option>
              <Select.Option value="INCIDENT">Sự cố khẩn cấp</Select.Option>
              <Select.Option value="TICKET">Bán vé / Khách hàng</Select.Option>
              <Select.Option value="WARNING">Cảnh báo</Select.Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="content"
            label="Nội dung thông báo"
            rules={[{ required: true, message: 'Vui lòng nhập nội dung' }]}
          >
            <Input.TextArea rows={3} placeholder="Nhập nội dung chi tiết thông báo..." />
          </Form.Item>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 20 }}>
            <Button onClick={() => setIsTestModalOpen(false)}>Hủy</Button>
            <Button
              type="primary"
              htmlType="submit"
              icon={<SendOutlined />}
              loading={sendingTest}
              style={{ backgroundColor: '#005f8e' }}
            >
              Gửi ngay
            </Button>
          </div>
        </Form>
      </Modal>
    </div>
  );
}
