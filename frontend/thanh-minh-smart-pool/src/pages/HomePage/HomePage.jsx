import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import './HomePage.css'
import { useQuery } from '@tanstack/react-query'
import poolAccessService from '../../features/tickets/services/poolAccessService'
import AmbientHeroBg from '../../components/shared/AmbientHeroBg'

// Import service images
import imgLocker from '../../assets/tu-do-mien-phi.png'
import imgShop from '../../assets/dich-vu-cho-thue.png'

// Import why-us images
import whyImg1 from '../../assets/tai-sao-anh-1-dochothue.png'
import whyImg2 from '../../assets/tai-sao-anh-2-camera-giam-sat.png'
import whyImg3 from '../../assets/tai-sao-anh-3-tudedofree.png'


const whyUsItems = [
  {
    id: 1,
    title: 'Giá cả minh bạch & Hợp lý',
    tag: '01 ✦ TỐI ƯU CHI PHÍ',
    desc: 'Bảng giá được niêm yết công khai rõ ràng trên toàn hệ thống, tuyệt đối không có chi phí ẩn. Bạn hoàn toàn an tâm trải nghiệm dịch vụ với mức phí xứng đáng nhất.',
    features: [
      'Bảng giá niêm yết minh bạch, đa dạng gói vé',
      'Linh hoạt lựa chọn vé lượt hoặc vé tháng tiết kiệm',
      'Thanh toán online tiện lợi, nhận mã QR tức thì',
    ],
    highlightStats: [
      { label: 'Không phụ phí ẩn', value: '100%' },
      { label: 'Gói vé linh hoạt', value: 'Vé lượt / Tháng' },
      { label: 'Vào cổng nhanh', value: 'Mã QR' },
    ],
    img: null,
  },
  {
    id: 2,
    title: 'Tủ đồ cá nhân hoàn toàn miễn phí',
    tag: '02 ✦ AN TOÀN & TIỆN NGHI',
    desc: 'Hệ thống tủ khóa cá nhân hiện đại, phân khu thông minh, sạch sẽ và rộng rãi. Khách hàng được sử dụng miễn phí 100%, an tâm tuyệt đối về tư trang cá nhân.',
    features: [
      'Tủ đồ cá nhân riêng tư, bảo mật',
      'Không tính thêm bất kỳ phụ phí bảo quản nào',
      'Khu vực gửi đồ có nhân viên túc trực hỗ trợ',
    ],
    img: whyImg3,
  },
  {
    id: 3,
    title: 'Camera AI giám sát an toàn 24/7',
    tag: '03 ✦ CÔNG NGHỆ ĐỘT PHÁ',
    desc: 'Thành Minh tiên phong ứng dụng trí tuệ nhân tạo (AI) vào hệ thống camera an toàn. Thuật toán nhận diện tự động phát hiện sớm nguy cơ đuối nước hoặc sự cố bất thường để cứu hộ xử lý tức thì.',
    features: [
      'Cảnh báo sớm nguy cơ mất an toàn dưới nước',
      'Giám sát bao quát toàn bộ diện tích mặt nước 24/7',
      'Đội ngũ cứu hộ chuyên nghiệp phối hợp cùng AI',
    ],
    img: whyImg2,
  },
  {
    id: 4,
    title: 'Dịch vụ cho thuê phụ kiện tiện ích',
    tag: '04 ✦ TRANG BỊ ĐẦY ĐỦ',
    desc: 'Bạn không cần lo lắng nếu quên mang theo phụ kiện bơi. Chúng tôi cung cấp dịch vụ cho thuê kính bơi, phao bơi, mũ bơi và khăn tắm sạch sẽ, được khử trùng sau mỗi lần sử dụng.',
    features: [
      'Trang bị chính hãng, đa dạng lứa tuổi và kích cỡ',
      'Tiệt trùng vệ sinh nghiêm ngặt sau mỗi lượt sử dụng',
      'Chi phí thuê hợp lý, phục vụ nhanh chóng tại chỗ',
    ],
    img: whyImg1,
  },
]

const services = [
  { img: imgLocker, title: 'Tủ đồ thông minh miễn phí', desc: 'Tủ khóa cá nhân an toàn, riêng tư và hoàn toàn miễn phí cho mọi khách hàng.' },
  { img: imgShop, title: 'Cho thuê phụ kiện bơi lội', desc: 'Đầy đủ phao bơi, kính bơi, khăn tắm chất lượng cao đã tiệt trùng.' },
]

export default function HomePage() {
  const [showScrollTop, setShowScrollTop] = useState(false)

  const { data: liveStatus, isLoading: loadingLiveStatus } = useQuery({
    queryKey: ['poolLiveStatus'],
    queryFn: poolAccessService.getLiveStatus,
    refetchInterval: 15000,
    staleTime: 5000,
  })

  const currentSwimmers = liveStatus?.currentSwimmers ?? 0
  const maxCapacity = liveStatus?.maxCapacity ?? 100
  const isMaintenance = liveStatus?.operationalStatus === 'Maintenance'
  const isCrowded = liveStatus?.isCrowded ?? false
  const statusReason = liveStatus?.statusReason
  const currentShiftName = liveStatus?.currentShiftName || 'Đang mở cửa'

  useEffect(() => {
    const handleScroll = () => setShowScrollTop(window.scrollY > 400)
    window.addEventListener('scroll', handleScroll)

    const observerCallback = (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('active')
        }
      })
    }

    const observerOptions = {
      root: null,
      rootMargin: '0px',
      threshold: 0.12,
    }

    const observer = new IntersectionObserver(observerCallback, observerOptions)
    const revealElements = document.querySelectorAll('.reveal')
    revealElements.forEach((el) => observer.observe(el))

    return () => {
      window.removeEventListener('scroll', handleScroll)
      revealElements.forEach((el) => observer.unobserve(el))
    }
  }, [])

  const scrollToTop = () => window.scrollTo({ top: 0, behavior: 'smooth' })

  return (
    <main className="home-page">
      {/* ===== HERO ===== */}
      <section className="hero">
        <AmbientHeroBg />

        {/* Waves transitioning smoothly into --bg (#f8fafc) */}
        <div className="hero-waves" aria-hidden="true">
          <svg className="wave wave1" viewBox="0 24 150 28" preserveAspectRatio="none">
            <defs>
              <path id="gentle-wave" d="M-160 44c30 0 58-18 88-18s 58 18 88 18 58-18 88-18 58 18 88 18 v44h-352z" />
            </defs>
            <use href="#gentle-wave" x="48" y="0" fill="rgba(248, 250, 252, 0.4)" />
            <use href="#gentle-wave" x="48" y="3" fill="rgba(248, 250, 252, 0.75)" />
            <use href="#gentle-wave" x="48" y="5" fill="#f8fafc" />
          </svg>
        </div>

        <div className="hero-content">
          <div className="container">
            {isMaintenance && (
              <div
                style={{
                  background: '#fef2f2',
                  border: '1px solid #f87171',
                  borderRadius: 12,
                  padding: '16px 20px',
                  marginBottom: 24,
                  display: 'flex',
                  alignItems: 'center',
                  gap: 14,
                  boxShadow: '0 4px 14px rgba(239, 68, 68, 0.15)',
                  animation: 'fadeIn 0.3s ease-in-out',
                }}
              >
                <span style={{ fontSize: 32 }}>⚠️</span>
                <div>
                  <div style={{ fontWeight: 700, color: '#b91c1c', fontSize: 16 }}>
                    THÔNG BÁO: BỂ BƠI TẠM DỪNG HOẠT ĐỘNG ĐỂ BẢO TRÌ THIẾT BỊ AN TOÀN
                  </div>
                  <div style={{ color: '#7f1d1d', fontSize: 13, marginTop: 4, lineHeight: 1.5 }}>
                    {statusReason ||
                      'Hệ thống đang tiến hành bảo dưỡng thiết bị an toàn kỹ thuật theo quy định. Quý khách vui lòng tạm hoãn đặt vé cho đến khi có thông báo mở cửa trở lại.'}
                  </div>
                </div>
              </div>
            )}

            {/* Text side */}
            <div className="hero-text">
              <div className="hero-badge">
                <span className="hero-badge-pulse" />
                Bể bơi Thông minh
              </div>
              <h1 className="hero-title">
                <span className="hero-title-line">Bể bơi <span className="hero-title-brand">Thành Minh</span></span>
                <span className="hero-title-line">kính chào quý khách!</span>
              </h1>
              <p className="hero-desc">
                Trải nghiệm hồ bơi chuẩn chất lượng với công nghệ camera AI cảnh báo sớm,
                đặt vé trực tuyến tiện lợi và tận hưởng làn nước mát lành giữa lòng thành phố.
              </p>
              <div className="hero-actions">
                <Link to="/bang-gia" className="btn-primary hero-btn-main">
                  <span>Xem bảng giá vé</span>
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <line x1="5" y1="12" x2="19" y2="12" />
                    <polyline points="12 5 19 12 12 19" />
                  </svg>
                </Link>
                <Link to="/gioi-thieu" className="btn-outline">
                  Tìm hiểu thêm
                </Link>
              </div>
            </div>

            {/* Visual Glassmorphic Live Card */}
            <div className="hero-visual">
              <div className="hero-live-card">
                <div className="live-card-header">
                  <div className="live-indicator">
                    <span
                      className="live-dot"
                      style={{
                        backgroundColor: isMaintenance ? '#ef4444' : isCrowded ? '#f59e0b' : '#10b981',
                      }}
                    />
                    <span className="live-text">
                      {isMaintenance ? 'Đang bảo trì' : currentShiftName}
                    </span>
                  </div>
                  {isCrowded && (
                    <span
                      style={{
                        fontSize: 11,
                        background: '#fef3c7',
                        color: '#b45309',
                        padding: '2px 8px',
                        borderRadius: 99,
                        fontWeight: 600,
                      }}
                    >
                      Đang đông
                    </span>
                  )}
                  <span className="live-badge-smart">AI Smart Pool</span>
                </div>

                <div className="live-card-body">
                  <div className="live-metric-title">Số khách trong bể</div>
                  <div className="live-metric-value">
                    {loadingLiveStatus ? (
                      <span className="metric-loading">...</span>
                    ) : (
                      <>
                        <span>{currentSwimmers}</span>
                      </>
                    )}
                    <span className="live-metric-unit">người</span>
                  </div>
                </div>

                <div className="live-card-grid">
                  <div className="live-grid-item">
                    <span className="grid-item-label">Ca sáng</span>
                    <span className="grid-item-val">06:00 – 11:30</span>
                  </div>
                  <div className="live-grid-item">
                    <span className="grid-item-label">Ca chiều</span>
                    <span className="grid-item-val">13:30 – 19:30</span>
                  </div>
                </div>

                <div className="live-card-footer">
                  <Link to="/bang-gia" className="live-footer-link">
                    Đặt vé bơi trực tuyến ngay →
                  </Link>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ===== TẠI SAO CHỌN THÀNH MINH ===== */}
      <section className="section-why-us">
        <div className="container">
          <div className="section-header reveal" style={{ textAlign: 'center' }}>
            <div className="why-us-badge">✨</div>
            <h2 className="section-title">Tại sao nên chọn Bể bơi Thành Minh?</h2>
          </div>

          <div className="why-us-list">
            {whyUsItems.map((item, i) => {
              const isReverse = i % 2 !== 0
              const hasImage = !!item.img

              if (!hasImage) {
                // Card 1: Giá cả minh bạch (Full width featured showcase)
                return (
                  <div key={item.id} className="why-us-card feature-banner reveal">
                    <div className="feature-banner-content">
                      <div className="why-us-tag">{item.tag}</div>
                      <h3 className="why-us-title">{item.title}</h3>
                      <p className="why-us-desc">{item.desc}</p>
                      <ul className="why-us-features">
                        {item.features.map((f, j) => (
                          <li key={j}>
                            <span className="check-icon">✓</span>
                            <span>{f}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                    {item.highlightStats && (
                      <div className="feature-banner-stats">
                        {item.highlightStats.map((st, k) => (
                          <div key={k} className="banner-stat-box">
                            <span className="stat-box-val">{st.value}</span>
                            <span className="stat-box-lbl">{st.label}</span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )
              }

              return (
                <div key={item.id} className={`why-us-card reveal ${isReverse ? 'row-reverse' : ''}`}>
                  <div className="why-us-img-wrapper">
                    <img src={item.img} alt={item.title} className="why-us-img" loading="lazy" />
                  </div>
                  
                  <div className="why-us-content">
                    <div className="why-us-tag">{item.tag}</div>
                    <h3 className="why-us-title">{item.title}</h3>
                    <p className="why-us-desc">{item.desc}</p>
                    <ul className="why-us-features">
                      {item.features.map((f, j) => (
                        <li key={j}>
                          <span className="check-icon">✓</span>
                          <span>{f}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      </section>

      {/* ===== CTA BANNER ===== */}
      <section className="section-cta">
        <div className="container">
          <div className="cta-card reveal">
            <div className="cta-content">
              <span className="cta-badge">🌟 Trải nghiệm bơi lội tuyệt vời</span>
              <h2 className="cta-title">Sẵn sàng đắm mình trong làn nước mát?</h2>
              <p className="cta-desc">
                Đặt vé bơi online trong 30 giây. Nhận mã QR vào cổng trực tiếp nhanh chóng mà không cần xếp hàng chờ đợi.
              </p>
              <div className="cta-actions">
                <Link to="/bang-gia" className="btn-primary cta-btn-white">
                  <span>Khám phá bảng giá vé</span>
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <line x1="5" y1="12" x2="19" y2="12" />
                    <polyline points="12 5 19 12 12 19" />
                  </svg>
                </Link>
                <Link to="/lien-he" className="btn-outline">
                  Tư vấn &amp; Hỗ trợ
                </Link>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ===== DỊCH VỤ TIỆN ÍCH ===== */}
      <section className="section-services">
        <div className="container">
          <div className="section-header reveal" style={{ textAlign: 'center' }}>
            <div className="why-us-badge">🎯</div>
            <h2 className="section-title">Dịch vụ tại Thành Minh</h2>
          </div>
        </div>
        
        {/* Infinite Marquee Slider with clean fade masks */}
        <div className="services-marquee reveal">
          <div className="services-marquee-track">
            {/* Group 1 */}
            <div className="marquee-group">
              {[...services, ...services, ...services].map((item, i) => (
                <div key={`s1-${i}`} className="service-card">
                  <div className="service-img-wrapper">
                    <img src={item.img} alt={item.title} className="service-img" loading="lazy" />
                  </div>
                  <div className="service-content">
                    <span className="service-tag">Dịch vụ tiện ích</span>
                    <h3 className="service-title">{item.title}</h3>
                    <p className="service-desc">{item.desc}</p>
                  </div>
                </div>
              ))}
            </div>
            {/* Group 2 (Duplicate for continuous loop) */}
            <div className="marquee-group" aria-hidden="true">
              {[...services, ...services, ...services].map((item, i) => (
                <div key={`s2-${i}`} className="service-card">
                  <div className="service-img-wrapper">
                    <img src={item.img} alt={item.title} className="service-img" loading="lazy" />
                  </div>
                  <div className="service-content">
                    <span className="service-tag">Dịch vụ tiện ích</span>
                    <h3 className="service-title">{item.title}</h3>
                    <p className="service-desc">{item.desc}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* Floating Scroll to Top Button */}
      {showScrollTop && (
        <button className="scroll-top-btn" onClick={scrollToTop} aria-label="Lên đầu trang">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="18 15 12 9 6 15" />
          </svg>
        </button>
      )}
    </main>
  )
}
