export default function AuthPhoto() {
  return (
    <section className="auth-photo" aria-label="Giới thiệu bể bơi thông minh Thành Minh">
      {/* ── Soft Blue Background & Ambient Glowing Spots ── */}
      <div className="auth-photo-bg" aria-hidden="true">
        {/* Ambient large blur orbs */}
        <div className="auth-spot-orb spot-1" />
        <div className="auth-spot-orb spot-2" />
        <div className="auth-spot-orb spot-3" />
        <div className="auth-spot-orb spot-4" />

        {/* Floating light spots / glowing dots */}
        <div className="auth-glow-dots">
          {[...Array(8)].map((_, index) => (
            <div key={index} className={`auth-glow-dot gd-${index + 1}`} />
          ))}
        </div>

        {/* Delicate subtle dots pattern overlay */}
        <div className="auth-dots-pattern" />

        {/* Bottom soft waves */}
        <div className="auth-waves-wrap">
          <div className="auth-wave aw1">
            <svg viewBox="0 0 2880 120" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,60 C480,110 960,10 1440,60 C1920,110 2400,10 2880,60 L2880,120 L0,120 Z" fill="rgba(0, 119, 182, 0.08)" />
            </svg>
          </div>
          <div className="auth-wave aw2">
            <svg viewBox="0 0 2880 120" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,80 C480,30 960,95 1440,55 C1920,15 2400,85 2880,45 L2880,120 L0,120 Z" fill="rgba(0, 180, 216, 0.06)" />
            </svg>
          </div>
        </div>
      </div>

      {/* ── Foreground Content ── */}
      <div className="auth-photo-content">
        {/* Badge */}
        <div className="auth-photo-badge">
          <span className="auth-badge-pulse" />
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" width="15" height="15">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          </svg>
          An toàn · Tiêu chuẩn · Chất lượng
        </div>

        {/* Headline */}
        <h2 className="auth-photo-headline">
          Trải nghiệm bơi lội<br />
          <span className="auth-headline-highlight">đẳng cấp &amp; tiện nghi</span><br />
          tại Thành Minh
        </h2>

        {/* Highlight Feature Cards */}
        <div className="auth-features-list">
          <div className="auth-feature-card">
            <div className="auth-feature-icon-wrap icon-shield">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="20" height="20">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                <path d="M9 12l2 2 4-4" />
              </svg>
            </div>
            <div className="auth-feature-info">
              <h4>Camera AI Giám sát 24/7</h4>
              <p>Nhận diện nguy cơ và hỗ trợ cứu hộ tức thì</p>
            </div>
          </div>

          <div className="auth-feature-card">
            <div className="auth-feature-icon-wrap icon-spark">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="20" height="20">
                <rect x="2" y="5" width="20" height="14" rx="2" />
                <line x1="2" y1="10" x2="22" y2="10" />
              </svg>
            </div>
            <div className="auth-feature-info">
              <h4>Tiện ích trọn gói</h4>
              <p>Vé điện tử QR, tủ đồ an toàn &amp; dịch vụ cho thuê đồ bơi</p>
            </div>
          </div>
        </div>

        {/* Trust Badges */}
        <div className="auth-trust-strip">
          <div className="trust-item">
            <strong className="trust-number">10+</strong>
            <span className="trust-label">Năm uy tín</span>
          </div>
          <div className="trust-divider" />
          <div className="trust-item">
            <strong className="trust-number">2</strong>
            <span className="trust-label">Bể bơi tiêu chuẩn</span>
          </div>
          <div className="trust-divider" />
          <div className="trust-item">
            <strong className="trust-number">AI 24/7</strong>
            <span className="trust-label">Giám sát thông minh</span>
          </div>
        </div>
      </div>
    </section>
  )
}
