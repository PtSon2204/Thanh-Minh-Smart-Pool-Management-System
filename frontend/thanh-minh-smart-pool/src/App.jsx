import { BrowserRouter, Routes, Route } from 'react-router-dom'
import MainLayout from './layouts/MainLayout'
import HomePage from './pages/HomePage'
import AboutPage from './pages/AboutPage'
import PricingPage from './pages/PricingPage'
import ContactPage from './pages/ContactPage'
import NotFoundPage from './pages/NotFoundPage'
import AdminLayout from './layouts/admin/AdminLayout'
import DashboardPage from './pages/admin/DashboardPage'
import OfflineSalesPage from './pages/admin/OfflineSalesPage'
import TicketTypePage from './pages/admin/TicketTypePage'
import ServicePage from './pages/admin/ServicePage'
import InventoryPage from './pages/admin/InventoryPage'

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
            <Route path="/gioi-thieu" element={<AboutPage />} />
            <Route path="/bang-gia" element={<PricingPage />} />
            <Route path="/lien-he" element={<ContactPage />} />
            {/* <Route path="/dat-ve" element={<TicketPage />} /> */}
            <Route path="*" element={<NotFoundPage />} />
          </Route>

          {/* Admin Routes */}
          <Route path="/admin" element={<AdminLayout />}>
            <Route index element={<DashboardPage />} />
            <Route path="tickets/pos" element={<OfflineSalesPage />} />
            <Route path="tickets/types" element={<TicketTypePage />} />
            <Route path="services" element={<ServicePage />} />
            <Route path="inventory" element={<InventoryPage />} />
            {/* Future admin routes go here */}
          </Route>
        </Routes>
      </BrowserRouter>
    </ConfigProvider>
  )
}

export default App
