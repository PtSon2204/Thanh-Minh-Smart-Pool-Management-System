import { createBrowserRouter, Navigate } from 'react-router-dom'
import MainLayout from "../layouts/MainLayout";
import HomePage from "../pages/HomePage/HomePage";
import AboutPage from "../pages/AboutPage/AboutPage";
import PricingPage from "../pages/PricingPage/PricingPage";
import ContactPage from "../pages/ContactPage/ContactPage";
import NotFoundPage from "../pages/NotFoundPage/NotFoundPage";
import AuthPage from "../pages/AuthPage/AuthPage";
import ChangePasswordPage from "../pages/ChangePasswordPage/ChangePasswordPage";
import ProfilePage from "../pages/ProfilePage/ProfilePage";
import MyCardPage from "../pages/MyCardPage/MyCardPage";
import PaymentHistoryPage from "../pages/PaymentHistoryPage/PaymentHistoryPage";
import Error401Page from "../pages/Error401Page/Error401Page";
import ProtectedRoute from "./ProtectedRoute";
import AdminLayout from "../layouts/admin/AdminLayout";
import DashboardPage from "../pages/admin/dashboard/DashboardPage";
import OfflineSalesPage from "../pages/admin/tickets/OfflineSalesPage";
import TicketTypePage from "../pages/admin/tickets/TicketTypePage";
import ServicePage from "../pages/admin/services/ServicePage";
import InventoryPage from "../pages/admin/services/InventoryPage";
import RentalPage from "../pages/admin/rentals/RentalPage";
import VoucherPage from "../pages/admin/vouchers/VoucherPage";
import NotificationPage from "../pages/admin/notifications/NotificationPage";
import CustomerStatisticsPage from "../pages/admin/statistics/CustomerStatisticsPage";
import TicketRevenueStatisticsPage from "../pages/admin/statistics/TicketRevenueStatisticsPage";
import MonthlyTicketsPage from "../pages/admin/statistics/MonthlyTicketsPage";
import IncidentStatisticsPage from "../pages/admin/statistics/IncidentStatisticsPage";
import OperationCostsStatisticsPage from "../pages/admin/statistics/OperationCostsStatisticsPage";
import StaffProfilesPage from "../pages/admin/staff/StaffProfilesPage";
import ShiftManagementPage from "../pages/admin/staff/ShiftManagementPage";
import StaffSchedulePage from "../pages/admin/staff/StaffSchedulePage";
import StaffAttendancePage from "../pages/admin/staff/StaffAttendancePage";
import StaffQrAttendancePage from "../pages/admin/staff/StaffQrAttendancePage";
import StaffAttendanceScanPage from "../pages/admin/staff/StaffAttendanceScanPage";
import TicketScannerPage from "../pages/admin/tickets/TicketScannerPage";
import UserManagementPage from "../pages/admin/users/UserManagementPage";
import RolePermissionsPage from "../pages/admin/permissions/RolePermissionsPage";
import EquipmentPage from "../pages/admin/equipments/EquipmentPage";
import MaintenancePage from "../pages/admin/maintenance/MaintenancePage";
import MaintenanceHistoryPage from "../pages/admin/maintenance/MaintenanceHistoryPage";

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
          { path: '/doi-mat-khau', element: <ChangePasswordPage /> },
          { path: '/the-cua-toi', element: <MyCardPage /> },
          { path: '/lich-su-thanh-toan', element: <PaymentHistoryPage isCustomerView /> },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
  { path: '/401', element: <Error401Page /> },
  { path: '/unauthorized', element: <Error401Page /> },
  { path: '/dang-nhap', element: <AuthPage /> },
  { path: '/dang-ky', element: <AuthPage /> },
  { path: '/quen-mat-khau', element: <AuthPage /> },
  { path: '/xac-nhan-otp/dang-ky', element: <AuthPage /> },
  { path: '/xac-nhan-otp/quen-mat-khau', element: <AuthPage /> },
  { path: '/dat-lai-mat-khau', element: <AuthPage /> },
  {
    element: <ProtectedRoute allowedRoles={["ADMIN", "LIFEGUARD", "SALES"]} />,
    children: [
      {
        path: '/admin',
        element: <AdminLayout />,
        children: [
          { path: 'doi-mat-khau', element: <ChangePasswordPage /> },
          { path: 'staff/schedule', element: <StaffSchedulePage /> },
          {
            element: <ProtectedRoute allowedRoles={["ADMIN"]} />,
            children: [
              { index: true, element: <DashboardPage /> },
              { path: 'equipments/list-equipments', element: <EquipmentPage /> },
              { path: 'equipments/maintenance', element: <MaintenancePage /> },
              { path: 'equipments/maintenance-history', element: <MaintenanceHistoryPage /> },
              { path: 'equipment', element: <Navigate to="/admin/equipments/list-equipments" replace /> },
              { path: 'staff/shifts', element: <ShiftManagementPage /> },
              { path: 'notifications', element: <NotificationPage /> },
              { path: 'statistics/user-count', element: <CustomerStatisticsPage /> },
              { path: 'statistics/ticket-revenue', element: <TicketRevenueStatisticsPage /> },
              { path: 'statistics/monthly-tickets', element: <MonthlyTicketsPage /> },
              { path: 'statistics/incidents', element: <IncidentStatisticsPage /> },
              { path: 'statistics/operations-costs', element: <OperationCostsStatisticsPage /> },
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

export default router
