import './ContactPage.css'
import { useState, useEffect } from 'react'

export default function ContactPage() {
  const [form, setForm] = useState({ name: '', email: '', phone: '', subject: 'dat-ve', message: '' })
  const [errors, setErrors] = useState({})
  const [submitting, setSubmitting] = useState(false)
  const [submitted, setSubmitted] = useState(false)

  const validate = () => {
    const errs = {}
    if (!form.name.trim()) errs.name = 'Vui lòng nhập họ tên'
    if (!form.email.trim()) errs.email = 'Vui lòng nhập email'
    else if (!/^[^@]+@[^@]+\.[^@]+$/.test(form.email)) errs.email = 'Email không hợp lệ'
    if (!form.message.trim()) errs.message = 'Vui lòng nhập nội dung'
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
    }, 1500)
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
      {/* Hero Section */}
      <div className="contact-hero">
        {/* Video background */}
        <video
          className="contact-hero-video"
          autoPlay muted loop playsInline preload="auto"
          aria-hidden="true"
        >
          <source src="https://d8j0ntlcm91z4.cloudfront.net/user_38xzZboKViGWJOttwIXH07lWA1P/hf_20260418_094631_d30ab262-45ee-4b7d-99f3-5d5848c8ef13.mp4" type="video/mp4" />
        </video>
        <div className="contact-hero-overlay" aria-hidden="true" />
        {/* Waves */}
        <div className="contact-hero-waves" aria-hidden="true">
          <div className="chw-track chw1">
            <svg viewBox="0 0 1440 90" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,45 C240,90 480,0 720,45 C960,90 1200,0 1440,45 L1440,90 L0,90 Z" fill="var(--bg,#FEF1E6)"/>
            </svg>
            <svg viewBox="0 0 1440 90" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,45 C240,90 480,0 720,45 C960,90 1200,0 1440,45 L1440,90 L0,90 Z" fill="var(--bg,#FEF1E6)"/>
            </svg>
          </div>
          <div className="chw-track chw2">
            <svg viewBox="0 0 1440 90" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,65 C360,15 720,90 1080,45 C1260,22 1380,70 1440,55 L1440,90 L0,90 Z" fill="rgba(0,119,182,0.18)"/>
            </svg>
            <svg viewBox="0 0 1440 90" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,65 C360,15 720,90 1080,45 C1260,22 1380,70 1440,55 L1440,90 L0,90 Z" fill="rgba(0,119,182,0.18)"/>
            </svg>
          </div>
        </div>
        {/* Content */}
        <div className="contact-hero-content">
          <h1>Liên hệ với chúng tôi</h1>
          <p>Chúng tôi luôn sẵn sàng lắng nghe và hỗ trợ bạn. Đừng ngần ngại liên hệ!</p>
        </div>
      </div>

      {/* Main Content */}
      <div className="contact-main">
        <div className="container">
          <div className="contact-grid reveal">
            {/* Left Column - Contact Info */}
            <div className="contact-info">
              <h2>Thông tin liên hệ</h2>

              <div className="info-item">
                <div className="info-icon">📍</div>
                <div className="info-text">
                  <h4>Địa chỉ</h4>
                  <p>Đông Anh, Hà Nội</p>
                </div>
              </div>

              <div className="info-item">
                <div className="info-icon">📞</div>
                <div className="info-text">
                  <h4>Điện thoại</h4>
                  <p> Zalo: 0901 234 567</p>
                </div>
              </div>

              <div className="info-item">
                <div className="info-icon">✉️</div>
                <div className="info-text">
                  <h4>Email</h4>
                  <p>contact@thanhminh-pool.vn</p>
                </div>
              </div>

              <div className="info-item">
                <div className="info-icon">🕐</div>
                <div className="info-text">
                  <h4>Giờ hoạt động</h4>
                  <p>
                Sáng: 6:00 - 10:00 <br />
                Chiều: 15:00 - 20:00</p>
                </div>
              </div>

              <div className="social-links">
                <a href="#" className="social-link">👍 Facebook</a>
                <a href="#" className="social-link">💬 Zalo</a>
                <a href="#" className="social-link">📷 Instagram</a>
              </div>
            </div>

            {/* Right Column - Contact Form */}
            <div className="contact-form-wrapper">
              {submitted ? (
                <div className="success-msg">
                  ✅ Cảm ơn! Chúng tôi sẽ liên hệ với bạn trong 24 giờ.
                </div>
              ) : (
                <form onSubmit={handleSubmit} noValidate>
                  <div className="form-group">
                    <label htmlFor="name">Họ và tên *</label>
                    <input
                      type="text"
                      id="name"
                      name="name"
                      value={form.name}
                      onChange={handleChange}
                      placeholder="Nguyễn Văn A"
                    />
                    {errors.name && <p className="error-text">{errors.name}</p>}
                  </div>

                  <div className="form-group">
                    <label htmlFor="email">Email *</label>
                    <input
                      type="email"
                      id="email"
                      name="email"
                      value={form.email}
                      onChange={handleChange}
                      placeholder="example@email.com"
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

                  <div className="form-group">
                    <label htmlFor="subject">Chủ đề</label>
                    <select
                      id="subject"
                      name="subject"
                      value={form.subject}
                      onChange={handleChange}
                    >
                      <option value="dat-ve">Đặt vé</option>
                      <option value="gop-y">Góp ý dịch vụ</option>
                      <option value="su-co">Báo sự cố</option>
                      <option value="khac">Khác</option>
                    </select>
                  </div>

                  <div className="form-group">
                    <label htmlFor="message">Nội dung *</label>
                    <textarea
                      id="message"
                      name="message"
                      rows={5}
                      value={form.message}
                      onChange={handleChange}
                      placeholder="Nhập nội dung tin nhắn của bạn..."
                    />
                    {errors.message && <p className="error-text">{errors.message}</p>}
                  </div>

                  <button type="submit" className="submit-btn" disabled={submitting}>
                    {submitting ? 'Đang gửi...' : 'Gửi tin nhắn'}
                  </button>
                </form>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Map Section */}
      <div className="contact-map">
        <div className="container">
          <h2 className="reveal">Tìm chúng tôi</h2>
          <div className="reveal">
            <iframe
              src="https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d3919.4167463545044!2d106.69540097480726!3d10.777426089376016!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x31752f38f9ed887b%3A0x14aded5703768989!2sBen%20Nghe%2C%20District%201%2C%20Ho%20Chi%20Minh%20City!5e0!3m2!1sen!2svn!4v1696000000000!5m2!1sen!2svn"
              width="100%"
              height="400"
              style={{ border: 0 }}
              allowFullScreen
              loading="lazy"
              title="Bản đồ Bể bơi Thành Minh"
            />
          </div>
        </div>
      </div>

      {/* FAQ Section */}
      <div className="contact-faq">
        <div className="container">
          <h2 className="reveal">Câu hỏi thường gặp</h2>

          <details className="reveal">
            <summary>Tôi có thể đặt vé trực tuyến không?</summary>
            <p>
              Có, bạn hoàn toàn có thể đặt vé trực tuyến thông qua website hoặc ứng dụng của chúng tôi.
              Sau khi đặt vé, bạn sẽ nhận được mã QR qua email để sử dụng khi đến bể bơi.
            </p>
          </details>

          <details className="reveal">
            <summary>Thời gian phản hồi tin nhắn liên hệ là bao lâu?</summary>
            <p>
              Chúng tôi cam kết phản hồi tất cả các tin nhắn trong vòng 24 giờ làm việc.
              Đối với các vấn đề khẩn cấp, vui lòng liên hệ trực tiếp qua số điện thoại (028) 1234 5678.
            </p>
          </details>

          <details className="reveal">
            <summary>Tôi có thể huỷ hoặc đổi vé đã đặt không?</summary>
            <p>
              Bạn có thể huỷ hoặc đổi vé trước 24 giờ so với giờ sử dụng mà không mất phí.
              Đối với các trường hợp huỷ muộn hơn, vui lòng liên hệ bộ phận hỗ trợ khách hàng của chúng tôi.
            </p>
          </details>
        </div>
      </div>
    </div>
  )
}
