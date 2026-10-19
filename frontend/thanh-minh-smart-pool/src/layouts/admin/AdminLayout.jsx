import { useState, useEffect } from 'react';
import { Layout, Menu, Button, Avatar, Dropdown, Badge, message } from 'antd';
import {
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  DashboardOutlined,
  UserOutlined,
  SafetyCertificateOutlined,
  TeamOutlined,
  IdcardOutlined,
  TagOutlined,
  ToolOutlined,
  AppstoreOutlined,
  TransactionOutlined,
  BarChartOutlined,
  BellOutlined,
  LogoutOutlined,
  LockOutlined,
} from '@ant-design/icons';
import { Outlet, Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../features/auth/store/authStore';
import { notificationService } from '../../features/notifications/services/notificationService';
import authService from '../../features/auth/services/authService';
import './AdminLayout.css';

const { Header, Sider, Content } = Layout;

function openKeysForPath(pathname) {
  if (pathname.startsWith('/admin/staff/attendance')) return ['staff', 'staff-attendance'];
  if (pathname.startsWith('/admin/staff/')) return ['staff'];
  if (pathname.startsWith('/admin/services') || pathname.startsWith('/admin/inventory') || pathname.startsWith('/admin/rentals')) return ['services'];
  if (pathname.startsWith('/admin/payments')) return ['payments'];
  if (pathname.startsWith('/admin/statistics')) return ['statistics'];
  return [];
}

export default function AdminLayout() {
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);

  useEffect(() => {
    const fetchUnread = async () => {
      try {
        const res = await notificationService.getNotifications({ page: 1, pageSize: 1 });
        if (res && typeof res.unreadCount === 'number') {
          setUnreadCount(res.unreadCount);
        }
      } catch {
        // Ignored in layout if fails or not logged in
      }
    };
    fetchUnread();
    const interval = setInterval(fetchUnread, 30000);
    return () => clearInterval(interval);
  }, [location.pathname]);

  const { session } = useAuthStore();
  const navigate = useNavigate();
  const role = session?.role?.toUpperCase();
  const isAdmin = role === 'ADMIN';

  const roleLabelMap = {
    ADMIN: 'Quản trị viên',
    LIFEGUARD: 'Nhân viên cứu hộ',
    SALES: 'Nhân viên bán hàng',
  };
  const roleDisplay = roleLabelMap[role] || (isAdmin ? 'Quản trị viên' : 'Nhân viên');

  const menuItems = [
    {
      key: '/admin',
      icon: <DashboardOutlined />,
      label: <Link to="/admin">Bảng điều khiển</Link>,
    },
    isAdmin && {
      key: '/admin/users',
      icon: <UserOutlined />,
      label: <Link to="/admin/users">Quản lý người dùng</Link>,
    },
    isAdmin && {
      key: '/admin/roles/permissions',
      icon: <SafetyCertificateOutlined />,
      label: <Link to="/admin/roles/permissions">Phân quyền vai trò</Link>,
    },
    {
      key: 'staff',
      icon: <TeamOutlined />,
      label: 'Quản lý nhân viên',
      children: [
        {
          key: 'staff-attendance',
          label: isAdmin ? 'Chấm công nhân viên' : 'Check-in / Check-out',
          children: [
            { key: '/admin/staff/attendance', label: <Link to="/admin/staff/attendance">Check-in / Check-out</Link> },
            isAdmin && { key: '/admin/staff/attendance/qr', label: <Link to="/admin/staff/attendance/qr">Chấm công QR</Link> },
          ].filter(Boolean),
        },
        { key: '/admin/staff/shifts', label: <Link to="/admin/staff/shifts">Ca làm việc</Link> },
        { key: '/admin/staff/schedule', label: <Link to="/admin/staff/schedule">Lịch làm việc</Link> },
        { key: '/admin/staff/profiles', label: <Link to="/admin/staff/profiles">Hồ sơ nhân viên</Link> },
      ].filter(Boolean),
    },
    (isAdmin || role === 'SALES') && {
      key: '/admin/tickets',
      icon: <IdcardOutlined />,
      label: 'Quản lý vé',
      children: [
        { key: '/admin/tickets/scanQR', label: <Link to="/admin/tickets/scanQR">Quét QR vào cổng</Link> },
        { key: '/admin/tickets/types', label: <Link to="/admin/tickets/types">Loại vé</Link> },
        { key: '/admin/tickets/renewals', label: <Link to="/admin/tickets/renewals">Gia hạn vé</Link> },
        { key: '/admin/tickets/pos', label: <Link to="/admin/tickets/pos">Bán vé tại quầy (POS)</Link> },
      ],
    },
    (isAdmin || role === 'SALES') && {
      key: '/admin/coupons',
      icon: <TagOutlined />,
      label: <Link to="/admin/coupons">Quản lý mã giảm giá</Link>,
    },
    isAdmin && {
      key: '/admin/equipment',
      icon: <ToolOutlined />,
      label: <Link to="/admin/equipment">Quản lý thiết bị</Link>,
    },
    (isAdmin || role === 'SALES') && {
      key: 'services',
      className: 'admin-service-menu',
      icon: <AppstoreOutlined />,
      label: 'Quản lý dịch vụ',
      children: [
        { key: '/admin/services', label: <Link to="/admin/services">Danh mục dịch vụ</Link> },
        { key: '/admin/inventory', label: <Link to="/admin/inventory">Tồn kho và lịch sử</Link> },
        { key: '/admin/inventory/receipts', label: <Link to="/admin/inventory/receipts">Phiếu nhập hàng</Link> },
        { key: '/admin/rentals', label: <Link to="/admin/rentals">Giao dịch cho thuê</Link> },
      ],
    },
    (isAdmin || role === 'SALES') && {
      key: 'payments',
      icon: <TransactionOutlined />,
      label: 'Thanh toán và giao dịch',
      children: [
        { key: '/admin/payments/invoices', label: <Link to="/admin/payments/invoices">Lịch sử thanh toán</Link> },
      ],
    },
    isAdmin && {
      key: 'statistics',
      icon: <BarChartOutlined />,
      label: 'Thống kê',
      children: [
        { key: '/admin/statistics/ticket-revenue', label: <Link to="/admin/statistics/ticket-revenue">Thống kê số tiền vé</Link> },
        { key: '/admin/statistics/monthly-tickets', label: <Link to="/admin/statistics/monthly-tickets">Thống kê vé tháng</Link> },
        { key: '/admin/statistics/inventory-costs', label: <Link to="/admin/statistics/inventory-costs">Thống kê số tiền nhập hàng</Link> },
        { key: '/admin/statistics/sales-revenue', label: <Link to="/admin/statistics/sales-revenue">Thống kê doanh thu bán hàng</Link> },
        { key: '/admin/statistics/user-count', label: <Link to="/admin/statistics/user-count">Thống kê số lượng người dùng</Link> },
        { key: '/admin/statistics/incidents', label: <Link to="/admin/statistics/incidents">Thống kê sự cố</Link> },
        { key: '/admin/statistics/operations-costs', label: <Link to="/admin/statistics/operations-costs">Thống kê chi phí vận hành, bảo trì</Link> },
      ],
    },
    {
      key: '/admin/notifications',
      icon: <BellOutlined />,
      label: <Link to="/admin/notifications">Thông báo hệ thống</Link>,
    },
  ].filter(Boolean);

  const handleLogout = async () => {
    try {
      await authService.logout();
    } catch {
      message.warning('Không thể xác nhận đăng xuất với máy chủ. Phiên trên thiết bị đã được xóa.');
    }
    navigate('/dang-nhap', { replace: true });
  };

  const handleUserMenuClick = ({ key }) => {
    if (key === 'profile') {
      navigate('/ho-so');
    } else if (key === 'change-password') {
      navigate('/admin/doi-mat-khau');
    } else if (key === 'logout') {
      void handleLogout();
    }
  };

  const userMenu = {
    items: [
      {
        key: 'profile',
        icon: <UserOutlined />,
        label: 'Hồ sơ cá nhân',
      },
      {
        key: 'change-password',
        icon: <LockOutlined />,
        label: 'Đổi mật khẩu',
      },
      {
        type: 'divider',
      },
      {
        key: 'logout',
        icon: <LogoutOutlined />,
        label: 'Đăng xuất',
        danger: true,
      },
    ],
    onClick: handleUserMenuClick,
  };

  return (
    <Layout style={{ minHeight: '100vh', background: '#e2e8f0' }}>
      <Sider trigger={null} collapsible collapsed={collapsed} breakpoint="md" onBreakpoint={setCollapsed} width={250} className="admin-sidebar">
        <div className="bubbles-container">
          <div className="bubble"></div>
          <div className="bubble"></div>
          <div className="bubble"></div>
          <div className="bubble"></div>
          <div className="bubble"></div>
          <div className="bubble"></div>
          <div className="bubble"></div>
        </div>
        <div
          className="admin-logo"
          style={{
            height: 64,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: collapsed ? '1rem' : '1.25rem',
            fontWeight: 'bold',
          }}
        >
          {collapsed ? 'TM' : 'Thanh Minh Admin'}
        </div>
        <Menu
          mode="inline"
          selectedKeys={[location.pathname]}
          defaultOpenKeys={openKeysForPath(location.pathname)}
          items={menuItems}
          style={{ 
            borderRight: 0, 
            marginTop: 16, 
            position: 'relative', 
            zIndex: 10,
            overflowY: 'auto',
            height: 'calc(100vh - 80px)'
          }}
        />
      </Sider>
      <Layout style={{ background: '#e2e8f0', minWidth: 0 }}>
        <Header
          style={{
            padding: '0 24px',
            background: '#ffffff',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            borderBottom: '1px solid #cbd5e1',
            boxShadow: '0 1px 4px rgba(0, 21, 41, 0.04)',
            zIndex: 1,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center' }}>
            <Button
              type="text"
              aria-label={collapsed ? 'Mở menu quản trị' : 'Thu gọn menu quản trị'}
              icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
              onClick={() => setCollapsed(!collapsed)}
              style={{
                fontSize: '16px',
                width: 64,
                height: 64,
                marginLeft: -24,
              }}
            />
          </div>
          
          <div style={{ display: 'flex', alignItems: 'center', gap: 24 }}>
            <Link to="/admin/notifications" style={{ display: 'inline-flex' }}>
              <Badge count={unreadCount} size="small" overflowCount={99}>
                <Button type="text" shape="circle" icon={<BellOutlined style={{ fontSize: 18 }} />} />
              </Badge>
            </Link>
            <Dropdown menu={userMenu} placement="bottomRight">
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
                <Avatar style={{ backgroundColor: '#005f8e' }} icon={<UserOutlined />} />
                <span style={{ fontWeight: 600 }}>{session?.username ? `${session.username} (${roleDisplay})` : roleDisplay}</span>
              </div>
            </Dropdown>
          </div>
        </Header>
        <Content
          className="admin-content"
          style={{
            minHeight: 280,
          }}
        >
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
