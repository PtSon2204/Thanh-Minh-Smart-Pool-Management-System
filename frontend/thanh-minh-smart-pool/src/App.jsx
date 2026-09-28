import { BrowserRouter, Routes, Route } from 'react-router-dom'
import MainLayout from './layouts/MainLayout'
import HomePage from './pages/HomePage'
import AdminLayout from './layouts/admin/AdminLayout'
import DashboardPage from './pages/admin/DashboardPage'

import { ConfigProvider } from 'antd'

function App() {
  return (
    <ConfigProvider
      theme={{
        token: {
          fontFamily: "'Open Sans', system-ui, -apple-system, Roboto, sans-serif",
        },
      }}
    >
      <BrowserRouter>
        <Routes>
          {/* Public Routes */}
          <Route element={<MainLayout />}>
            <Route path="/" element={<HomePage />} />
            {/* Future routes go here */}
            {/* <Route path="/gioi-thieu" element={<AboutPage />} /> */}
            {/* <Route path="/dat-ve" element={<TicketPage />} /> */}
            {/* <Route path="/bang-gia" element={<PricingPage />} /> */}
          </Route>

          {/* Admin Routes */}
          <Route path="/admin" element={<AdminLayout />}>
            <Route index element={<DashboardPage />} />
            {/* Future admin routes go here */}
            {/* <Route path="users" element={<UsersPage />} /> */}
            {/* <Route path="orders" element={<OrdersPage />} /> */}
          </Route>
        </Routes>
      </BrowserRouter>
    </ConfigProvider>
  )
}

export default App
