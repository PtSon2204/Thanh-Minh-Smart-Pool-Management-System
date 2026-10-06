import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import './HomePage.css'

// Import service images
import imgLocker from '../../assets/tu-do-mien-phi.png'
import imgShop from '../../assets/dich-vu-cho-thue.png'
import imgFood from '../../assets/dich-vu-am-thuc.png'

// Import why-us images
import whyImg1 from '../../assets/tai-sao-anh-1-dochothue.png'
import whyImg2 from '../../assets/tai-sao-anh-2-camera-giam-sat.png'
import whyImg3 from '../../assets/tai-sao-anh-3-tudedofree.png'
import whyImg4 from '../../assets/tai-sao-anh-4-doannhanh.png'

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

const whyUsItems = [
  {
    id: 1,
    title: 'Giá cả minh bạch',
    tag: '01 ✦ TỐI ƯU CHI PHÍ',
    desc: 'Bảng giá được niêm yết rõ ràng, không có phụ phí ẩn. Bạn hoàn toàn yên tâm tận hưởng dịch vụ với mức chi phí hợp lý và xứng đáng nhất.',
    features: ['Giá niêm yết công khai trên toàn hệ thống', 'Đa dạng gói vé theo nhu cầu (vé lẻ, vé tháng)'],
    img: null,
  },
  {
    id: 2,
    title: 'Tủ đồ cá nhân miễn phí',
    tag: '02 ✦ AN TOÀN & TIỆN LỢI',
    desc: 'Hệ thống tủ đồ cá nhân hiện đại, không gian rộng rãi và hoàn toàn miễn phí, giúp bạn an tâm tuyệt đối khi bảo quản tư trang trong suốt quá trình vui chơi.',
    features: ['Tủ khóa an toàn, riêng tư', 'Không phát sinh phụ phí bảo quản đồ'],
    img: whyImg3,
  },
  {
    id: 3,
    title: 'Đồ ăn nhanh tiện lợi',
    tag: '03 ✦ NẠP NĂNG LƯỢNG',
    desc: 'Quầy ẩm thực ngay tại khuôn viên cung cấp đa dạng các món ăn vặt, xúc xích, bim bim và nước giải khát, giúp bạn nạp lại năng lượng tức thì sau những giờ bơi lội.',
    features: ['Thực đơn phong phú, đa dạng lựa chọn', 'Phục vụ nhanh chóng, hợp vệ sinh'],
    img: whyImg4,
  },
  {
    id: 4,
    title: 'Camera AI giám sát an toàn',
    tag: '04 ✦ CÔNG NGHỆ TIÊN PHONG',
    desc: 'Thành Minh tự hào ứng dụng công nghệ Trí tuệ nhân tạo (AI) vào hệ thống camera giám sát. Hệ thống liên tục quét và tự động phát hiện, cảnh báo ngay lập tức các nguy cơ mất an toàn.',
    features: ['Hỗ trợ phát hiện nguy cơ đuối nước sớm', 'Giám sát 24/7 bao quát toàn bộ khu vực'],
    img: whyImg2,
  },
  {
    id: 5,
    title: 'Dịch vụ cho thuê phụ kiện',
    tag: '05 ✦ TRANG BỊ ĐẦY ĐỦ',
    desc: 'Quên mang theo phụ kiện bơi lội? Đừng lo! Chúng tôi cung cấp dịch vụ cho thuê phao bơi, kính bơi, khăn tắm sạch sẽ, chất lượng cao dành cho mọi lứa tuổi.',
    features: ['Đa dạng mẫu mã và kích cỡ', 'Vệ sinh tiệt trùng cẩn thận sau mỗi lần thuê'],
    img: whyImg1,
  },
]

const services = [
  { img: imgLocker, title: 'Tủ đồ miễn phí', desc: 'Tủ đồ cá nhân an toàn, miễn phí cho mọi khách hàng' },
  { img: imgShop, title: 'Cho thuê phụ kiện', desc: 'Cung cấp đầy đủ phao, kính bơi, khăn tắm...' },
  { img: imgFood, title: 'Đồ ăn nhanh', desc: 'Phục vụ đa dạng: bim bim, nước giải khát, xúc xích...' },
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
        <video 
          className="hero-video-bg"
          autoPlay 
          loop 
          muted 
          playsInline 
          disableRemotePlayback
          preload="auto"
        >
          <source src="https://d8j0ntlcm91z4.cloudfront.net/user_38xzZboKViGWJOttwIXH07lWA1P/hf_20260424_064411_9e9d7f84-9277-41f4-ab10-59172d89e6be.mp4" type="video/mp4" />
        </video>
        <div className="hero-video-overlay"></div>

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
                <div className="hero-card-pool">Hồ bơi thông minh</div>

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

      {/* ===== TẠI SAO CHỌN THÀNH MINH ===== */}
      <section className="section-why-us">
        <div className="container">
          <div className="section-header reveal" style={{ textAlign: 'center', marginBottom: '60px' }}>
            <div style={{ marginBottom: '24px' }}>
              <div className="why-us-badge" style={{ marginBottom: 0 }}>✨ GIÁ TRỊ TỐT NHẤT DÀNH CHO BẠN</div>
            </div>
            <h2 className="section-title">Tại sao lại chọn <span style={{ color: 'var(--accent)', fontStyle: 'italic', fontFamily: 'serif' }}>Bể bơi Thành Minh</span>?</h2>
            <p className="section-subtitle">5 tiêu chuẩn hàng đầu giúp chúng tôi mang đến trải nghiệm bơi lội tuyệt vời nhất</p>
          </div>

          <div className="why-us-list">
            {whyUsItems.map((item, i) => {
              // Item 2 (i=1): Image Left (row)
              // Item 3 (i=2): Image Right (row-reverse)
              const isReverse = i % 2 === 0 && i !== 0; 
              const hasImage = !!item.img;

              return (
                <div key={item.id} className={`why-us-card reveal ${isReverse ? 'row-reverse' : ''} ${!hasImage ? 'no-image' : ''}`}>
                  {hasImage && (
                    <div className="why-us-img-wrapper">
                      <img src={item.img} alt={item.title} className="why-us-img" />
                    </div>
                  )}
                  
                  <div className="why-us-content">
                    <div className="why-us-tag">
                      {item.tag}
                    </div>
                    <h3 className="why-us-title">{item.title}</h3>
                    <p className="why-us-desc">{item.desc}</p>
                    <ul className="why-us-features">
                      {item.features.map((f, j) => (
                        <li key={j}>
                          <span className="check-icon">✓</span> {f}
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>
              );
            })}
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
              <Link to="/dat-ve" className="btn-primary">
                🎟️ Đặt vé online ngay
              </Link>
              <Link to="/bang-gia" className="btn-outline">
                📋 Xem bảng giá
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* ===== KHUYẾN MÃI – SỰ KIỆN ===== */}
      <section className="section-promo">
        <div className="container">
          <div className="section-header reveal" style={{ textAlign: 'center' }}>
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
          <div className="section-header reveal" style={{ textAlign: 'center' }}>
            <h2 className="section-title">Dịch vụ tại Thanh Minh</h2>
            <p className="section-subtitle">Đa dạng dịch vụ tiện ích phục vụ trọn vẹn chuyến thăm của bạn</p>
          </div>
        </div>
        
        {/* Infinite Marquee Slider */}
        <div className="services-marquee reveal">
          <div className="services-marquee-track">
            {/* Group 1 */}
            <div className="marquee-group">
              {[...services, ...services, ...services].map((item, i) => (
                <div key={`s1-${i}`} className="service-card">
                  <div className="service-img-wrapper">
                    <img src={item.img} alt={item.title} className="service-img" />
                  </div>
                  <div className="service-content">
                    <div className="service-title">{item.title}</div>
                    <div className="service-desc">{item.desc}</div>
                    <div className="service-tag">Dịch vụ tiện ích</div>
                  </div>
                </div>
              ))}
            </div>
            {/* Group 2 (Duplicate) */}
            <div className="marquee-group" aria-hidden="true">
              {[...services, ...services, ...services].map((item, i) => (
                <div key={`s2-${i}`} className="service-card">
                  <div className="service-img-wrapper">
                    <img src={item.img} alt={item.title} className="service-img" />
                  </div>
                  <div className="service-content">
                    <div className="service-title">{item.title}</div>
                    <div className="service-desc">{item.desc}</div>
                    <div className="service-tag">Dịch vụ tiện ích</div>
                  </div>
                </div>
              ))}
            </div>
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