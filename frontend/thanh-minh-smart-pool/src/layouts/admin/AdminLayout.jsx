import { useState, useEffect, useCallback, useRef } from "react";
import {
  Layout,
  Menu,
  Button,
  Avatar,
  Dropdown,
  Badge,
  Drawer,
  Grid,
  message,
  notification,
} from "antd";
import {
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  DashboardOutlined,
  UserOutlined,
  SafetyCertificateOutlined,
  AuditOutlined,
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
  SecurityScanOutlined,
  AlertOutlined,
  // BookOutlined,
  ExperimentOutlined,
  PlusCircleOutlined,
  UnorderedListOutlined,
} from "@ant-design/icons";
import { Outlet, Link, useLocation, useNavigate } from "react-router-dom";
import { useAuthStore } from "../../features/auth/store/authStore";
import { notificationService } from "../../features/notifications/services/notificationService";
import {
  onReceiveNotification,
  startSignalRConnection,
} from "../../services/signalrClient";
import authService from "../../features/auth/services/authService";
import { getSessionRoles } from "../../features/auth/utils/sessionRoles";
import { useAlarmSound } from "../../features/safety/hooks/useAlarmSound";
import { useSafetyAlertSignalR } from "../../features/safety/hooks/useSafetyAlertSignalR";
import "./AdminLayout.css";

const { Header, Sider, Content } = Layout;

function openKeysForPath(pathname) {
  if (pathname === "/admin/staff/attendance/scan") return ["staff"];
  if (pathname.startsWith("/admin/staff/attendance"))
    return ["staff", "staff-attendance"];
  if (pathname.startsWith("/admin/staff/")) return ["staff"];
  if (pathname.startsWith("/admin/equipments/")) return ["equipments"];
  if (
    pathname.startsWith("/admin/services") ||
    pathname.startsWith("/admin/inventory") ||
    pathname.startsWith("/admin/rentals")
  )
    return ["services"];
  if (pathname.startsWith("/admin/payments")) return ["payments"];
  if (pathname.startsWith("/admin/statistics")) return ["statistics"];
  if (pathname.startsWith("/admin/safety")) return ["safety"];
  return [];
}

export default function AdminLayout() {
  const location = useLocation();
  const { session } = useAuthStore();
  const navigate = useNavigate();
  const role = session?.role?.toUpperCase();
  const roles = getSessionRoles(session);
  const isCoach = roles.includes("COACH");
  const selectedMenuPath = location.pathname;
  const isAdmin = role === "ADMIN";
  const isLifeguard = role === "LIFEGUARD";
  const [collapsed, setCollapsed] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const screens = Grid.useBreakpoint();
  const isMobile = !screens.md;
  const [unreadCount, setUnreadCount] = useState(0);

  // Global Safety Alert — phát âm thanh cảnh báo trên mọi thiết bị đang kết nối
  const { startAlarm, stopAlarm } = useAlarmSound();
  const stopAlarmTimerRef = useRef(null);
  const [notifApi, notifContextHolder] = notification.useNotification();

  const handleGlobalAlert = useCallback(
    (payload) => {
      startAlarm(payload.level);
      notifApi.error({
        message:
          payload.level === "EMERGENCY"
            ? "KHẨN CẤP — Cần cứu hộ ngay!"
            : "Cảnh báo an toàn",
        description: (
          <div>
            <div>
              <strong>Khu vực:</strong> {payload.zone}
            </div>
            {payload.description && (
              <div>
                <strong>Mô tả:</strong> {payload.description}
              </div>
            )}
            <div>
              <strong>Kích hoạt bởi:</strong> {payload.triggeredByName}
            </div>
          </div>
        ),
        duration: 30,
        placement: "topRight",
        icon: <AlertOutlined style={{ color: "#e31748" }} />,
        style: { borderLeft: "4px solid #e31748" },
      });
      if (stopAlarmTimerRef.current) clearTimeout(stopAlarmTimerRef.current);
      stopAlarmTimerRef.current = setTimeout(() => stopAlarm(), 30000);
    },
    [startAlarm, stopAlarm, notifApi],
  );

  useSafetyAlertSignalR({ onAlert: handleGlobalAlert, enabled: true });

  useEffect(() => {
    return () => {
      if (stopAlarmTimerRef.current) clearTimeout(stopAlarmTimerRef.current);
      stopAlarm();
    };
  }, [stopAlarm]);

  useEffect(() => {
    const desktop = window.matchMedia("(min-width: 768px)");
    const closeMenu = () => setMobileMenuOpen(false);
    desktop.addEventListener("change", closeMenu);
    return () => desktop.removeEventListener("change", closeMenu);
  }, []);

  useEffect(() => {
    if (!isAdmin) return undefined;
    const fetchUnread = async () => {
      try {
        const res = await notificationService.getNotifications({
          page: 1,
          pageSize: 1,
        });
        if (res && typeof res.unreadCount === "number") {
          setUnreadCount(res.unreadCount);
        }
      } catch {
        // ignored
      }
    };
    fetchUnread();
    startSignalRConnection();
    const unsub = onReceiveNotification(() => {
      setUnreadCount((prev) => prev + 1);
    });
    const interval = setInterval(fetchUnread, 30000);
    return () => {
      clearInterval(interval);
      unsub();
    };
  }, [location.pathname, isAdmin]);

  const roleLabelMap = {
    ADMIN: "Quản trị viên",
    COACH: "Huấn luyện viên",
    LIFEGUARD: "Nhân viên cứu hộ",
    SALES: "Nhân viên bán vé",
  };
  const roleDisplay =
    isCoach && role === "CUSTOMER"
      ? "Khách hàng · Coach"
      : roleLabelMap[role] || (isAdmin ? "Quản trị viên" : "Nhân viên");

  const menuItems = [
    isAdmin && {
      key: "/admin",
      icon: <DashboardOutlined />,
      label: <Link to="/admin">Bảng điều khiển</Link>,
    },
    isAdmin && {
      key: "/admin/users",
      icon: <UserOutlined />,
      label: <Link to="/admin/users">Quản lý người dùng</Link>,
    },
    isAdmin && {
      key: "/admin/roles/permissions",
      icon: <SafetyCertificateOutlined />,
      label: <Link to="/admin/roles/permissions">Phân quyền vai trò</Link>,
    },
    isAdmin && {
      key: "/admin/coach-applications",
      icon: <AuditOutlined />,
      label: <Link to="/admin/coach-applications">Duyệt Coach</Link>,
    },
    isAdmin && {
      key: "/admin/classes/create",
      icon: <PlusCircleOutlined />,
      label: <Link to="/admin/classes/create">Tạo lớp dạy bơi</Link>,
    },
    (isAdmin || isCoach) && {
      key: "/admin/classes",
      icon: <UnorderedListOutlined />,
      label: (
        <Link to="/admin/classes">
          {isAdmin ? "Danh sách lớp dạy bơi" : "Lớp được phân công"}
        </Link>
      ),
    },
    {
      key: "staff",
      icon: <TeamOutlined />,
      label: "Quản lý nhân viên",
      children: [
        isAdmin && {
          key: "staff-attendance",
          label: "Chấm công nhân viên",
          children: [
            {
              key: "/admin/staff/attendance",
              label: (
                <Link to="/admin/staff/attendance">Check-in / Check-out</Link>
              ),
            },
            {
              key: "/admin/staff/attendance/qr",
              label: <Link to="/admin/staff/attendance/qr">Chấm công QR</Link>,
            },
          ],
        },
        (role === "SALES" || role === "LIFEGUARD") && {
          key: "/admin/staff/attendance/scan",
          label: (
            <Link to="/admin/staff/attendance/scan">
              Quét QR chấm công cá nhân
            </Link>
          ),
        },
        isAdmin && {
          key: "/admin/staff/shifts",
          label: <Link to="/admin/staff/shifts">Ca làm việc</Link>,
        },
        {
          key: "/admin/staff/schedule",
          label: <Link to="/admin/staff/schedule">Lịch làm việc</Link>,
        },
        isAdmin && {
          key: "/admin/staff/profiles",
          label: <Link to="/admin/staff/profiles">Hồ sơ nhân viên</Link>,
        },
      ].filter(Boolean),
    },
    (isAdmin || role === "SALES") && {
      key: "/admin/tickets",
      icon: <IdcardOutlined />,
      label: "Quản lý vé",
      children: [
        {
          key: "/admin/tickets/scanQR",
          label: <Link to="/admin/tickets/scanQR">Quét QR vào cổng</Link>,
        },
        {
          key: "/admin/tickets/types",
          label: <Link to="/admin/tickets/types">Loại vé</Link>,
        },
        {
          key: "/admin/tickets/pos",
          label: <Link to="/admin/tickets/pos">Bán vé tại quầy (POS)</Link>,
        },
      ],
    },
    (isAdmin || role === "SALES") && {
      key: "/admin/coupons",
      icon: <TagOutlined />,
      label: <Link to="/admin/coupons">Quản lý mã giảm giá</Link>,
    },
    (isAdmin || isLifeguard) && {
      key: "safety",
      icon: <SecurityScanOutlined />,
      label: "Giám sát an toàn",
      children: [
        isAdmin && {
          key: "/admin/safety/list-camera",
          label: <Link to="/admin/safety/list-camera">Danh sách camera</Link>,
        },
        {
          key: "/admin/safety/activate-alerts",
          label: (
            <Link to="/admin/safety/activate-alerts">Kích hoạt cảnh báo</Link>
          ),
        },
        {
          key: "/admin/safety/incidents",
          label: <Link to="/admin/safety/incidents">Lịch sử sự cố</Link>,
        },
      ].filter(Boolean),
    },
    (isAdmin || isLifeguard) && {
      key: "/admin/water-quality",
      icon: <ExperimentOutlined />,
      label: <Link to="/admin/water-quality">Chất lượng nước</Link>,
    },
    (isAdmin || isLifeguard) && {
      key: "equipments",
      icon: <ToolOutlined />,
      label: "Quản lý thiết bị",
      children: [
        {
          key: "/admin/equipments/daily-inspections",
          label: (
            <Link to="/admin/equipments/daily-inspections">
              Kiểm tra đầu ca (Checklist)
            </Link>
          ),
        },
        isAdmin && {
          key: "/admin/equipments/list-equipments",
          label: (
            <Link to="/admin/equipments/list-equipments">
              Danh sách thiết bị
            </Link>
          ),
        },
        isAdmin && {
          key: '/admin/equipments/inspection-history',
          label: <Link to="/admin/equipments/inspection-history">Lịch sử kiểm tra</Link>,
        },
      ].filter(Boolean),
    },
    (isAdmin || role === "SALES") && {
      key: "services",
      className: "admin-service-menu",
      icon: <AppstoreOutlined />,
      label: "Dịch vụ cho thuê",
      children: [
        {
          key: "/admin/services",
          label: <Link to="/admin/services">Đồ cho thuê</Link>,
        },
        {
          key: "/admin/rentals",
          label: <Link to="/admin/rentals">Giao dịch cho thuê</Link>,
        },
      ],
    },
    isAdmin && {
      key: "payments",
      icon: <TransactionOutlined />,
      label: "Thanh toán và giao dịch",
      children: [
        {
          key: "/admin/payments/invoices",
          label: <Link to="/admin/payments/invoices">Lịch sử thanh toán</Link>,
        },
      ],
    },
    isAdmin && {
      key: "statistics",
      icon: <BarChartOutlined />,
      label: "Thống kê",
      children: [
        {
          key: "/admin/statistics/ticket-revenue",
          label: (
            <Link to="/admin/statistics/ticket-revenue">
              Thống kê số tiền vé
            </Link>
          ),
        },
        {
          key: "/admin/statistics/monthly-tickets",
          label: (
            <Link to="/admin/statistics/monthly-tickets">
              Thống kê vé tháng
            </Link>
          ),
        },
        {
          key: "/admin/statistics/user-count",
          label: (
            <Link to="/admin/statistics/user-count">
              Thống kê số lượng người dùng
            </Link>
          ),
        },
        {
          key: "/admin/statistics/incidents",
          label: <Link to="/admin/statistics/incidents">Thống kê sự cố</Link>,
        },
      ],
    },
  ].filter(Boolean);

  const handleLogout = async () => {
    try {
      await authService.logout();
    } catch {
      message.warning(
        "Không thể xác nhận đăng xuất với máy chủ. Phiên trên thiết bị đã được xóa.",
      );
    }
    navigate("/dang-nhap", { replace: true });
  };

  const handleUserMenuClick = ({ key }) => {
    if (key === "profile") {
      navigate("/ho-so");
    } else if (key === "change-password") {
      navigate("/admin/doi-mat-khau");
    } else if (key === "logout") {
      void handleLogout();
    }
  };

  const userMenu = {
    items: [
      { key: "profile", icon: <UserOutlined />, label: "Hồ sơ cá nhân" },
      { key: "change-password", icon: <LockOutlined />, label: "Đổi mật khẩu" },
      { type: "divider" },
      {
        key: "logout",
        icon: <LogoutOutlined />,
        label: "Đăng xuất",
        danger: true,
      },
    ],
    onClick: handleUserMenuClick,
  };

  return (
    <>
      {notifContextHolder}
      <Layout
        className="admin-layout"
        style={{ minHeight: "100vh", background: "#e2e8f0" }}
      >
        {isMobile ? (
          <Drawer
            title="Thanh Minh Admin"
            placement="left"
            width={280}
            open={mobileMenuOpen}
            onClose={() => setMobileMenuOpen(false)}
            rootClassName="admin-navigation-drawer"
          >
            <Menu
              mode="inline"
              selectedKeys={[selectedMenuPath]}
              defaultOpenKeys={openKeysForPath(location.pathname)}
              items={menuItems}
              onClick={() => setMobileMenuOpen(false)}
            />
          </Drawer>
        ) : (
          <Sider
            trigger={null}
            collapsible
            collapsed={collapsed}
            width={250}
            className="admin-sidebar"
          >
            <div className="bubbles-container">
              <div className="bubble" />
              <div className="bubble" />
              <div className="bubble" />
              <div className="bubble" />
              <div className="bubble" />
              <div className="bubble" />
              <div className="bubble" />
            </div>
            <div className="admin-logo">
              {collapsed ? (
                <span className="admin-logo-collapsed">TM</span>
              ) : (
                <span className="admin-logo-text">Thanh Minh Admin</span>
              )}
            </div>
            <Menu
              mode="inline"
              selectedKeys={[selectedMenuPath]}
              defaultOpenKeys={openKeysForPath(location.pathname)}
              items={menuItems}
              style={{
                borderRight: 0,
                marginTop: 16,
                position: "relative",
                zIndex: 10,
                overflowY: "auto",
                height: "calc(100vh - 80px)",
              }}
            />
          </Sider>
        )}
        <Layout style={{ background: "#e2e8f0", minWidth: 0 }}>
          <Header
            className="admin-header"
            style={{
              padding: "0 24px",
              background: "#ffffff",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              borderBottom: "1px solid #e6e9ec",
              boxShadow: "0 1px 4px rgba(0, 21, 41, 0.04)",
              zIndex: 1,
            }}
          >
            <div style={{ display: "flex", alignItems: "center" }}>
              <Button
                type="text"
                icon={
                  isMobile || collapsed ? (
                    <MenuUnfoldOutlined />
                  ) : (
                    <MenuFoldOutlined />
                  )
                }
                aria-label={
                  isMobile
                    ? "Mở menu quản trị"
                    : collapsed
                      ? "Mở rộng menu quản trị"
                      : "Thu gọn menu quản trị"
                }
                aria-expanded={isMobile ? mobileMenuOpen : !collapsed}
                onClick={() =>
                  isMobile ? setMobileMenuOpen(true) : setCollapsed(!collapsed)
                }
                style={{
                  fontSize: "16px",
                  width: 64,
                  height: 64,
                  marginLeft: isMobile ? -16 : -24,
                }}
              />
            </div>

            <div className="admin-header-actions">
              {isAdmin && (
                <Link
                  to="/admin"
                  style={{ display: "inline-flex" }}
                  title="Thông báo hệ thống (tại Bảng điều khiển)"
                >
                  <Badge count={unreadCount} size="small" overflowCount={99}>
                    <Button
                      type="text"
                      shape="circle"
                      aria-label="Thông báo hệ thống tại Bảng điều khiển"
                      icon={<BellOutlined style={{ fontSize: 18 }} />}
                    />
                  </Badge>
                </Link>
              )}
              <Dropdown
                menu={userMenu}
                placement="bottomRight"
                trigger={["click"]}
              >
                <button
                  type="button"
                  className="admin-account-trigger"
                  aria-label="Menu tài khoản"
                >
                  <Avatar
                    style={{ backgroundColor: "#0068ff" }}
                    icon={<UserOutlined />}
                  />
                  <span className="admin-account-name">
                    {session?.username
                      ? `${session.username} (${roleDisplay})`
                      : roleDisplay}
                  </span>
                </button>
              </Dropdown>
            </div>
          </Header>
          <Content className="admin-content" style={{ minHeight: 280 }}>
            <Outlet />
          </Content>
        </Layout>
      </Layout>
    </>
  );
}
