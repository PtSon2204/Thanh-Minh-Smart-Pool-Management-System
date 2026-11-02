import './ContactPage.css'
import { useState, useEffect } from 'react'
import AmbientHeroBg from '../../components/shared/AmbientHeroBg'

export default function ContactPage() {
  const [form, setForm] = useState({ name: '', email: '', phone: '', subject: 'dat-ve', message: '' })
  const [errors, setErrors] = useState({})
  const [submitting, setSubmitting] = useState(false)
  const [submitted, setSubmitted] = useState(false)

  const validate = () => {
    const errs = {}
    if (!form.name.trim()) errs.name = 'Vui lòng nhập họ và tên'
    if (!form.email.trim()) errs.email = 'Vui lòng nhập địa chỉ email'
    else if (!/^[^@]+@[^@]+\.[^@]+$/.test(form.email)) errs.email = 'Email không hợp lệ'
    if (!form.message.trim()) errs.message = 'Vui lòng nhập nội dung tin nhắn'
    return errs
  }

  const handleSubmit = (e) => {
    e.preventDefault()
    const errs = validate()
    if (Object.keys(errs).length > 0) {
      setErrors(errs)
      return
    }
    setSubmitting(true)
    setTimeout(() => {
      setSubmitted(true)
      setSubmitting(false)
    }, 1200)
  }

  const handleChange = (e) => {
    setForm(prev => ({ ...prev, [e.target.name]: e.target.value }))
    setErrors(prev => ({ ...prev, [e.target.name]: '' }))
  }

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('active')
          }
        })
      },
      { threshold: 0.1 }
    )
    const elements = document.querySelectorAll('.reveal')
    elements.forEach((el) => observer.observe(el))
    return () => observer.disconnect()
  }, [])

  return (
    <div className="contact-page">
      {/* ── 1. HERO ── */}
      <section className="contact-hero">
        <AmbientHeroBg />
        
        {/* Waves connecting seamlessly to #f8fafc */}
        <div className="contact-hero-waves" aria-hidden="true">
          <div className="chw-track chw1">
            <svg viewBox="0 0 1440 90" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,45 C240,90 480,0 720,45 C960,90 1200,0 1440,45 L1440,90 L0,90 Z" fill="rgba(248, 250, 252, 0.4)"/>
            </svg>
            <svg viewBox="0 0 1440 90" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,45 C240,90 480,0 720,45 C960,90 1200,0 1440,45 L1440,90 L0,90 Z" fill="rgba(248, 250, 252, 0.4)"/>
            </svg>
          </div>
          <div className="chw-track chw2">
            <svg viewBox="0 0 1440 90" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,65 C360,15 720,90 1080,45 C1260,22 1380,70 1440,55 L1440,90 L0,90 Z" fill="#f8fafc"/>
            </svg>
            <svg viewBox="0 0 1440 90" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,65 C360,15 720,90 1080,45 C1260,22 1380,70 1440,55 L1440,90 L0,90 Z" fill="#f8fafc"/>
            </svg>
          </div>
        </div>

        <div className="contact-hero-content">
          <div className="contact-hero-badge">
            💬 Hỗ trợ khách hàng
          </div>
          <h1>Liên hệ với chúng tôi</h1>
          <p>Đội ngũ Bể bơi Thành Minh luôn sẵn sàng đồng hành, giải đáp thắc mắc và tiếp nhận mọi phản hồi của bạn.</p>
        </div>
      </section>

      {/* ── 2. MAIN CONTACT SECTION ── */}
      <section className="contact-main">
        <div className="container">
          <div className="contact-grid reveal">
            {/* Left Column - Contact Info */}
            <div className="contact-info">
              <span className="section-kicker">✦ KẾT NỐI VỚI CHÚNG TÔI</span>
              <h2>Thông tin liên lạc</h2>
              <p className="contact-info-desc">
                Quý khách có thể ghé trực tiếp hồ bơi hoặc kết nối nhanh chóng qua các kênh liên lạc dưới đây:
              </p>

              <div className="info-cards-list">
                <div className="info-card">
                  <div className="info-icon-box">📍</div>
                  <div className="info-text">
                    <h4>Địa chỉ hồ bơi</h4>
                    <p>Khu Thể thao Bể bơi Thành Minh, Đông Anh, Hà Nội</p>
                  </div>
                </div>

                <div className="info-card">
                  <div className="info-icon-box">📞</div>
                  <div className="info-text">
                    <h4>Hotline &amp; Zalo</h4>
                    <p className="highlight-text">0901 234 567</p>
                  </div>
                </div>

                <div className="info-card">
                  <div className="info-icon-box">✉️</div>
                  <div className="info-text">
                    <h4>Email hỗ trợ</h4>
                    <p>contact@thanhminh-pool.vn</p>
                  </div>
                </div>

                <div className="info-card">
                  <div className="info-icon-box">🕐</div>
                  <div className="info-text">
                    <h4>Giờ mở cửa đón khách</h4>
                    <p>
                      <strong>Ca sáng:</strong> 06:00 – 11:30 <br />
                      <strong>Ca chiều:</strong> 13:30 – 19:30 
                    </p>
                  </div>
                </div>
              </div>

              {/* Social Channels */}
              <div className="social-section">
                <h4>Kênh mạng xã hội</h4>
                <div className="social-links">
                  <a href="https://facebook.com" target="_blank" rel="noreferrer" className="social-pill">
                    <span>📘</span> Facebook
                  </a>
                  <a href="https://zalo.me" target="_blank" rel="noreferrer" className="social-pill">
                    <span>💬</span> Zalo OA
                  </a>
                  <a href="https://instagram.com" target="_blank" rel="noreferrer" className="social-pill">
                    <span>📷</span> Instagram
                  </a>
                </div>
              </div>
            </div>

            {/* Right Column - Contact Form */}
            <div className="contact-form-wrapper">
              <div className="form-header">
                <h3>Gửi tin nhắn cho chúng tôi</h3>
                <p>Điền thông tin vào mẫu bên dưới, chúng tôi sẽ phản hồi trong vòng 24 giờ làm việc.</p>
              </div>

              {submitted ? (
                <div className="success-msg">
                  <div className="success-icon">✓</div>
                  <h4>Gửi tin nhắn thành công!</h4>
                  <p>Cảm ơn bạn đã liên hệ. Đội ngũ chăm sóc khách hàng của Thành Minh sẽ liên lạc lại trong thời gian sớm nhất.</p>
                  <button 
                    type="button" 
                    className="btn-primary" 
                    style={{ marginTop: '20px' }}
                    onClick={() => {
                      setSubmitted(false)
                      setForm({ name: '', email: '', phone: '', subject: 'dat-ve', message: '' })
                    }}
                  >
                    Gửi tin nhắn khác
                  </button>
                </div>
              ) : (
                <form onSubmit={handleSubmit} noValidate>
                  <div className="form-group">
                    <label htmlFor="name">Họ và tên <span className="req">*</span></label>
                    <input
                      type="text"
                      id="name"
                      name="name"
                      value={form.name}
                      onChange={handleChange}
                      placeholder="Ví dụ: Nguyễn Văn A"
                      className={errors.name ? 'input-error' : ''}
                    />
                    {errors.name && <p className="error-text">{errors.name}</p>}
                  </div>

                  <div className="form-row-2">
                    <div className="form-group">
                      <label htmlFor="email">Email liên hệ <span className="req">*</span></label>
                      <input
                        type="email"
                        id="email"
                        name="email"
                        value={form.email}
                        onChange={handleChange}
                        placeholder="email@example.com"
                        className={errors.email ? 'input-error' : ''}
                      />
                      {errors.email && <p className="error-text">{errors.email}</p>}
                    </div>

                    <div className="form-group">
                      <label htmlFor="phone">Số điện thoại</label>
                      <input
                        type="tel"
                        id="phone"
                        name="phone"
                        value={form.phone}
                        onChange={handleChange}
                        placeholder="0901 234 567"
                      />
                    </div>
                  </div>

                  <div className="form-group">
                    <label htmlFor="subject">Chủ đề cần tư vấn</label>
                    <select
                      id="subject"
                      name="subject"
                      value={form.subject}
                      onChange={handleChange}
                    >
                      <option value="dat-ve">Đặt vé bơi / Mua vé tháng</option>
                      <option value="hoc-boi">Đăng ký lớp học bơi</option>
                      <option value="gop-y">Góp ý chất lượng dịch vụ</option>
                      <option value="su-co">Báo sự cố &amp; Hỗ trợ kỹ thuật</option>
                      <option value="khac">Chủ đề khác</option>
                    </select>
                  </div>

                  <div className="form-group">
                    <label htmlFor="message">Nội dung tin nhắn <span className="req">*</span></label>
                    <textarea
                      id="message"
                      name="message"
                      rows={4}
                      value={form.message}
                      onChange={handleChange}
                      placeholder="Mô tả chi tiết câu hỏi hoặc yêu cầu của bạn..."
                      className={errors.message ? 'input-error' : ''}
                    />
                    {errors.message && <p className="error-text">{errors.message}</p>}
                  </div>

                  <button type="submit" className="submit-btn" disabled={submitting}>
                    {submitting ? (
                      <span className="btn-loading">
                        <span className="spinner" />
                        Đang gửi tin nhắn...
                      </span>
                    ) : (
                      <span>Gửi tin nhắn ngay →</span>
                    )}
                  </button>
                </form>
              )}
            </div>
          </div>
        </div>
      </section>

      {/* ── 3. MAP SECTION ── */}
      <section className="contact-map">
        <div className="container">
          <div className="section-header reveal" style={{ textAlign: 'center' }}>
            <span className="section-kicker">✦</span>
            <h2 className="section-title">Bản đồ hướng dẫn đường đi</h2>
          </div>
          <div className="map-frame reveal">
            <iframe
              src="https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d3919.4167463545044!2d106.69540097480726!3d10.777426089376016!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x31752f38f9ed887b%3A0x14aded5703768989!2sBen%20Nghe%2C%20District%201%2C%20Ho%20Chi%20Minh%20City!5e0!3m2!1sen!2svn!4v1696000000000!5m2!1sen!2svn"
              width="100%"
              height="420"
              style={{ border: 0 }}
              allowFullScreen
              loading="lazy"
              title="Bản đồ Bể bơi Thành Minh"
            />
          </div>
        </div>
      </section>

      {/* ── 4. FAQ SECTION ── */}
      <section className="contact-faq">
        <div className="container">
          <div className="section-header reveal" style={{ textAlign: 'center' }}>
            <span className="section-kicker">✦</span>
            <h2 className="section-title">Câu hỏi thường gặp</h2>
          </div>

          <div className="faq-list">
            <details className="faq-item reveal">
              <summary>
                <span>Tôi có thể mua vé trực tuyến và dùng ngay được không?</span>
                <span className="faq-arrow">▼</span>
              </summary>
              <div className="faq-body">
                <p>
                  Có, bạn hoàn toàn có thể mua vé trực tuyến trên website. Sau khi thanh toán thành công, hệ thống sẽ gửi mã QR vào mục Đơn hàng và email của bạn. Bạn chỉ cần quét mã QR tại cổng kiểm soát để vào bơi ngay mà không cần xếp hàng mua vé giấy.
                </p>
              </div>
            </details>

            <details className="faq-item reveal">
              <summary>
                <span>Bể bơi có quy định về trang phục bơi lội không?</span>
                <span className="faq-arrow">▼</span>
              </summary>
              <div className="faq-body">
                <p>
                  Để đảm bảo vệ sinh nguồn nước và an toàn cho mọi người, khách hàng vui lòng mặc đồ bơi chuyên dụng (quần bơi, áo bơi). Nếu quên mang theo, chúng tôi có sẵn dịch vụ cho thuê đồ bơi và phụ kiện sạch sẽ, đã tiệt trùng ngay tại quầy tiện ích.
                </p>
              </div>
            </details>

            <details className="faq-item reveal">
              <summary>
                <span>Trẻ em có khu vực bơi riêng không?</span>
                <span className="faq-arrow">▼</span>
              </summary>
              <div className="faq-body">
                <p>
                  Bể bơi Thành Minh bố trí 2 khu vực hồ bơi riêng biệt: bể trẻ em dành cho các bé cao dưới 1,4m với độ sâu vừa phải và bể lớn 10x23m dành cho người lớn. Cả hai khu vực đều có nhân viên cứu hộ túc trực và camera AI giám sát 24/7.
                </p>
              </div>
            </details>

            <details className="faq-item reveal">
              <summary>
                <span>Tủ đồ cá nhân và gửi xe có mất phí không?</span>
                <span className="faq-arrow">▼</span>
              </summary>
              <div className="faq-body">
                <p>
                  Cả dịch vụ gửi xe và tủ khóa đồ cá nhân tại Bể bơi Thành Minh đều hoàn toàn <strong>miễn phí 100%</strong> cho mọi khách hàng mua vé bơi.
                </p>
              </div>
            </details>
          </div>
        </div>
      </section>
    </div>
  )
}
