import { useState, useEffect } from 'react'
import { Link, useLocation } from 'react-router-dom'
import './Navbar.css'
import logoImg from '../../assets/logo-be-boi-thanh-minh.png'
import VoltageButton from './VoltageButton'

const navItems = [
  { label: 'Trang chủ', to: '/' },
  { label: 'Giới thiệu', to: '/gioi-thieu' },
  { label: 'Bảng giá vé', to: '/bang-gia' },
  { label: 'Sơ đồ bể bơi', to: '/so-do-be-boi' },
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
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setMenuOpen(false)
  }, [location])

  return (
    <nav className={`navbar${scrolled ? ' navbar-scrolled' : ''}`}>
      {/* Main bar */}
      <div className="navbar-main">
        <div className="container">
          <Link to="/" className="navbar-logo">
            <img src={logoImg} alt="Logo" className="navbar-logo-img" />
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

          <div className="navbar-cta" style={{ display: 'flex', gap: '12px' }}>
            <VoltageButton to="/dang-ky" variant="outline">
              Đăng ký
            </VoltageButton>
            <VoltageButton to="/dang-nhap" variant="solid">
              Đăng nhập
            </VoltageButton>
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
