import { Outlet } from 'react-router-dom'
import Navbar from '../components/shared/Navbar'
import Footer from '../components/shared/Footer'
import CartDrawer from '../features/cart/components/CartDrawer'
import FloatingContact from '../components/shared/FloatingContact'
import GlobalSafetyAlertsHost from '../features/safety/components/GlobalSafetyAlertsHost'
import './MainLayout.css'

export default function MainLayout() {
  return (
    <div className="main-layout">
      <Navbar />
      <div className="main-content">
        <Outlet />
      </div>
      <FloatingContact />
      <CartDrawer />
      <Footer />
      <GlobalSafetyAlertsHost />
    </div>
  )
}

