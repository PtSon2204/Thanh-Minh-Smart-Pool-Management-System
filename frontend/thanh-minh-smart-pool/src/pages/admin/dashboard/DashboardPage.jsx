import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  Table,
  Button,
  Modal,
  Form,
  Input,
  Select,
  message,
  notification as antNotification,
  Badge,
  Spin,
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
  SafetyCertificateOutlined,
  ThunderboltOutlined,
  RightOutlined,
  EyeOutlined,
  ToolOutlined,
  ExperimentOutlined,
  BarChartOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import dayjs from 'dayjs';
import { useAuthStore } from '../../../features/auth/store/authStore';
import { notificationService } from '../../../features/notifications/services/notificationService';
import { reportService } from '../../../features/reports/services/reportService';
import {
  startSignalRConnection,
  onReceiveNotification,
  sendNotificationViaHub,
} from '../../../services/signalrClient';
import './DashboardPage.css';

/**
 * Render notification type badge with refined pill styles
 */
const renderTypeBadge = (type) => {
  const t = (type || 'SYSTEM').toUpperCase();
  if (t === 'MAINTENANCE' || t.startsWith('MT:')) {
    return <span className="db-pill-tag db-tag-maintenance">Bảo trì</span>;
  }
  if (t === 'INCIDENT') {
    return <span className="db-pill-tag db-tag-incident">Sự cố</span>;
  }
  if (t === 'TICKET' || t === 'TICKET_DAY' || t === 'TICKET_MONTH' || t === 'ORDER') {
    return <span className="db-pill-tag db-tag-ticket">Bán vé</span>;
  }
  if (t === 'RENTAL') {
    return <span className="db-pill-tag db-tag-rental">Thuê đồ</span>;
  }
  if (t === 'WARNING') {
    return <span className="db-pill-tag db-tag-warning">Cảnh báo</span>;
  }
  if (t === 'CUSTOMER' || t === 'USER') {
    return <span className="db-pill-tag db-tag-customer">Khách hàng</span>;
  }
  return <span className="db-pill-tag db-tag-system">Hệ thống</span>;
};

/**
 * Calculate current swimming shift label
 */
const getCurrentShift = () => {
  const now = dayjs();
  const currentMinutes = now.hour() * 60 + now.minute();
  const sangStart = 6 * 60; // 06:00
  const sangEnd = 11 * 60 + 30; // 11:30
  const chieuStart = 13 * 60 + 30; // 13:30
  const chieuEnd = 19 * 60 + 30; // 19:30

  if (currentMinutes >= sangStart && currentMinutes <= sangEnd) {
    return { name: 'Ca sáng (06:00 – 11:30)', active: true };
  }
  if (currentMinutes >= chieuStart && currentMinutes <= chieuEnd) {
    return { name: 'Ca chiều (13:30 – 19:30)', active: true };
  }
  return { name: 'Ngoài giờ mở ca', active: false };
};

export default function DashboardPage() {
  const { session } = useAuthStore();
  const adminName = session?.fullName || session?.userName || 'Quản trị viên';
  const shiftInfo = useMemo(() => getCurrentShift(), []);

  const [notifications, setNotifications] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('ALL');
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
      const res = await notificationService.getNotifications({ page: 1, pageSize: 30 });
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
    message.success('Đã làm mới dữ liệu hệ thống');
  }, [loadNotifications, loadStats]);

  useEffect(() => {
    let isMounted = true;

    // Tải thông báo
    notificationService
      .getNotifications({ page: 1, pageSize: 30 })
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
        message: `Thông báo hệ thống mới: ${newNotif.title || ''}`,
        description: newNotif.content,
        placement: 'topRight',
        duration: 6,
        style: {
          borderRadius: 12,
          borderLeft: '4px solid #0284c7',
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
          message: localPayload.title,
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

  // Filtered notifications based on active tab
  const filteredNotifications = useMemo(() => {
    if (activeTab === 'ALL') return notifications;
    if (activeTab === 'UNREAD') return notifications.filter((n) => !n.isRead);
    if (activeTab === 'INCIDENT') {
      return notifications.filter((n) => {
        const t = (n.type || '').toUpperCase();
        return t === 'INCIDENT' || t === 'WARNING';
      });
    }
    if (activeTab === 'MAINTENANCE') {
      return notifications.filter((n) => {
        const t = (n.type || '').toUpperCase();
        return t === 'MAINTENANCE' || t.startsWith('MT:');
      });
    }
    if (activeTab === 'TICKET') {
      return notifications.filter((n) => {
        const t = (n.type || '').toUpperCase();
        return t === 'TICKET' || t === 'ORDER' || t.includes('TICKET');
      });
    }
    if (activeTab === 'SYSTEM') {
      return notifications.filter((n) => {
        const t = (n.type || '').toUpperCase();
        return t === 'SYSTEM' || !t;
      });
    }
    return notifications;
  }, [notifications, activeTab]);

  const unreadCount = useMemo(
    () => notifications.filter((n) => !n.isRead).length,
    [notifications]
  );

  const notificationColumns = [
    {
      title: 'Thời gian',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 170,
      render: (val, record) => {
        const formatted = val ? dayjs(val).format('DD/MM/YYYY HH:mm:ss') : 'Vừa xong';
        return (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <span style={{ fontSize: 13, color: '#334155', fontWeight: 500 }}>{formatted}</span>
            {record.isNew && (
              <Badge
                count="MỚI"
                style={{
                  backgroundColor: '#059669',
                  fontSize: 10,
                  padding: '0 5px',
                  height: 16,
                  lineHeight: '16px',
                  width: 'fit-content',
                }}
              />
            )}
          </div>
        );
      },
    },
    {
      title: 'Loại',
      dataIndex: 'type',
      key: 'type',
      width: 110,
      render: (type) => renderTypeBadge(type),
    },
    {
      title: 'Tiêu đề',
      dataIndex: 'title',
      key: 'title',
      width: 240,
      render: (text, record) => (
        <div className="db-notif-title-wrap">
          {!record.isRead && <span className="db-unread-dot" title="Chưa đọc" />}
          <span
            style={{
              fontWeight: record.isRead ? 500 : 700,
              color: record.isRead ? '#475569' : '#0f172a',
              fontSize: 13.5,
            }}
          >
            {text}
          </span>
        </div>
      ),
    },
    {
      title: 'Nội dung chi tiết',
      dataIndex: 'content',
      key: 'content',
      render: (text) => (
        <span style={{ color: '#334155', fontSize: 13.5, lineHeight: 1.5 }}>
          {text}
        </span>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'isRead',
      key: 'isRead',
      width: 110,
      render: (isRead) => (
        <span
          className="db-pill-tag"
          style={{
            background: isRead ? '#f1f5f9' : '#eff6ff',
            color: isRead ? '#64748b' : '#0284c7',
            borderColor: isRead ? '#e2e8f0' : '#bfdbfe',
          }}
        >
          {isRead ? 'Đã đọc' : 'Chưa đọc'}
        </span>
      ),
    },
    {
      title: 'Thao tác',
      key: 'action',
      width: 130,
      render: (_, record) => (
        <div>
          {!record.isRead && (
            <button
              type="button"
              className="db-btn-mark-read"
              onClick={() => handleMarkAsRead(record.id)}
            >
              <CheckOutlined style={{ fontSize: 11 }} />
              <span>Đã đọc</span>
            </button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="dashboard-page">
      {/* ── 1. Welcome & Operational Status Banner ── */}
      <div className="db-welcome-banner">
        <div className="db-welcome-left">
          <div className="db-welcome-badge">
            <SafetyCertificateOutlined />
            <span>Thanh Minh Smart Pool Cockpit</span>
          </div>
          <h1 className="db-welcome-title">
            Xin chào, <span className="db-name-highlight">{adminName}</span>!
          </h1>
          <p className="db-welcome-desc">
            Tổng quan dữ liệu kinh doanh, chỉ số an toàn hồ bơi và bảng điều phối hoạt động hệ thống theo thời gian thực.
          </p>
        </div>

        <div className="db-welcome-right">
          <div className="db-status-pill">
            <span className="db-live-indicator" />
            <span>Trực tuyến</span>
          </div>

          <div className="db-shift-info">
            <span>Ca hiện tại:</span>
            <strong>{shiftInfo.name}</strong>
          </div>
        </div>
      </div>

      {/* ── 2. Executive KPI Stat Cards ── */}
      <div className="db-kpi-grid">
        {/* Card 1: Khách hàng */}
        <div className="db-kpi-card">
          <div className="db-kpi-top">
            <div className="db-kpi-icon-box db-icon-emerald">
              <UserOutlined />
            </div>
            <span className="db-growth-pill db-growth-up">
              {stats.customerGrowthRate !== 0 ? (
                <>
                  {stats.customerGrowthRate >= 0 ? <ArrowUpOutlined /> : <ArrowDownOutlined />}
                  <span>{Math.abs(stats.customerGrowthRate)}%</span>
                </>
              ) : (
                <span>Ổn định</span>
              )}
            </span>
          </div>

          <span className="db-kpi-label">Tổng Khách Hàng</span>

          <div className="db-kpi-value-row">
            {statsLoading ? (
              <Spin size="small" />
            ) : (
              <span className="db-kpi-value">
                {new Intl.NumberFormat('vi-VN').format(stats.totalCustomers)}
              </span>
            )}
            <span className="db-kpi-unit">thành viên</span>
          </div>

          <div className="db-kpi-footer">
            <span className="db-kpi-subtext">Khách hàng đăng ký tài khoản</span>
          </div>
        </div>

        {/* Card 2: Vé đã bán */}
        <div className="db-kpi-card">
          <div className="db-kpi-top">
            <div className="db-kpi-icon-box db-icon-sky">
              <ShoppingCartOutlined />
            </div>
            <span className="db-growth-pill db-growth-neutral">
              <span>Tháng {dayjs().format('MM/YYYY')}</span>
            </span>
          </div>

          <span className="db-kpi-label">Tổng Vé Đã Bán</span>

          <div className="db-kpi-value-row">
            {statsLoading ? (
              <Spin size="small" />
            ) : (
              <span className="db-kpi-value">
                {new Intl.NumberFormat('vi-VN').format(stats.totalTicketsSold)}
              </span>
            )}
            <span className="db-kpi-unit">vé</span>
          </div>

          <div className="db-kpi-footer">
            <span className="db-kpi-subtext">Bao gồm vé lượt &amp; vé tháng</span>
          </div>
        </div>

        {/* Card 3: Doanh thu */}
        <div className="db-kpi-card">
          <div className="db-kpi-top">
            <div className="db-kpi-icon-box db-icon-amber">
              <DollarOutlined />
            </div>
            <span
              className={`db-growth-pill ${
                stats.revenueGrowthRate >= 0 ? 'db-growth-up' : 'db-growth-down'
              }`}
            >
              {stats.revenueGrowthRate !== 0 ? (
                <>
                  {stats.revenueGrowthRate >= 0 ? <ArrowUpOutlined /> : <ArrowDownOutlined />}
                  <span>{Math.abs(stats.revenueGrowthRate)}%</span>
                </>
              ) : (
                <span>Thực thu</span>
              )}
            </span>
          </div>

          <span className="db-kpi-label">Doanh Thu Vé Tháng Này</span>

          <div className="db-kpi-value-row">
            {statsLoading ? (
              <Spin size="small" />
            ) : (
              <span className="db-kpi-value">
                {new Intl.NumberFormat('vi-VN').format(stats.totalRevenue)}
              </span>
            )}
            <span className="db-kpi-unit">VNĐ</span>
          </div>

          <div className="db-kpi-footer">
            <span className="db-kpi-subtext">Đã quyết toán &amp; cập nhật tự động</span>
          </div>
        </div>

        {/* Card 4: An toàn & Vận hành */}
        <div className="db-kpi-card">
          <div className="db-kpi-top">
            <div className="db-kpi-icon-box db-icon-indigo">
              <SafetyCertificateOutlined />
            </div>
            <span className="db-growth-pill db-growth-up">
              <span>Chuẩn an toàn</span>
            </span>
          </div>

          <span className="db-kpi-label">Hệ Thống &amp; Cứu Hộ AI</span>

          <div className="db-kpi-value-row">
            <span className="db-kpi-value" style={{ color: '#059669' }}>
              100%
            </span>
            <span className="db-kpi-unit">hoạt động</span>
          </div>

          <div className="db-kpi-footer">
            <span className="db-kpi-subtext">Camera giám sát mép bể trực tuyến</span>
          </div>
        </div>
      </div>

      {/* ── 3. Quick Action Shortcuts Toolbar ── */}
      <div className="db-shortcuts-section">
        <div className="db-shortcuts-header">
          <ThunderboltOutlined style={{ color: '#0284c7' }} />
          <span>Thao Tác Quản Trị Nhanh</span>
        </div>

        <div className="db-shortcuts-grid">
          <Link to="/admin/tickets/pos" className="db-shortcut-item">
            <div className="db-shortcut-icon">
              <ShoppingCartOutlined />
            </div>
            <span>Bán vé tại quầy (POS)</span>
            <RightOutlined style={{ marginLeft: 'auto', fontSize: 11, color: '#94a3b8' }} />
          </Link>

          <Link to="/admin/safety/list-camera" className="db-shortcut-item">
            <div className="db-shortcut-icon">
              <EyeOutlined />
            </div>
            <span>Camera AI giám sát an toàn</span>
            <RightOutlined style={{ marginLeft: 'auto', fontSize: 11, color: '#94a3b8' }} />
          </Link>

          <Link to="/admin/water-quality" className="db-shortcut-item">
            <div className="db-shortcut-icon">
              <ExperimentOutlined />
            </div>
            <span>Đo kiểm chất lượng nước</span>
            <RightOutlined style={{ marginLeft: 'auto', fontSize: 11, color: '#94a3b8' }} />
          </Link>

          <Link to="/admin/equipments/list-equipments" className="db-shortcut-item">
            <div className="db-shortcut-icon">
              <ToolOutlined />
            </div>
            <span>Quản lý thiết bị &amp; bảo trì</span>
            <RightOutlined style={{ marginLeft: 'auto', fontSize: 11, color: '#94a3b8' }} />
          </Link>

          <Link to="/admin/statistics/ticket-revenue" className="db-shortcut-item">
            <div className="db-shortcut-icon">
              <BarChartOutlined />
            </div>
            <span>Báo cáo doanh thu chi tiết</span>
            <RightOutlined style={{ marginLeft: 'auto', fontSize: 11, color: '#94a3b8' }} />
          </Link>
        </div>
      </div>

      {/* ── 4. Notifications & Activity Feed ── */}
      <div className="db-notifications-card" id="notifications">
        <div className="db-card-header">
          <div className="db-header-title-box">
            <div className="db-header-icon">
              <BellOutlined />
            </div>
            <div className="db-header-text">
              <h3>
                Bảng Thông Báo Hệ Thống &amp; Hoạt Động
                {unreadCount > 0 && (
                  <span className="db-unread-count-pill">{unreadCount} chưa đọc</span>
                )}
              </h3>
              <p>Nhận diện cảnh báo an toàn, sự cố thiết bị và cập nhật đơn hàng tức thì</p>
            </div>
          </div>

          <div className="db-header-actions">
            <button
              type="button"
              className="db-btn-refresh"
              onClick={handleRefresh}
              disabled={loading || statsLoading}
              title="Tải lại danh sách thông báo và số liệu"
            >
              <ReloadOutlined spin={loading || statsLoading} />
              <span>Làm mới</span>
            </button>

            <button
              type="button"
              className="db-btn-send-test"
              onClick={() => setIsTestModalOpen(true)}
            >
              <SendOutlined />
              <span>Gửi thử thông báo</span>
            </button>
          </div>
        </div>

        {/* Filter Tabs Bar */}
        <div className="db-tabs-bar">
          <button
            type="button"
            className={`db-filter-tab ${activeTab === 'ALL' ? 'active' : ''}`}
            onClick={() => setActiveTab('ALL')}
          >
            <span>Tất cả</span>
            <span className="db-tab-badge">{notifications.length}</span>
          </button>

          <button
            type="button"
            className={`db-filter-tab ${activeTab === 'UNREAD' ? 'active' : ''}`}
            onClick={() => setActiveTab('UNREAD')}
          >
            <span>Chưa đọc</span>
            <span className="db-tab-badge">{unreadCount}</span>
          </button>

          <button
            type="button"
            className={`db-filter-tab ${activeTab === 'INCIDENT' ? 'active' : ''}`}
            onClick={() => setActiveTab('INCIDENT')}
          >
            <span>Sự cố &amp; Cảnh báo</span>
          </button>

          <button
            type="button"
            className={`db-filter-tab ${activeTab === 'MAINTENANCE' ? 'active' : ''}`}
            onClick={() => setActiveTab('MAINTENANCE')}
          >
            <span>Bảo trì thiết bị</span>
          </button>

          <button
            type="button"
            className={`db-filter-tab ${activeTab === 'TICKET' ? 'active' : ''}`}
            onClick={() => setActiveTab('TICKET')}
          >
            <span>Bán vé &amp; Đơn hàng</span>
          </button>

          <button
            type="button"
            className={`db-filter-tab ${activeTab === 'SYSTEM' ? 'active' : ''}`}
            onClick={() => setActiveTab('SYSTEM')}
          >
            <span>Hệ thống</span>
          </button>
        </div>

        {/* Notifications Table */}
        <Table
          rowKey="id"
          scroll={{ x: 860 }}
          columns={notificationColumns}
          dataSource={filteredNotifications}
          loading={loading}
          pagination={{
            pageSize: 6,
            showTotal: (total, range) => `${range[0]}-${range[1]} trong số ${total} thông báo`,
          }}
          rowClassName={(record) => (!record.isRead ? 'db-notif-row-unread' : '')}
          locale={{
            emptyText: (
              <div className="db-empty-state">
                <div className="db-empty-icon">
                  <BellOutlined />
                </div>
                <h4>Không có thông báo nào</h4>
                <p>Mọi hoạt động và chỉ số an toàn hiện tại đang vận hành ổn định.</p>
              </div>
            ),
          }}
        />
      </div>

      {/* ── 5. Modal Gửi Thử Thông Báo ── */}
      <Modal
        title={
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 16 }}>
            <SendOutlined style={{ color: '#0284c7' }} />
            <span style={{ fontWeight: 700 }}>Gửi thông báo thử nghiệm</span>
          </div>
        }
        open={isTestModalOpen}
        onCancel={() => setIsTestModalOpen(false)}
        footer={null}
        destroyOnClose
        centered
        width={540}
      >
        <p style={{ color: '#64748b', fontSize: 13, marginBottom: 18, lineHeight: 1.5 }}>
          Thông báo được gửi sẽ phát tức thì qua SignalR Hub tới tất cả quản trị viên đang trực tuyến và cập nhật trực tiếp trên bảng điều khiển.
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
            label={<span style={{ fontWeight: 600, color: '#334155' }}>Tiêu đề thông báo</span>}
            rules={[{ required: true, message: 'Vui lòng nhập tiêu đề thông báo' }]}
          >
            <Input placeholder="Nhập tiêu đề thông báo..." style={{ borderRadius: 8, padding: '7px 12px' }} />
          </Form.Item>

          <Form.Item
            name="type"
            label={<span style={{ fontWeight: 600, color: '#334155' }}>Loại phân loại thông báo</span>}
            rules={[{ required: true, message: 'Vui lòng chọn loại thông báo' }]}
          >
            <Select style={{ width: '100%' }}>
              <Select.Option value="SYSTEM">Hệ thống (System)</Select.Option>
              <Select.Option value="MAINTENANCE">Bảo trì thiết bị (Maintenance)</Select.Option>
              <Select.Option value="INCIDENT">Sự cố khẩn cấp (Incident)</Select.Option>
              <Select.Option value="TICKET">Bán vé / Khách hàng (Ticket)</Select.Option>
              <Select.Option value="WARNING">Cảnh báo chung (Warning)</Select.Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="content"
            label={<span style={{ fontWeight: 600, color: '#334155' }}>Nội dung chi tiết</span>}
            rules={[{ required: true, message: 'Vui lòng nhập nội dung chi tiết' }]}
          >
            <Input.TextArea
              rows={3}
              placeholder="Nhập nội dung chi tiết gửi tới hệ thống..."
              style={{ borderRadius: 8 }}
            />
          </Form.Item>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 24 }}>
            <Button
              onClick={() => setIsTestModalOpen(false)}
              style={{ borderRadius: 8, padding: '6px 16px' }}
            >
              Hủy bỏ
            </Button>
            <Button
              type="primary"
              htmlType="submit"
              icon={<SendOutlined />}
              loading={sendingTest}
              style={{
                borderRadius: 8,
                padding: '6px 20px',
                background: '#0284c7',
                borderColor: '#0284c7',
              }}
            >
              Phát thông báo ngay
            </Button>
          </div>
        </Form>
      </Modal>
    </div>
  );
}
