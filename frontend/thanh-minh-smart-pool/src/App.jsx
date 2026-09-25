import { BrowserRouter, Routes, Route } from 'react-router-dom'
import MainLayout from './layouts/MainLayout'
import HomePage from './pages/HomePage'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<MainLayout />}>
          <Route path="/" element={<HomePage />} />
          {/* Future routes go here */}
          {/* <Route path="/gioi-thieu" element={<AboutPage />} /> */}
          {/* <Route path="/dat-ve" element={<TicketPage />} /> */}
          {/* <Route path="/bang-gia" element={<PricingPage />} /> */}
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

export default App
