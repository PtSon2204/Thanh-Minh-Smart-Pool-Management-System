import { useState, useEffect } from 'react'
import { Link, useLocation } from 'react-router-dom'
import './Navbar.css'

const navItems = [
  { label: 'Trang chủ', to: '/' },
  { label: 'Giới thiệu', to: '/gioi-thieu' },
  {
    label: 'Dịch vụ',
    to: '/dich-vu',
    children: [
      { label: 'Hồ bơi ngoài trời', to: '/dich-vu/ho-boi-ngoai-troi' },
      { label: 'Cho thuê phao, khăn tắm', to: '/dich-vu/ho-boi-trong-nha' },
      { label: 'Dịch vụ ẩm thực', to: '/dich-vu/am-thuc' },
    ],
  },
  { label: 'Bảng giá', to: '/bang-gia' },
  { label: 'Đặt vé', to: '/dat-ve' },
  { label: 'Tin tức', to: '/tin-tuc' },
  { label: 'Liên hệ', to: '/lien-he' },
]

export default function Navbar() {
  const [scrolled, setScrolled] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const location = useLocation()

  useEffect(() => {
    const handleScroll = () => setScrolled(window.scrollY > 50)
    window.addEventListener('scroll', handleScroll)
    return () => window.removeEventListener('scroll', handleScroll)
  }, [])

  useEffect(() => {
    setMenuOpen(false)
  }, [location])

  return (
    <nav className={`navbar${scrolled ? ' navbar-scrolled' : ''}`}>
      {/* Main bar */}
      <div className="navbar-main">
        <div className="container">
          <Link to="/" className="navbar-logo">
            <div className="navbar-logo-icon">🏊</div>
            <div className="navbar-logo-text">
              <span className="navbar-logo-name">Thanh Minh</span>
              <span className="navbar-logo-sub">Smart Pool Management</span>
            </div>
          </Link>

          <ul className="navbar-nav">
            {navItems.map((item) => (
              <li key={item.to} className="nav-item">
                <Link
                  to={item.to}
                  className={`nav-link${location.pathname === item.to ? ' active' : ''}`}
                >
                  {item.label}
                  {item.children && <span style={{ fontSize: '0.65rem' }}>▾</span>}
                </Link>
                {item.children && (
                  <div className="nav-dropdown">
                    {item.children.map((child) => (
                      <Link key={child.to} to={child.to}>
                        {child.label}
                      </Link>
                    ))}
                  </div>
                )}
              </li>
            ))}
          </ul>

          <div className="navbar-cta">
            <Link to="/dang-ky" className="btn-auth btn-register">
              Đăng ký
            </Link>
            <Link to="/dang-nhap" className="btn-auth btn-login">
              Đăng nhập
            </Link>
          </div>

          <button
            className="hamburger"
            onClick={() => setMenuOpen(!menuOpen)}
            aria-label="Toggle menu"
          >
            <span />
            <span />
            <span />
          </button>
        </div>
      </div>
    </nav>
  )
}
