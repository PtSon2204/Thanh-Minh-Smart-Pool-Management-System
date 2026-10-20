import { ConfigProvider } from 'antd'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import MainLayout from "./layouts/MainLayout";
import HomePage from "./pages/HomePage/HomePage";
import AboutPage from "./pages/AboutPage/AboutPage";
import PricingPage from "./pages/PricingPage/PricingPage";
import ContactPage from "./pages/ContactPage/ContactPage";
import NotFoundPage from "./pages/NotFoundPage/NotFoundPage";
import AuthPage from "./pages/AuthPage/AuthPage";
import ProfilePage from "./pages/ProfilePage/ProfilePage";
import MyCardPage from "./pages/MyCardPage/MyCardPage";
import PaymentHistoryPage from "./pages/PaymentHistoryPage/PaymentHistoryPage";
import Error401Page from "./pages/Error401Page/Error401Page";
import ProtectedRoute from "./routes/ProtectedRoute";
import AdminLayout from "./layouts/admin/AdminLayout";
import DashboardPage from "./pages/admin/DashboardPage";
import OfflineSalesPage from "./pages/admin/OfflineSalesPage";
import TicketTypePage from "./pages/admin/TicketTypePage";
import ServicePage from "./pages/admin/ServicePage";
import InventoryPage from "./pages/admin/InventoryPage";
import RentalPage from "./pages/admin/RentalPage";
import VoucherPage from "./pages/admin/VoucherPage";
import NotificationPage from "./pages/admin/NotificationPage";
import CustomerStatisticsPage from "./pages/admin/statistics/CustomerStatisticsPage";
import StaffProfilesPage from "./pages/admin/StaffProfilesPage";
import ShiftManagementPage from "./pages/admin/ShiftManagementPage";
import StaffSchedulePage from "./pages/admin/StaffSchedulePage";
import StaffAttendancePage from "./pages/admin/StaffAttendancePage";
import StaffQrAttendancePage from "./pages/admin/StaffQrAttendancePage";
import StaffAttendanceScanPage from "./pages/admin/StaffAttendanceScanPage";
import TicketScannerPage from "./pages/admin/TicketScannerPage";
import UserManagementPage from "./pages/admin/UserManagementPage";
import RolePermissionsPage from "./pages/admin/RolePermissionsPage";

const router = createBrowserRouter([
  {
    element: <MainLayout />,
    children: [
      { path: '/', element: <HomePage /> },
      { path: '/gioi-thieu', element: <AboutPage /> },
      { path: '/bang-gia', element: <PricingPage /> },
      { path: '/lien-he', element: <ContactPage /> },
      {
        element: <ProtectedRoute redirectTo="/dang-nhap" allowedRoles={["CUSTOMER", "ADMIN", "LIFEGUARD", "SALES"]} />,
        children: [
          { path: '/ho-so', element: <ProfilePage /> },
          { path: '/the-cua-toi', element: <MyCardPage /> },
          { path: '/lich-su-thanh-toan', element: <PaymentHistoryPage /> },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
  { path: '/401', element: <Error401Page /> },
  { path: '/unauthorized', element: <Error401Page /> },
  { path: '/dang-nhap', element: <AuthPage /> },
  { path: '/dang-ky', element: <AuthPage /> },
  {
    element: <ProtectedRoute allowedRoles={["ADMIN", "LIFEGUARD", "SALES"]} />,
    children: [
      {
        path: '/admin',
        element: <AdminLayout />,
        children: [
          { path: 'staff/schedule', element: <StaffSchedulePage /> },
          {
            element: <ProtectedRoute allowedRoles={["ADMIN"]} />,
            children: [
              { path: 'staff/shifts', element: <ShiftManagementPage /> },
              { index: true, element: <DashboardPage /> },
              { path: 'notifications', element: <NotificationPage /> },
              { path: 'statistics/user-count', element: <CustomerStatisticsPage /> },
              { path: 'staff/profiles', element: <StaffProfilesPage /> },
              { path: 'payments/invoices', element: <PaymentHistoryPage /> },
              { path: 'staff/attendance', element: <StaffAttendancePage /> },
              { path: 'staff/attendance/qr', element: <StaffQrAttendancePage /> },
              { path: 'users', element: <UserManagementPage /> },
              { path: 'roles/permissions', element: <RolePermissionsPage /> },
            ],
          },
          {
            element: <ProtectedRoute allowedRoles={["ADMIN", "SALES"]} />,
            children: [
              { path: 'tickets/pos', element: <OfflineSalesPage /> },
              { path: 'tickets/types', element: <TicketTypePage /> },
              { path: 'tickets/scanQR', element: <TicketScannerPage /> },
              { path: 'services', element: <ServicePage /> },
              { path: 'inventory', element: <InventoryPage /> },
              { path: 'rentals', element: <RentalPage /> },
              { path: 'coupons', element: <VoucherPage /> },
            ],
          },
          {
            element: <ProtectedRoute allowedRoles={["SALES", "LIFEGUARD"]} />,
            children: [{ path: 'staff/attendance/scan', element: <StaffAttendanceScanPage /> }],
          },
        ],
      },
    ],
  },
])

export default function App() {
  return <ConfigProvider theme={{ token: { fontFamily: "'Open Sans', system-ui, -apple-system, Roboto, sans-serif" } }}><RouterProvider router={router} /></ConfigProvider>
}
