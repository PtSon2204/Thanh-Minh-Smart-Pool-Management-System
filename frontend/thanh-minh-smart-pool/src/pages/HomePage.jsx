import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import './HomePage.css'

// Import service images
import imgFood from '../assets/service_food.png'
import imgShop from '../assets/service_shop.png'
import imgHotel from '../assets/service_hotel.png'
import imgLocker from '../assets/service_locker.png'

const promoItems = [
  {
    id: 1,
    tag: 'Khuyến mãi',
    emoji: '🎉',
    title: 'KHAI TRƯƠNG MÙA HÈ 2026 – VÉ ĐỒNG GIÁ CHỈ 99K',
    date: '01/06/2026',
    href: '/tin-tuc/khai-truong-2026',
  },
  {
    id: 2,
    tag: 'Sự kiện',
    emoji: '🌊',
    title: 'LỄ HỘI NƯỚC 2026 – TRẢI NGHIỆM CẢM GIÁC MẠNH TẠI HỒ BƠI',
    date: '15/06/2026',
    href: '/tin-tuc/le-hoi-nuoc',
  },
  {
    id: 3,
    tag: 'Thông báo',
    emoji: '📅',
    title: 'LỊCH HOẠT ĐỘNG THÁNG 9/2026 – THỜI GIAN MỞ CỬA CẬP NHẬT',
    date: '01/09/2026',
    href: '/tin-tuc/lich-hoat-dong',
  },
]

const services = [
  { img: imgLocker, title: 'Tủ để đồ', desc: 'Tủ để đồ cá nhân, an toàn và tiện lợi' },
  { img: imgHotel, title: 'Nhà khách', desc: 'Phòng nghỉ thoải mái, đầy đủ tiện nghi' },
  { img: imgShop, title: 'Đồ bơi – Tủ gửi đồ', desc: 'Cho thuê đồ bơi, tủ gửi đồ an toàn' },
  { img: imgFood, title: 'Du lịch', desc: 'Tour tham quan, dịch vụ du lịch trọn gói' },
]

const pricingPlans = [
  {
    icon: '👶',
    name: 'Trẻ em',
    desc: 'Dành cho trẻ dưới 10 tuổi',
    price: '80.000đ',
    unit: '/ lượt',
    features: ['Vào cổng tự do', 'Bể vầy trẻ em', 'Khu vui chơi trẻ nhỏ', 'An toàn có bảo vệ'],
  },
  {
    icon: '🧑',
    name: 'Người lớn',
    desc: 'Dành cho khách từ 10 tuổi',
    price: '150.000đ',
    unit: '/ lượt',
    featured: true,
    badge: 'Phổ biến nhất',
    features: ['Toàn bộ khu vực hồ bơi', 'Đường trượt nước', 'Bể sóng nhân tạo', 'Sông lười', 'Tủ gửi đồ miễn phí'],
  },
  {
    icon: '👨‍👩‍👧‍👦',
    name: 'Gia đình',
    desc: '2 người lớn + 2 trẻ em',
    price: '380.000đ',
    unit: '/ gói',
    features: ['Toàn bộ tiện ích', 'Ưu tiên vị trí', 'Khăn tắm miễn phí', 'Giảm 10% ẩm thực', 'Chỗ ngồi riêng'],
  },
]

export default function HomePage() {
  const [showScrollTop, setShowScrollTop] = useState(false)

  useEffect(() => {
    // Scroll to top button logic
    const handleScroll = () => setShowScrollTop(window.scrollY > 400)
    window.addEventListener('scroll', handleScroll)

    // Scroll animation logic (Intersection Observer)
    const observerCallback = (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('active')
          // Optional: Keep it visible once scrolled past, or let it fade out when scrolling up
          // observer.unobserve(entry.target) 
        }
      })
    }

    const observerOptions = {
      root: null,
      rootMargin: '0px',
      threshold: 0.15, // Trigger when 15% of the element is visible
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
    <main>
      {/* ===== HERO ===== */}
      <section className="hero">
        <div className="hero-waves">
          <svg className="wave wave1" viewBox="0 24 150 28" preserveAspectRatio="none">
            <defs>
              <path id="gentle-wave" d="M-160 44c30 0 58-18 88-18s 58 18 88 18 58-18 88-18 58 18 88 18 v44h-352z" />
            </defs>
            <use href="#gentle-wave" x="48" y="0" fill="rgba(254, 241, 230, 0.5)" />
            <use href="#gentle-wave" x="48" y="3" fill="rgba(254, 241, 230, 0.7)" />
            <use href="#gentle-wave" x="48" y="5" fill="#FEF1E6" />
          </svg>
        </div>
        <div className="hero-bubbles">
          <span /><span /><span /><span /><span /><span />
        </div>

        <div className="hero-content">
          <div className="container">
            {/* Text side */}
            <div className="hero-text">
              <div className="hero-badge">
                🏆 Mùa hè giải nhiệt
              </div>
              <h1 className="hero-title">
                <div style={{ whiteSpace: 'nowrap' }}>Bể bơi <span>Thành Minh</span></div>
                <div style={{ whiteSpace: 'nowrap' }}>Kính chào quý khách!</div>
              </h1>
              <p className="hero-desc">
                Trải nghiệm hồ bơi thông minh với hệ thống quản lý hiện đại —
                đặt vé online 
                và tận hưởng không gian vui chơi mát mẻ ngay giữa lòng thành phố.
              </p>
              <div className="hero-actions">
                <Link to="/dat-ve" className="btn-primary">
                  🎟️ Đặt vé ngay
                </Link>
                <Link to="/bang-gia" className="btn-outline">
                  📋 Xem bảng giá
                </Link>
              </div>
              
            </div>

            {/* Visual card */}
            <div className="hero-visual">
              <div className="hero-card-main">
                <div className="hero-card-title">HỆ THỐNG QUẢN LÝ</div>
                <div className="hero-card-pool">🏊 Hồ bơi thông minh</div>

                <div className="hero-pool-stats">
                  <div className="hero-pool-stat">
                    <div className="hero-pool-stat-val">142</div>
                    <div className="hero-pool-stat-label">Khách hiện tại</div>
                  </div>
                </div>

                <div className="hero-card-status">
                  <span className="status-dot" />
                  Đang hoạt động bình thường
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ===== KHUYẾN MÃI – SỰ KIỆN ===== */}
      <section className="section-promo">
        <div className="container">
          <div className="section-header reveal">
            <h2 className="section-title">Khuyến mãi – Sự kiện</h2>
            <p className="section-subtitle">Cập nhật những ưu đãi và sự kiện hấp dẫn mới nhất</p>
          </div>
          <div className="promo-grid">
            {promoItems.map((item, idx) => (
              <div key={item.id} className="promo-card reveal" style={{ transitionDelay: `${idx * 150}ms` }}>
                <div className="promo-card-img">{item.emoji}</div>
                <div className="promo-card-body">
                  <span className="promo-card-tag">{item.tag}</span>
                  <h3 className="promo-card-title">{item.title}</h3>
                  <p className="promo-card-date">📅 {item.date}</p>
                  <Link to={item.href} className="promo-card-link">
                    Xem thêm →
                  </Link>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ===== DỊCH VỤ ===== */}
      <section className="section-services">
        <div className="container">
          <div className="section-header reveal">
            <h2 className="section-title">Dịch vụ tại Thanh Minh</h2>
            <p className="section-subtitle">Đa dạng dịch vụ tiện ích phục vụ trọn vẹn chuyến thăm của bạn</p>
          </div>
        </div>
        
        {/* Infinite Marquee Slider */}
        <div className="services-marquee reveal">
          <div className="services-marquee-track">
            {/* Render original list */}
            {services.map((item, i) => (
              <div key={`s1-${i}`} className="service-card">
                <img src={item.img} alt={item.title} className="service-img" />
                <div className="service-content">
                  <div className="service-title">{item.title}</div>
                  <div className="service-desc">{item.desc}</div>
                </div>
              </div>
            ))}
            {/* Duplicate list for infinite loop effect */}
            {services.map((item, i) => (
              <div key={`s2-${i}`} className="service-card">
                <img src={item.img} alt={item.title} className="service-img" />
                <div className="service-content">
                  <div className="service-title">{item.title}</div>
                  <div className="service-desc">{item.desc}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ===== CTA BANNER ===== */}
      <section className="section-cta">
        <div className="container">
          <div className="cta-content reveal">
            <h2 className="cta-title">🌊 Sẵn sàng cho một ngày vui trọn vẹn?</h2>
            <p className="cta-desc">
              Đặt vé ngay hôm nay để đảm bảo chỗ và nhận ưu đãi tốt nhất.
              Hệ thống đặt vé online hoạt động 24/7 — nhanh chóng, tiện lợi, an toàn.
            </p>
            <div className="cta-actions">
              <Link to="/dat-ve" className="btn-white">
                🎟️ Đặt vé online ngay
              </Link>
              <Link to="/bang-gia" className="btn-outline">
                📋 Xem bảng giá
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* ===== BẢNG GIÁ ===== */}
      <section className="section-pricing">
        <div className="container">
          <div className="section-header reveal" style={{ textAlign: 'center' }}>
            <h2 className="section-title">Bảng giá vé</h2>
            <p className="section-subtitle">Giá vé rõ ràng, minh bạch — phù hợp mọi đối tượng</p>
          </div>
          <div className="pricing-cards">
            {pricingPlans.map((plan, i) => (
              <div key={i} className={`pricing-card reveal${plan.featured ? ' featured' : ''}`} style={{ transitionDelay: `${i * 150}ms` }}>
                {plan.badge && <div className="pricing-badge">{plan.badge}</div>}
                <div className="pricing-card-icon">{plan.icon}</div>
                <div className="pricing-card-name">{plan.name}</div>
                <div className="pricing-card-desc">{plan.desc}</div>
                <div className="pricing-price">{plan.price}</div>
                <div className="pricing-unit">{plan.unit}</div>
                <ul className="pricing-features">
                  {plan.features.map((f, j) => <li key={j}>{f}</li>)}
                </ul>
                <Link to="/dang-ky" className="pricing-btn">
                  Đăng ký 
                </Link>
                <Link to="/dang-nhap" className="pricing-btn" style={{ marginTop: '10px' }}>
                  Đăng nhập
                </Link>
              </div>
            ))}
          </div>
          <div className="reveal" style={{ textAlign: 'center', transitionDelay: '450ms' }}>
            <Link to="/bang-gia" className="btn-primary">
              Xem bảng giá đầy đủ →
            </Link>
          </div>
        </div>
      </section>

      {/* Scroll to top */}
      {showScrollTop && (
        <button className="scroll-top-btn" onClick={scrollToTop} aria-label="Scroll to top">
          ↑
        </button>
      )}
    </main>
  )
}