import { BrowserRouter, Routes, Route } from "react-router-dom";
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
import TicketRevenueStatisticsPage from "./pages/admin/statistics/TicketRevenueStatisticsPage";
import StaffProfilesPage from "./pages/admin/StaffProfilesPage";
import ShiftManagementPage from "./pages/admin/ShiftManagementPage";
import StaffSchedulePage from "./pages/admin/StaffSchedulePage";
import UserManagementPage from "./pages/admin/UserManagementPage";
import RolePermissionsPage from "./pages/admin/RolePermissionsPage";

import { ConfigProvider } from "antd";

function App() {
  return (
    <ConfigProvider
      theme={{
        token: {
          fontFamily:
            "'Open Sans', system-ui, -apple-system, Roboto, sans-serif",
        },
      }}
    >
      <BrowserRouter>
        <Routes>
          {/* Public & Customer Layout */}
          <Route element={<MainLayout />}>
            <Route path="/" element={<HomePage />} />
            <Route path="/gioi-thieu" element={<AboutPage />} />
            <Route path="/bang-gia" element={<PricingPage />} />
            <Route path="/lien-he" element={<ContactPage />} />

            {/* Authenticated Customer Routes (redirect to login if not logged in) */}
            <Route
              element={
                <ProtectedRoute
                  redirectTo="/dang-nhap"
                  allowedRoles={["CUSTOMER", "ADMIN", "LIFEGUARD", "SALES"]}
                />
              }
            >
              <Route path="/ho-so" element={<ProfilePage />} />
              <Route path="/the-cua-toi" element={<MyCardPage />} />
              <Route
                path="/lich-su-thanh-toan"
                element={<PaymentHistoryPage />}
              />
            </Route>

            <Route path="*" element={<NotFoundPage />} />
          </Route>

          {/* Standalone Error & Auth Routes (full-screen, no Navbar/Footer) */}
          <Route path="/401" element={<Error401Page />} />
          <Route path="/unauthorized" element={<Error401Page />} />
          <Route path="/dang-nhap" element={<AuthPage />} />
          <Route path="/dang-ky" element={<AuthPage />} />

          {/* Protected Admin Routes (Staff & Admin only) */}
          <Route
            element={
              <ProtectedRoute
                allowedRoles={["ADMIN", "LIFEGUARD", "SALES"]}
              />
            }
          >
            <Route path="/admin" element={<AdminLayout />}>
              <Route index element={<DashboardPage />} />
              <Route path="tickets/pos" element={<OfflineSalesPage />} />
              <Route path="tickets/types" element={<TicketTypePage />} />
              <Route path="services" element={<ServicePage />} />
              <Route path="inventory" element={<InventoryPage />} />
              <Route path="rentals" element={<RentalPage />} />
              <Route path="coupons" element={<VoucherPage />} />
              <Route path="notifications" element={<NotificationPage />} />
              <Route
                path="statistics/user-count"
                element={<CustomerStatisticsPage />}
              />
              <Route
                path="statistics/ticket-revenue"
                element={<TicketRevenueStatisticsPage />}
              />
              <Route path="staff/profiles" element={<StaffProfilesPage />} />
              <Route path="staff/shifts" element={<ShiftManagementPage />} />
              <Route path="staff/schedule" element={<StaffSchedulePage />} />
              <Route
                path="payments/invoices"
                element={<PaymentHistoryPage />}
              />

              {/* Admin-only sensitive management routes */}
              <Route
                element={<ProtectedRoute allowedRoles={["ADMIN"]} />}
              >
                <Route path="users" element={<UserManagementPage />} />
                <Route
                  path="roles/permissions"
                  element={<RolePermissionsPage />}
                />
              </Route>
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </ConfigProvider>
  );
}

export default App;

