import { Link } from 'react-router-dom'
import './Footer.css'
import logoImg from '../../assets/logo-be-boi-thanh-minh.png'

export default function Footer() {
  return (
    <footer className="footer">
      <div className="container">
        <div className="footer-grid">
          {/* Brand */}
          <div>
            <div className="footer-brand-name">
              <img src={logoImg} alt="Thanh Minh Logo" className="footer-logo-img" />
              Thanh Minh Smart Pool
            </div>
            <p className="footer-brand-desc">
              Hệ thống quản lý hồ bơi thông minh — nơi mang lại trải nghiệm bơi lội
              an toàn, hiện đại và đẳng cấp cho mọi lứa tuổi.
            </p>
            <div className="footer-social">
              <a href="https://facebook.com" target="_blank" rel="noreferrer" aria-label="Facebook">📘</a>
              <a href="https://zalo.me" target="_blank" rel="noreferrer" aria-label="Zalo">💬</a>
              <a href="https://youtube.com" target="_blank" rel="noreferrer" aria-label="YouTube">▶️</a>
              <a href="mailto:info@thanhminh.vn" aria-label="Email">📧</a>
            </div>
          </div>

          {/* Quick Links */}
          <div className="footer-col">
            <h4>Liên kết nhanh</h4>
            <div className="footer-links">
              <Link to="/">→ Trang chủ</Link>
              <Link to="/gioi-thieu">→ Giới thiệu</Link>
            </div>
            <div className="footer-service-item">
              <span className="footer-contact-icon"></span>
              <span>Bảng giá vé</span>
            </div>
             <div className="footer-service-item">
              <span className="footer-contact-icon"></span>
              <span>Sơ đồ hồ bơi</span>
            </div>
          </div>

          {/* Services */}
          <div className="footer-col">
            <h4>Dịch vụ</h4>
             <div className="footer-service-item">
              <span className="footer-contact-icon"></span>
              <span>Dịch vụ đồ ăn</span>
            </div>
            <div className="footer-service-item">
              <span className="footer-contact-icon"></span>
              <span>Dịch vụ thuê đồ</span>
            </div>
            <div className="footer-service-item">
              <span className="footer-contact-icon"></span>
              <span>Tủ đồ miễn phí</span>
            </div>
            <div className="footer-service-item">
              <span className="footer-contact-icon"></span>
              <span>Trông xe miễn phí</span>
            </div>
          </div>

          {/* Contact */}
          <div className="footer-col">
            <h4>Liên hệ</h4>
            <div className="footer-contact-item">
              <span className="footer-contact-icon">📍</span>
              <span>614 Lạc Long Quân, Phường Tây Hồ, Hà Nội</span>
            </div>
            <div className="footer-contact-item">
              <span className="footer-contact-icon">📞</span>
              <span>(84-24) 37 184 222 / 37 100 957</span>
            </div>
            <div className="footer-contact-item">
              <span className="footer-contact-icon">📠</span>
              <span>Fax: (84-24) 37 184 190</span>
            </div>
            <div className="footer-contact-item">
              <span className="footer-contact-icon">📧</span>
              <span>info@thanhminh-pool.vn</span>
            </div>
            <div className="footer-contact-item">
              <span className="footer-contact-icon">🕐</span>
              <span>Mở cửa: 8:00 – 18:00 hàng ngày</span>
            </div>
          </div>
        </div>
      </div>

      <div className="footer-bottom">
        <div className="container" style={{ display: 'flex', justifyContent: 'space-between', width: '100%' }}>
          <span>© 2026 Thanh Minh Smart Pool. All rights reserved.</span>
          <div className="footer-bottom-links">
            <a href="/chinh-sach">Chính sách</a>
            <a href="/bao-mat">Bảo mật</a>
            <a href="/dieu-khoan">Điều khoản</a>
          </div>
        </div>
      </div>
    </footer>
  )
}
