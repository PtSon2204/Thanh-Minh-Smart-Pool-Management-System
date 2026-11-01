import './PricingPage.css'
import { useNavigate, useLocation } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import axios from 'axios'
import { useState, useMemo } from 'react'
import { message } from 'antd'
import { useAuthStore } from '../../features/auth/store/authStore'
import { useCartStore } from '../../features/cart/store/cartStore'

const categoryMeta = {
  VE_THANG: {
    label: 'VÉ THÁNG',
    icon: '📅',
    color: '#0284c7',
    bg: '#e0f2fe',
    border: '#bae6fd',
    badgeText: 'Thành viên tháng',
  },
  VE_LUOT: {
    label: 'VÉ LƯỢT',
    icon: '🎟️',
    color: '#0d9488',
    bg: '#ccfbf1',
    border: '#99f6e4',
    badgeText: 'Khách lượt linh hoạt',
  },
}

const getInfoGrid = (ticket) => {
  const isThang = ticket.ticketCategory === 'VE_THANG'
  return [
    {
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="15" height="15">
          <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
          <circle cx="12" cy="10" r="3" />
        </svg>
      ),
      label: 'Địa điểm',
      value: 'Bể bơi Thành Minh',
    },
    {
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="15" height="15">
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
        </svg>
      ),
      label: 'Hiệu lực',
      value: isThang ? `${ticket.durationDays || 30} ngày` : 'Sử dụng trong ngày',
    },
    {
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="15" height="15">
          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
          <circle cx="12" cy="7" r="4" />
        </svg>
      ),
      label: 'Đối tượng',
      value: isThang ? 'Hội viên đăng ký' : 'Mọi khách hàng',
    },
    {
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="15" height="15">
          <circle cx="12" cy="12" r="5" />
          <line x1="12" y1="1" x2="12" y2="3" />
          <line x1="12" y1="21" x2="12" y2="23" />
          <line x1="4.22" y1="4.22" x2="5.64" y2="5.64" />
          <line x1="18.36" y1="18.36" x2="19.78" y2="19.78" />
          <line x1="1" y1="12" x2="3" y2="12" />
          <line x1="21" y1="12" x2="23" y2="12" />
          <line x1="4.22" y1="19.78" x2="5.64" y2="18.36" />
          <line x1="18.36" y1="5.64" x2="19.78" y2="4.22" />
        </svg>
      ),
      label: 'Giờ mở cửa',
      value: '5:30 - 21:00 hàng ngày',
    },
  ]
}

export default function PricingPage() {
  const [search, setSearch] = useState('')
  const [activeTab, setActiveTab] = useState('ALL')

  const navigate = useNavigate()
  const location = useLocation()
  const { session } = useAuthStore()
  const addToCart = useCartStore((state) => state.addToCart)

  const { data: apiData, isLoading, isError, refetch } = useQuery({
    queryKey: ['ticket-types-public'],
    queryFn: async () => {
      const res = await axios.get('/api/ticket-types?isActive=true&pageIndex=1&pageSize=100')
      return res.data.items || res.data
    },
    retry: 1,
  })

  const handleAddToCart = (ticket) => {
    if (!session) {
      navigate('/dang-nhap?redirect=' + encodeURIComponent(location.pathname))
      return
    }
    addToCart({
      id: ticket.id,
      name: ticket.name,
      price: ticket.price,
      ticketCategory: ticket.ticketCategory,
    })
    message.success(`Đã thêm "${ticket.name}" vào giỏ hàng`)
  }

  const tickets = useMemo(() => {
    return apiData && apiData.length > 0 ? apiData : []
  }, [apiData])

  const counts = useMemo(() => {
    return {
      ALL: tickets.length,
      VE_LUOT: tickets.filter((t) => t.ticketCategory === 'VE_LUOT').length,
      VE_THANG: tickets.filter((t) => t.ticketCategory === 'VE_THANG').length,
    }
  }, [tickets])

  const filtered = useMemo(() => {
    return tickets.filter((t) => {
      const matchTab = activeTab === 'ALL' || t.ticketCategory === activeTab
      const matchSearch = t.name.toLowerCase().includes(search.toLowerCase().trim())
      return matchTab && matchSearch
    })
  }, [tickets, activeTab, search])

  const handleResetFilters = () => {
    setSearch('')
    setActiveTab('ALL')
  }

  return (
    <div className="pl-page">
      {/* ── 1. Hero Banner ── */}
      <section className="pl-hero">
        <div className="pl-hero-bg" aria-hidden="true">
          <div className="pl-hero-orb orb-1" />
          <div className="pl-hero-orb orb-2" />
          <div className="pl-hero-pattern" />
        </div>

        <div className="pl-hero-container">
          <div className="pl-hero-badge">
            <span className="pl-badge-dot" />
            Bảng giá niêm yết chính thức
          </div>

          <h1 className="pl-hero-title">
            Bảng Giá Vé Dịch Vụ
          </h1>

          <p className="pl-hero-sub">
            Minh bạch • Linh hoạt • Phù hợp mọi đối tượng khách hàng từ bơi thư giãn đến rèn luyện sức khỏe.
          </p>

          <div className="pl-hero-badges">
            <span className="pl-hero-pill">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
              </svg>
              Giám sát AI 24/7 an toàn
            </span>
            <span className="pl-hero-pill">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
                <path d="M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z" />
              </svg>
              Kiểm soát chất lượng nước &amp; vệ sinh
            </span>
            <span className="pl-hero-pill">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
                <rect x="3" y="11" width="18" height="11" rx="2" />
                <path d="M7 11V7a5 5 0 0 1 10 0v4" />
              </svg>
              Miễn phí trông xe &amp; tủ đồ
            </span>
          </div>
        </div>
      </section>

      {/* ── 2. Body Container ── */}
      <div className="pl-body">
        {/* ── Control Bar: Search & Filter ── */}
        <div className="pl-control-panel">
          <div className="pl-search-wrapper">
            <svg
              className="pl-search-icon"
              width="18"
              height="18"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              className="pl-search-input"
              type="text"
              placeholder="Tìm kiếm loại vé (VD: người lớn, trẻ em, vé tháng...)"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Tìm kiếm vé"
            />
            {search && (
              <button
                className="pl-search-clear"
                type="button"
                onClick={() => setSearch('')}
                aria-label="Xóa tìm kiếm"
              >
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="18" y1="6" x2="6" y2="18" />
                  <line x1="6" y1="6" x2="18" y2="18" />
                </svg>
              </button>
            )}
          </div>

          <div className="pl-filter-tabs" role="tablist" aria-label="Lọc theo loại vé">
            {[
              { key: 'ALL', label: 'Tất cả', count: counts.ALL },
              { key: 'VE_LUOT', label: 'Vé lượt', count: counts.VE_LUOT },
              { key: 'VE_THANG', label: 'Vé tháng', count: counts.VE_THANG },
            ].map((f) => (
              <button
                key={f.key}
                role="tab"
                aria-selected={activeTab === f.key}
                className={`pl-filter-tab${activeTab === f.key ? ' active' : ''}`}
                onClick={() => setActiveTab(f.key)}
              >
                <span className="pl-tab-label">{f.label}</span>
                <span className="pl-tab-count">{f.count}</span>
              </button>
            ))}
          </div>
        </div>

        {/* ── Status Bar / Result info ── */}
        <div className="pl-status-bar">
          <p className="pl-result-count">
            Đang hiển thị <strong>{filtered.length}</strong> loại vé
            {(search || activeTab !== 'ALL') && ' theo bộ lọc'}
          </p>

          {(search || activeTab !== 'ALL') && (
            <button className="pl-reset-btn" onClick={handleResetFilters}>
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2">
                <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
                <path d="M3 3v5h5" />
              </svg>
              Xóa bộ lọc
            </button>
          )}
        </div>

        {/* ── Loading Skeletons ── */}
        {isLoading && (
          <div className="pl-list">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="pl-skeleton-card">
                <div className="pl-sk-header" />
                <div className="pl-sk-body">
                  <div className="pl-sk-line pl-sk-title" />
                  <div className="pl-sk-line pl-sk-desc" />
                  <div className="pl-sk-grid" />
                  <div className="pl-sk-footer" />
                </div>
              </div>
            ))}
          </div>
        )}

        {/* ── Error State ── */}
        {isError && !isLoading && (
          <div className="pl-error-box">
            <div className="pl-error-icon">⚠️</div>
            <h3>Không thể tải bảng giá vé</h3>
            <p>Hệ thống tạm thời gặp trục trặc khi kết nối với máy chủ. Vui lòng thử lại sau.</p>
            <button className="pl-retry-btn" onClick={() => refetch()}>
              Thử lại ngay
            </button>
          </div>
        )}

        {/* ── Empty State ── */}
        {!isLoading && !isError && filtered.length === 0 && (
          <div className="pl-empty-box">
            <div className="pl-empty-icon">🔍</div>
            <h3>Không tìm thấy loại vé phù hợp</h3>
            <p>
              Không có loại vé nào khớp với từ khóa <strong>"{search}"</strong>
              {activeTab !== 'ALL' && ` trong danh mục ${activeTab === 'VE_THANG' ? 'Vé tháng' : 'Vé lượt'}`}.
            </p>
            <button className="pl-empty-reset-btn" onClick={handleResetFilters}>
              Xem tất cả loại vé
            </button>
          </div>
        )}

        {/* ── Ticket Grid ── */}
        {!isLoading && !isError && filtered.length > 0 && (
          <div className="pl-list">
            {filtered.map((ticket, idx) => {
              const meta = categoryMeta[ticket.ticketCategory] ?? categoryMeta.VE_LUOT
              const info = getInfoGrid(ticket)
              const isPopular = idx === 0
              const isThang = ticket.ticketCategory === 'VE_THANG'

              return (
                <div className={`pl-ticket-card${isThang ? ' is-monthly' : ''}`} key={ticket.id}>
                  {/* Accent Top Border */}
                  <div
                    className="pl-card-accent-bar"
                    style={{ background: isThang ? 'linear-gradient(90deg, #0284c7, #38bdf8)' : 'linear-gradient(90deg, #0d9488, #2dd4bf)' }}
                  />

                  <div className="pl-ticket-content">
                    {/* Header Tags */}
                    <div className="pl-ticket-header">
                      <span
                        className="pl-category-pill"
                        style={{
                          color: meta.color,
                          backgroundColor: meta.bg,
                          borderColor: meta.border,
                        }}
                      >
                        <span className="pl-pill-icon">{meta.icon}</span>
                        {meta.label}
                      </span>

                      {isPopular && (
                        <span className="pl-popular-pill">
                          <span className="pl-popular-flame">🔥</span>
                          Phổ biến nhất
                        </span>
                      )}
                    </div>

                    {/* Ticket Name */}
                    <h2 className="pl-ticket-name">{ticket.name}</h2>

                    {/* Ticket Description */}
                    <p className="pl-ticket-desc">
                      {isThang
                        ? `Vé tháng ${ticket.durationDays || 30} ngày - Không giới hạn buổi bơi hàng ngày, tích hợp mã QR định danh bảo mật.`
                        : 'Vé bơi tiêu chuẩn lượt - Tự do bơi lội trong khung giờ mở cửa, sử dụng trọn vẹn tiện ích hồ bơi.'}
                    </p>

                    {/* Metadata Grid */}
                    <div className="pl-meta-grid">
                      {info.map((item, i) => (
                        <div className="pl-meta-item" key={i}>
                          <span className="pl-meta-icon">{item.icon}</span>
                          <div className="pl-meta-text">
                            <span className="pl-meta-label">{item.label}</span>
                            <span className="pl-meta-value">{item.value}</span>
                          </div>
                        </div>
                      ))}
                    </div>

                    {/* Card Footer: Price & CTA */}
                    <div className="pl-ticket-footer">
                      <div className="pl-price-block">
                        <span className="pl-price-num">
                          {new Intl.NumberFormat('vi-VN').format(ticket.price)}
                        </span>
                        <span className="pl-price-curr">đ</span>
                        <span className="pl-price-unit">
                          {isThang ? ` / ${ticket.durationDays || 30} ngày` : ' / lượt'}
                        </span>
                      </div>

                      <button
                        className="pl-btn-add-cart"
                        onClick={() => handleAddToCart(ticket)}
                        type="button"
                        aria-label={`Thêm vé ${ticket.name} vào giỏ hàng`}
                      >
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" width="16" height="16">
                          <circle cx="9" cy="21" r="1" />
                          <circle cx="20" cy="21" r="1" />
                          <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6" />
                        </svg>
                        <span>Thêm giỏ hàng</span>
                      </button>
                    </div>
                  </div>
                </div>
              )
            })}
          </div>
        )}

        {/* ── 3. Perks / Included Benefits Section ── */}
        <section className="pl-perks-section">
          <div className="pl-perks-header">
            <span className="pl-perks-badge">Tiện ích miễn phí</span>
            <h3 className="pl-perks-title">Quyền lợi đi kèm mọi loại vé tại Bể bơi Thành Minh</h3>
            <p className="pl-perks-subtitle">Trải nghiệm dịch vụ bơi lội chuẩn mực và tiện nghi trọn gói</p>
          </div>

          <div className="pl-perks-grid">
            <div className="pl-perk-card">
              <div className="pl-perk-icon-wrap icon-locker">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="22" height="22">
                  <rect x="3" y="11" width="18" height="11" rx="2" />
                  <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                </svg>
              </div>
              <div className="pl-perk-content">
                <h4>Tủ gửi đồ cá nhân miễn phí</h4>
                <p>Mỗi khách hàng được cấp tủ đồ cá nhân riêng có khóa an toàn trong suốt thời gian bơi lội.</p>
              </div>
            </div>

            <div className="pl-perk-card">
              <div className="pl-perk-icon-wrap icon-parking">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="22" height="22">
                  <circle cx="12" cy="12" r="10" />
                  <path d="M9 17V7h4a3 3 0 0 1 0 6H9" />
                </svg>
              </div>
              <div className="pl-perk-content">
                <h4>Bãi đỗ xe an ninh</h4>
                <p>Bãi đỗ xe rộng rãi dành cho cả xe máy và ô tô, có bảo vệ giám sát và phát vé trông giữ miễn phí.</p>
              </div>
            </div>

            <div className="pl-perk-card">
              <div className="pl-perk-icon-wrap icon-safety">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="22" height="22">
                  <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                  <path d="M9 12l2 2 4-4" />
                </svg>
              </div>
              <div className="pl-perk-content">
                <h4>Cứu hộ chuyên trách &amp; AI</h4>
                <p>Đội ngũ cứu hộ viên chứng chỉ quốc gia túc trực mép bể cùng hệ thống AI phát hiện nguy cơ tức thì.</p>
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  )
}
