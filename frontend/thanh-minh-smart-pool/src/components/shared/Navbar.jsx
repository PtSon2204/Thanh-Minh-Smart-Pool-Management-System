import { useState, useEffect } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Avatar, Drawer, Dropdown, message } from 'antd'
import { UserOutlined, LogoutOutlined, EditOutlined, ShoppingOutlined, LockOutlined } from '@ant-design/icons'
import { useQuery } from '@tanstack/react-query'
import './Navbar.css'
import logoImg from '../../assets/logo-be-boi-thanh-minh.png'
import VoltageButton from './VoltageButton'
import { useAuthStore } from '../../features/auth/store/authStore'
import CartIcon from '../../features/cart/components/CartIcon'
import profileService from '../../features/profiles/services/profileService'
import authService from '../../features/auth/services/authService'

const navItems = [
  { label: 'Trang chủ', to: '/' },
  { label: 'Giới thiệu', to: '/gioi-thieu' },
  { label: 'Bảng giá vé', to: '/bang-gia' },
  { label: 'Liên hệ', to: '/lien-he' },
]

export default function Navbar() {
  const [scrolled, setScrolled] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  
  const location = useLocation()
  const navigate = useNavigate()

  const { session } = useAuthStore()
  const role = session?.role?.toUpperCase()
  const visibleNavItems = role === 'CUSTOMER'
    ? [...navItems, { label: 'Lớp học bơi', to: '/lop-hoc' }]
    : navItems

  async function handleLogout() {
    try {
      await authService.logout()
    } catch {
      message.warning('Không thể xác nhận đăng xuất với máy chủ. Phiên trên thiết bị đã được xóa.')
    }
    navigate('/dang-nhap', { replace: true })
  }
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

  useEffect(() => {
    const desktop = window.matchMedia('(min-width: 1025px)')
    const closeMenu = () => setMenuOpen(false)
    desktop.addEventListener('change', closeMenu)
    return () => desktop.removeEventListener('change', closeMenu)
  }, [])

  const userMenu = {
    items: [
      role === 'CUSTOMER' && {
        key: '0',
        icon: <UserOutlined />,
        label: 'Thẻ của tôi',
        onClick: () => navigate('/the-cua-toi')
      },
      role === 'COACH' && {
        key: 'classes',
        icon: <EditOutlined />,
        label: 'Lớp được phân công',
        onClick: () => navigate('/admin/classes')
      },
      role === 'ADMIN' && {
        key: 'admin',
        icon: <EditOutlined />,
        label: 'Trang quản trị',
        onClick: () => navigate('/admin')
      },
      {
        key: '1',
        icon: <EditOutlined />,
        label: 'Hồ sơ cá nhân',
        onClick: () => navigate('/ho-so')
      },
      {
        key: '2',
        icon: <LockOutlined />,
        label: 'Đổi mật khẩu',
        onClick: () => navigate('/doi-mat-khau')
      },
      role === 'CUSTOMER' && { key: '3', icon: <ShoppingOutlined />, label: 'Lịch sử thanh toán', onClick: () => navigate('/lich-su-thanh-toan') },
      {
        key: '4',
        icon: <LogoutOutlined />,
        label: 'Đăng xuất',
        danger: true,
        onClick: () => { void handleLogout() }
      }
    ].filter(Boolean)
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
            {visibleNavItems.map((item) => (
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

          <div className="navbar-cta">
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

          <div className="navbar-mobile-actions">
            {session?.role?.toUpperCase() === 'CUSTOMER' && <CartIcon />}
            <button
              className="hamburger"
              onClick={() => setMenuOpen(!menuOpen)}
              aria-label="Mở menu điều hướng"
              aria-expanded={menuOpen}
              aria-controls="mobile-navigation"
            >
              <span />
              <span />
              <span />
            </button>
          </div>
        </div>
      </div>
      <Drawer
        title="Thanh Minh Smart Pool"
        placement="right"
        open={menuOpen}
        onClose={() => setMenuOpen(false)}
        width={320}
        rootClassName="mobile-navigation-drawer"
      >
        <div id="mobile-navigation" className="mobile-navigation">
          {visibleNavItems.map(item => <Link key={item.to} to={item.to} aria-current={location.pathname === item.to ? 'page' : undefined} onClick={() => setMenuOpen(false)}>{item.label}</Link>)}
          <div className="mobile-navigation-account">
            {session ? <>
              <strong>{profileData?.fullName || session.username}</strong>
              {role !== 'CUSTOMER' && <Link to={role === 'COACH' ? '/admin/classes' : '/admin'} onClick={() => setMenuOpen(false)}>{role === 'COACH' ? 'Lớp được phân công' : 'Trang quản trị'}</Link>}
              {userMenu.items.map(item => <button key={item.key} type="button" onClick={() => { setMenuOpen(false); item.onClick() }}>{item.icon}{item.label}</button>)}
            </> : <>
              <Link to="/dang-ky" onClick={() => setMenuOpen(false)}>Đăng ký</Link>
              <Link to="/dang-nhap" onClick={() => setMenuOpen(false)}>Đăng nhập</Link>
            </>}
          </div>
        </div>
      </Drawer>
    </nav>
  )
}


