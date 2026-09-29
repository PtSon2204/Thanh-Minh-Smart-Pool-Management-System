import { BrowserRouter, Routes, Route } from 'react-router-dom'
import MainLayout from './layouts/MainLayout'
import HomePage from './pages/HomePage'
import AdminLayout from './layouts/admin/AdminLayout'
import DashboardPage from './pages/admin/DashboardPage'
import TicketTypePage from './pages/admin/TicketTypePage'

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
            <Route path="tickets/types" element={<TicketTypePage />} />
            {/* Future admin routes go here */}
          </Route>
        </Routes>
      </BrowserRouter>
    </ConfigProvider>
  )
}

export default App
