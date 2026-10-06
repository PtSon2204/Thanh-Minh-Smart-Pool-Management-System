import { useState, useEffect } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Avatar, Dropdown } from 'antd'
import { UserOutlined, LogoutOutlined, EditOutlined, ShoppingOutlined } from '@ant-design/icons'
import { useQuery } from '@tanstack/react-query'
import './Navbar.css'
import logoImg from '../../assets/logo-be-boi-thanh-minh.png'
import VoltageButton from './VoltageButton'
import { useAuthStore } from '../../features/auth/store/authStore'
import CartIcon from '../../features/cart/components/CartIcon'
import profileService from '../../features/profiles/services/profileService'

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
  const navigate = useNavigate()

  const { session, setSession } = useAuthStore()
  const { data: profileData } = useQuery({
    queryKey: ['myProfile'],
    queryFn: profileService.getMyProfile,
    enabled: !!session
  })

  useEffect(() => {
    const handleScroll = () => setScrolled(window.scrollY > 50)
    window.addEventListener('scroll', handleScroll)
    return () => window.removeEventListener('scroll', handleScroll)
  }, [])

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setMenuOpen(false)
  }, [location])

  const userMenu = {
    items: [
      {
        key: '1',
        icon: <EditOutlined />,
        label: 'Hồ sơ cá nhân',
        onClick: () => navigate('/ho-so')
      },
      {
        key: '2',
        icon: <LogoutOutlined />,
        label: 'Đổi mật khẩu',
        danger: true,
        onClick: () => setSession(null)
      },
      {
        key: '3',
        icon: <ShoppingOutlined />,
        label: 'Lịch sử mua vé',
        danger: true,
        onClick: () => setSession(null)
      },
      {
        key: '4',
        icon: <LogoutOutlined />,
        label: 'Đăng xuất',
        danger: true,
        onClick: () => setSession(null)
      }
    ]
  }

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
                  {item.children && <span style={{ fontSize: '0.65rem' }}>▼</span>}
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

          <div className="navbar-cta" style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
            {session ? (
              <>
                {session.role === 'CUSTOMER' && <CartIcon />}
                <div style={{ marginLeft: '12px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <Dropdown menu={userMenu} placement="bottomRight" trigger={['click']}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', padding: '4px 8px', borderRadius: '24px', background: 'transparent', transition: 'background 0.2s' }} className="user-dropdown-trigger">
                      <Avatar src={profileData?.avatarUrl} style={{ backgroundColor: '#f97316' }} icon={<UserOutlined />} />
                      <span style={{ fontWeight: 600, color: '#002c8c' }}>{profileData?.fullName || session.username}</span>
                    </div>
                  </Dropdown>
                </div>
              </>
            ) : (
              <>
                <VoltageButton to="/dang-ky" variant="outline">
                  Đăng ký
                </VoltageButton>
                <VoltageButton to="/dang-nhap" variant="solid">
                  Đăng nhập
                </VoltageButton>
              </>
            )}
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
