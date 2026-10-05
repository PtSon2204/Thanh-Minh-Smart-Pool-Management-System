import { useState } from 'react';
import { Layout, Menu, Button, Avatar, Dropdown, Badge } from 'antd';
import {
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  DashboardOutlined,
  UserOutlined,
  TeamOutlined,
  IdcardOutlined,
  TagOutlined,
  ToolOutlined,
  AppstoreOutlined,
  TransactionOutlined,
  BarChartOutlined,
  BellOutlined,
  LogoutOutlined,
} from '@ant-design/icons';
import { Outlet, Link, useLocation } from 'react-router-dom';
import './AdminLayout.css';

const { Header, Sider, Content } = Layout;

export default function AdminLayout() {
  const [collapsed, setCollapsed] = useState(false);
  const location = useLocation();

  const menuItems = [
    {
      key: '/admin',
      icon: <DashboardOutlined />,
      label: <Link to="/admin">Bảng điều khiển</Link>,
    },
    {
      key: '/admin/users',
      icon: <UserOutlined />,
      label: <Link to="/admin/users">Quản lý người dùng</Link>,
    },
    {
      key: 'staff',
      icon: <TeamOutlined />,
      label: 'Quản lý nhân viên',
      children: [
        { key: '/admin/staff/attendance', label: <Link to="/admin/staff/attendance">Chấm công hàng ngày</Link> },
        { key: '/admin/staff/schedule', label: <Link to="/admin/staff/schedule">Xem lịch và ca làm việc</Link> },
        { key: '/admin/staff/profiles', label: <Link to="/admin/staff/profiles">Hồ sơ nhân viên</Link> },
      ],
    },
    {
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
    {
      key: '/admin/coupons',
      icon: <TagOutlined />,
      label: <Link to="/admin/coupons">Quản lý mã giảm giá</Link>,
    },
    {
      key: '/admin/equipment',
      icon: <ToolOutlined />,
      label: <Link to="/admin/equipment">Quản lý thiết bị</Link>,
    },
    {
      key: 'services',
      className: 'admin-service-menu',
      icon: <AppstoreOutlined />,
      label: 'Quản lý dịch vụ',
      children: [
        { key: '/admin/services', label: <Link to="/admin/services">Danh mục dịch vụ</Link> },
        { key: '/admin/inventory', label: <Link to="/admin/inventory">Tồn kho và lịch sử</Link> },
        { key: '/admin/rentals', label: <Link to="/admin/rentals">Giao dịch cho thuê</Link> },
      ],
    },
    {
      key: 'payments',
      icon: <TransactionOutlined />,
      label: 'Thanh toán và giao dịch',
      children: [
        { key: '/admin/payments/invoices', label: <Link to="/admin/payments/invoices">Lịch sử thanh toán</Link> },
      ],
    },
    {
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
  ];

  const userMenu = {
    items: [
      {
        key: 'profile',
        icon: <UserOutlined />,
        label: 'Hồ sơ cá nhân',
      },
      {
        type: 'divider',
      },
      {
        key: 'logout',
        icon: <LogoutOutlined />,
        label: 'Đăng xuất',
      },
    ],
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
          defaultOpenKeys={['/admin/services', '/admin/inventory', '/admin/rentals'].includes(location.pathname) ? ['services'] : []}
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
            <Badge count={5} size="small">
              <Button type="text" shape="circle" icon={<BellOutlined style={{ fontSize: 18 }} />} />
            </Badge>
            <Dropdown menu={userMenu} placement="bottomRight">
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
                <Avatar style={{ backgroundColor: '#005f8e' }} icon={<UserOutlined />} />
                <span style={{ fontWeight: 600 }}>Quản trị viên</span>
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
